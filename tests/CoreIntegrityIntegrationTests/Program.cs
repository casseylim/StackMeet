using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;
using StackMeet.Api.Services;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_CoreIntegrityTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
var builder = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = databaseName, IntegratedSecurity = true, TrustServerCertificate = true, MultipleActiveResultSets = true, ConnectTimeout = 5 };
if (!string.Equals(builder.DataSource, server, StringComparison.Ordinal)) throw new InvalidOperationException("Refusing integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_CoreIntegrityTest_", StringComparison.Ordinal) || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_'))) throw new InvalidOperationException("Refusing cleanup: generated database name failed safety validation.");
Console.WriteLine($"CoreIntegrity LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
var options = new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(builder.ConnectionString).Options;
Exception? primaryFailure = null;
Exception? cleanupFailure = null;
try
{
    Console.WriteLine("Preflight: starting");
    var probeBuilder = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };
    await using (var probe = new SqlConnection(probeBuilder.ConnectionString))
    {
        using var probeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try { await probe.OpenAsync(probeTimeout.Token); }
        catch (Exception ex) { throw new InvalidOperationException($"LocalDB preflight failed for {server}. Verify SqlLocalDB instance health before rerunning. {ex.Message}", ex); }
    }
    Console.WriteLine("Preflight: passed");
    Console.WriteLine("Migration: starting");
    await using var db = new StackMeetDbContext(options);
    using var migrationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    try { await db.Database.MigrateAsync(migrationTimeout.Token); }
    catch (Exception ex) { throw new InvalidOperationException($"LocalDB migration phase failed for {databaseName}. {ex.Message}", ex); }
    Console.WriteLine("Migration: passed");
    Console.WriteLine("Assertions: starting");
    var passwordService = new PasswordHashService();
    const string syntheticPassword = "Synthetic-only-password!";
    var generatedHash = passwordService.Hash(syntheticPassword);
    Assert(passwordService.Verify(syntheticPassword, generatedHash), "ENG02 current password hash round trip");
    Assert(!passwordService.Verify("wrong-synthetic-password", generatedHash), "ENG02 incorrect password rejected");
    var hashFields = generatedHash.Split('$');
    string WithHashField(int index, string value)
    {
        var fields = (string[])hashFields.Clone();
        fields[index] = value;
        return string.Join("$", fields);
    }
    foreach (var iteration in new[] { "0", "-1", "1000001", "2147483647", "invalid" })
        Assert(!passwordService.Verify(syntheticPassword, WithHashField(1, iteration)), "ENG02 malformed or excessive iterations rejected");
    foreach (var field in new[] { 2, 3 })
        foreach (var invalid in new[] { "", "!!!", Convert.ToBase64String(new byte[1]), Convert.ToBase64String(new byte[65]) })
            Assert(!passwordService.Verify(syntheticPassword, WithHashField(field, invalid)), "ENG02 malformed salt or digest rejected");
    Assert(!passwordService.Verify(syntheticPassword, new string('x', 501)), "ENG02 oversized hash rejected");
    Assert(!passwordService.Verify(syntheticPassword, WithHashField(0, "unsupported")), "ENG02 unsupported hash algorithm rejected");
    Assert(!passwordService.Verify(syntheticPassword, null), "ENG02 null stored hash rejected");
    // Compatibility: the encoded iteration count remains usable for valid prior hashes.
    var priorSalt = new byte[16];
    var priorKey = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(syntheticPassword, priorSalt, 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
    var priorHash = string.Join("$", "PBKDF2-SHA256", "100000", Convert.ToBase64String(priorSalt), Convert.ToBase64String(priorKey));
    Assert(passwordService.Verify(syntheticPassword, priorHash), "ENG02 valid historical iteration count supported");

    const string sessionTestKey = "synthetic-eng02b-local-only-signing-key";
    var sessionConfig = new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Security:SessionSigningKey"] = sessionTestKey }).Build();
    var sessionService = new SessionTokenService(sessionConfig);
    var legacySession = sessionService.Create("SYNTHETIC", "测试 Malay");
    Assert(sessionService.TryValidate(legacySession.ToString(), out var parsedLegacy)
        && !parsedLegacy.IsAccountSession && parsedLegacy.CompetitionId == "SYNTHETIC"
        && parsedLegacy.DisplayName == "测试 Malay", "ENG02B valid legacy session round trip");
    var longLegacy = sessionService.Create("SYNTHETIC", new string('测', SessionTokenService.MaxLegacyDisplayNameLength));
    Assert(sessionService.TryValidate(longLegacy.ToString(), out _), "ENG02B maximum supported legacy display name remains usable");
    var accountSession = sessionService.CreateForUser(123, "synthetic@example.test", "Synthetic", true, 7);
    Assert(sessionService.TryValidate(accountSession.ToString(), out var parsedAccount)
        && parsedAccount.UserId == 123 && parsedAccount.IsSystemAdmin && parsedAccount.SessionVersion == 7,
        "ENG02B valid account session round trip");
    string EncodeText(string text) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));
    string EncodeUrl(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    string SignSyntheticPayload(byte[] value)
    {
        using var signer = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(sessionTestKey));
        return EncodeUrl(value) + "." + EncodeUrl(signer.ComputeHash(value));
    }
    var validUntil = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();
    var expiredAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
    var emailField = EncodeText("synthetic@example.test");
    var nameField = EncodeText("Synthetic");
    var competitionField = EncodeText("SYNTHETIC");
    foreach (var malformed in new[]
    {
        $"v3|0|{emailField}|{nameField}|1|7|{validUntil}",
        $"v3|123|{emailField}|{nameField}|2|7|{validUntil}",
        $"v3|123|{emailField}|{nameField}|1|0|{validUntil}",
        $"v3|123|!!!|{nameField}|1|7|{validUntil}",
        $"v3|123||{nameField}|1|7|{validUntil}",
        $"v3|123|{emailField}|{nameField}|1|7|{long.MaxValue}",
        $"v3|123|{emailField}|{nameField}|1|7|{expiredAt}",
        $"{competitionField}|!!!|{validUntil}",
        $"{competitionField}|{nameField}|{long.MinValue}",
        $"{competitionField}|{nameField}|{expiredAt}",
        $"|{nameField}|{validUntil}"
    })
    {
        Assert(!sessionService.TryValidate(SignSyntheticPayload(System.Text.Encoding.UTF8.GetBytes(malformed)), out var rejected)
            && !rejected.IsAccountSession && rejected.CompetitionId == "", "ENG02B malformed or expired signed token rejected");
    }
    Assert(!sessionService.TryValidate(SignSyntheticPayload(new byte[] { 0xff, 0xfe }), out _), "ENG02B invalid UTF-8 rejected");
    foreach (var malformed in new string?[] { null, "", ".", "bad.token", new string('x', 8193), accountSession.Payload + "." + new string('A', 43) })
        Assert(!sessionService.TryValidate(malformed, out _), "ENG02B invalid unsigned token rejected");
    Assert(CompetitionResultRules.MaximumBatchSize == 500, "batch limit");
    Assert(CompetitionResultRules.NormalizeParticipantType(" relay ") == "Timed Relay", "legacy relay normalization");
    Assert(CompetitionResultRules.NormalizeStage(" finals ") == "Finals", "stage normalization");
    Assert(CompetitionResultRules.NormalizeEvent(" cycle ") == "Cycle", "event normalization");
    Assert(CompetitionResultRules.ValidateIdentity("Finals", "Individual", "A1", "3-3-3").Count == 0, "valid identity");
    Assert(CompetitionResultRules.ValidateIdentity("Finals", "Timed Relay", "A1", "Cycle").Count > 0, "relay event rule");
    Assert(CompetitionResultRules.ValidateAttempts([1.234m, 2.345m, 3.456m], 0).Count == 0, "valid attempts");
    Assert(CompetitionResultRules.ValidateAttempts([], 0).Count > 0, "empty attempts rejected");
    Assert(CompetitionResultRules.ValidateAttempts([1.2345m], 0).Count > 0, "precision rejected");
    Assert(CompetitionResultRules.ValidateIdentity("Finals", "Individual", new string('x', 51), "Cycle").Count > 0, "participant length rejected");
    var references = new CompetitionParticipantReferenceService();
    var extracted = references.ExtractReferencedCodes("{\"doubles\":{\"one\":\"A1\",\"two\":\"A2\",\"parentName\":\"External Name\"},\"relay\":{\"members\":[\"A3\"]}}");
    Assert(extracted.Count == 3 && new[] { "A1", "A2", "A3" }.All(extracted.Contains), "state participant references");
    Assert(!references.ExtractReferencedCodes("{\"doubles\":{\"parentName\":\"External Name\"}}").Contains("External Name"), "external parentName ignored");

    Assert(references.ContainsParticipant("{\"relays\":[{\"members\":[\"1.1\",\"1.2\"]}]}", "1.2"), "relay members array reference protected");
    Assert(references.ContainsParticipant("{\"doubles\":[{\"childStackerId\":\"1.3\",\"parentStackerId\":\"1.4\"}]}", "1.4"), "child parent doubles alias protected");
    Assert(references.ContainsParticipant("{\"doubles\":[{\"stackerOneId\":\"1.5\",\"stackerTwoId\":\"1.6\"}]}", "1.5"), "legacy doubles alias protected");
    Assert(references.ContainsParticipant("{\"selectedQualifiers\":[\"ABC-1\"]}", "abc-1"), "participant references compare case-insensitively");
    Assert(references.ContainsParticipant("{\"relays\":[", "1.1"), "malformed state fails closed for deletion safety");
    Assert(!references.ContainsParticipant("{\"doubles\":[{\"parentName\":\"1.7\"}]}", "1.7"), "external parent name is not a participant reference");
    Assert(!references.ContainsParticipant("{\"notes\":{\"membersText\":\"1.8\"}}", "1.8"), "unrelated text is not a participant reference");

    var teamIntegrity = new CompetitionTeamResultIntegrityService(db);
    var teamState = "{\"doubles\":[{\"id\":\"2.1\",\"one\":\"A1\",\"two\":\"A2\"},{\"id\":\"2.2\",\"one\":\"A1\",\"status\":\"pending\"},{\"id\":\"2.3\",\"type\":\"child_parent\",\"one\":\"A3\",\"parentName\":\"External Parent\"},{\"id\":\"2.4\",\"stackerOneId\":\"A1\",\"stackerTwoId\":\"A2\"},{\"id\":\"2.5\",\"one\":\"A3\",\"parentName\":\"Legacy External Parent\"}],\"relays\":[{\"id\":\"3.1\",\"members\":[\"A1\",\"A2\",\"A3\",\"A4\"]},{\"id\":\"3.2\",\"members\":[\"A1\",\"A2\",\"A3\"]},{\"id\":\"3.3\",\"one\":\"A1\",\"two\":\"A2\",\"three\":\"A3\",\"four\":\"A4\"}]}";
    Assert(teamIntegrity.TryReadReadyTeams(teamState, out var readyTeams, out var teamStateError) && teamStateError is null, "team state parsed");
    Assert(readyTeams.Doubles.SetEquals(new[] { "2.1", "2.3", "2.4", "2.5" }), "complete doubles detected including legacy aliases");
    Assert(readyTeams.TimedRelays.SetEquals(new[] { "3.1", "3.3" }), "ready relays detected including legacy member slots");
    Assert(teamIntegrity.ValidateResultUpserts(teamState, [new ResultUpsertRequest("Finals", "Doubles", "2.1", "Cycle", [8.123m], 0, null)]) is null, "valid doubles result team accepted");
    Assert(teamIntegrity.ValidateResultUpserts(teamState, [new ResultUpsertRequest("Finals", "Doubles", "2.2", "Cycle", [8.123m], 0, null)]) is not null, "pending doubles result team rejected");
    Assert(teamIntegrity.ValidateResultUpserts(teamState, [new ResultUpsertRequest("Finals", "Timed Relay", "3.1", "3-6-3", [20.123m], 0, null)]) is null, "valid relay result team accepted");
    Assert(teamIntegrity.ValidateResultUpserts(teamState, [new ResultUpsertRequest("Finals", "Timed Relay", "3.2", "3-6-3", [20.123m], 0, null)]) is not null, "incomplete relay result team rejected");
    Assert(!teamIntegrity.TryReadReadyTeams("{\"doubles\":[{\"id\":\"2.1\",\"two\":\"A2\"},{\"id\":\"2.1\",\"two\":\"A3\"}]}", out _, out var duplicateTeamError) && duplicateTeamError?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true, "duplicate team IDs fail closed");
    Assert(!teamIntegrity.TryReadReadyTeams("{\"relays\":[", out _, out var malformedTeamError) && malformedTeamError is not null, "malformed team state fails closed");

    var teamCompetitionKey = $"TEAM_{Guid.NewGuid():N}"[..20].ToUpperInvariant();
    var teamCompetition = new StackMeet.Api.Models.Competition
    {
        CompetitionCode = teamCompetitionKey,
        CompetitionKey = teamCompetitionKey,
        CompetitionName = "Team Integrity",
        Venue = "LocalDB",
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow),
        Status = "Active",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    db.Competitions.Add(teamCompetition);
    await db.SaveChangesAsync();
    db.Stackers.AddRange(
        new StackMeet.Api.Models.Stacker { CompetitionId = teamCompetition.Id, StackerCode = "A1", FirstName = "A", LastName = "One", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new StackMeet.Api.Models.Stacker { CompetitionId = teamCompetition.Id, StackerCode = "A2", FirstName = "A", LastName = "Two", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new StackMeet.Api.Models.Stacker { CompetitionId = teamCompetition.Id, StackerCode = "A3", FirstName = "A", LastName = "Three", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new StackMeet.Api.Models.Stacker { CompetitionId = teamCompetition.Id, StackerCode = "A4", FirstName = "A", LastName = "Four", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    Assert(await teamIntegrity.ValidateResultUpsertsAsync(
        teamCompetition.Id,
        teamState,
        [new ResultUpsertRequest("Finals", "Doubles", "2.1", "Cycle", [8.123m], 0, null)]) is null,
        "team result resolves to registered competition stackers");

    var foreignMemberState = "{\"doubles\":[{\"id\":\"2.9\",\"one\":\"A1\",\"two\":\"FOREIGN\"}],\"relays\":[]}";
    Assert(await teamIntegrity.ValidateResultUpsertsAsync(
        teamCompetition.Id,
        foreignMemberState,
        [new ResultUpsertRequest("Finals", "Doubles", "2.9", "Cycle", [8.123m], 0, null)]) is not null,
        "team result rejects member outside competition stackers");

    var externalParentState = "{\"doubles\":[{\"id\":\"2.8\",\"one\":\"A3\",\"parentName\":\"External Parent\"}],\"relays\":[]}";
    Assert(await teamIntegrity.ValidateResultUpsertsAsync(
        teamCompetition.Id,
        externalParentState,
        [new ResultUpsertRequest("Finals", "Doubles", "2.8", "Cycle", [8.123m], 0, null)]) is null,
        "child parent result links registered child while external parent remains external");

    db.CompetitionResults.Add(new StackMeet.Api.Models.CompetitionResult
    {
        CompetitionId = teamCompetition.Id,
        Stage = "Finals",
        ParticipantType = "Doubles",
        ParticipantCode = "2.1",
        EventCode = "Cycle",
        AttemptsJson = "[8.123]",
        Penalty = 0,
        Revision = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var metadataOnlyTeamState = teamState.Replace(
        "{\"id\":\"2.1\",\"one\":\"A1\",\"two\":\"A2\"}",
        "{\"id\":\"2.1\",\"one\":\"A1\",\"two\":\"A2\",\"division\":\"12U\"}",
        StringComparison.Ordinal);
    Assert(await teamIntegrity.ValidateStateAgainstExistingResultsAsync(
        teamCompetition.Id,
        teamState,
        metadataOnlyTeamState) is null,
        "team metadata may change while result membership stays fixed");

    var swappedMemberState = teamState.Replace(
        "{\"id\":\"2.1\",\"one\":\"A1\",\"two\":\"A2\"}",
        "{\"id\":\"2.1\",\"one\":\"A1\",\"two\":\"A3\"}",
        StringComparison.Ordinal);
    Assert(await teamIntegrity.ValidateStateAgainstExistingResultsAsync(
        teamCompetition.Id,
        teamState,
        swappedMemberState) is not null,
        "team members cannot change while SQL results reference team");

    await RunHttpAcceptanceAsync(builder.ConnectionString, db);

    Console.WriteLine("Assertions: passed");
    Console.WriteLine("CoreIntegrity LocalDB integration harness passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"Primary failure: {ex.Message}");
}
finally
{
    Console.WriteLine("Cleanup: starting");
    try
    {
        var masterBuilder = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };
        await using var cleanup = new SqlConnection(masterBuilder.ConnectionString);
        using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await cleanup.OpenAsync(cleanupTimeout.Token);
        await using var command = cleanup.CreateCommand();
        command.CommandTimeout = 8;
        var escapedName = databaseName.Replace("]", "]]");
        command.CommandText = $"IF DB_ID(@databaseName) IS NOT NULL BEGIN ALTER DATABASE [{escapedName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{escapedName}]; END";
        command.Parameters.AddWithValue("@databaseName", databaseName);
        await command.ExecuteNonQueryAsync(cleanupTimeout.Token);
        Console.WriteLine($"Cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"Cleanup failed for {databaseName}: {ex.Message}");
    }
}
if (primaryFailure is not null) { if (cleanupFailure is not null) Console.Error.WriteLine($"Cleanup secondary failure: {cleanupFailure.Message}"); throw primaryFailure; }
if (cleanupFailure is not null) throw new InvalidOperationException("CoreIntegrity tests passed but cleanup failed.", cleanupFailure);
static void Assert(bool condition, string name) { if (!condition) throw new InvalidOperationException($"Failed scenario: {name}"); Console.WriteLine($"PASS {name}"); }

static async Task RunHttpAcceptanceAsync(string connectionString, StackMeetDbContext db)
{
    var key = $"HTTP_{Guid.NewGuid():N}"[..20].ToUpperInvariant();
    var now = DateTime.UtcNow;
    var competition = new StackMeet.Api.Models.Competition { CompetitionCode = key, CompetitionKey = key, CompetitionName = "HTTP Acceptance", Venue = "LocalDB", StartDate = DateOnly.FromDateTime(now), EndDate = DateOnly.FromDateTime(now), Status = "Active", CreatedAt = now, UpdatedAt = now };
    db.Competitions.Add(competition);
    await db.SaveChangesAsync();
    var other = new StackMeet.Api.Models.Competition { CompetitionCode = key + "B", CompetitionKey = key + "B", CompetitionName = "HTTP Acceptance B", Venue = "LocalDB", StartDate = DateOnly.FromDateTime(now), EndDate = DateOnly.FromDateTime(now), Status = "Active", CreatedAt = now, UpdatedAt = now };
    db.Competitions.Add(other);
    await db.SaveChangesAsync();
    db.Stackers.Add(new StackMeet.Api.Models.Stacker { CompetitionId = other.Id, StackerCode = "B1", FirstName = "B", LastName = "One", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = now, UpdatedAt = now });
    await db.SaveChangesAsync();
    db.Stackers.AddRange(new StackMeet.Api.Models.Stacker { CompetitionId = competition.Id, StackerCode = "A1", FirstName = "A", LastName = "One", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = now, UpdatedAt = now }, new StackMeet.Api.Models.Stacker { CompetitionId = competition.Id, StackerCode = "A2", FirstName = "A", LastName = "Two", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = now, UpdatedAt = now }, new StackMeet.Api.Models.Stacker { CompetitionId = competition.Id, StackerCode = "A3", FirstName = "A", LastName = "Three", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = now, UpdatedAt = now }, new StackMeet.Api.Models.Stacker { CompetitionId = competition.Id, StackerCode = "A4", FirstName = "A", LastName = "Four", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = now, UpdatedAt = now });
    db.CompetitionStates.Add(new StackMeet.Api.Models.CompetitionState { CompetitionKey = key, JsonData = "{\"seed\":true}", SchemaVersion = "0.9-online", StateRevision = 1, CreatedAt = now, UpdatedAt = now });
    await db.SaveChangesAsync();

    var apiRoot = Path.GetFullPath("backend/StackMeet.Api");
    var assembly = Path.Combine(apiRoot, "bin", "Release", "net8.0", "StackMeet.Api.dll");
    if (!File.Exists(assembly)) throw new FileNotFoundException("Build the Release API before running CoreIntegrity.", assembly);
    var startInfo = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = apiRoot,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    foreach (var argument in new[] { assembly, "--urls", "http://127.0.0.1:0" }) startInfo.ArgumentList.Add(argument);
    using var process = new Process { StartInfo = startInfo };
    process.StartInfo.Environment["ConnectionStrings__StackMeet"] = connectionString;
    const string signingKey = "synthetic-eng01-local-signing-key";
    process.StartInfo.Environment["Security__SessionSigningKey"] = signingKey;
    process.StartInfo.Environment["Security__AdminKey"] = "synthetic-eng01-admin";
    process.StartInfo.Environment["Security__ApiKey"] = "phase-e-http-test-key";
    process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
    process.StartInfo.Environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information";
    var logs = new ConcurrentQueue<string>();
    var listening = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
    void Capture(string stream, string? line)
    {
        if (line is null) return;
        logs.Enqueue($"{stream}: {line}");
        while (logs.Count > 50) logs.TryDequeue(out _);
        const string marker = "Now listening on: ";
        var offset = line.IndexOf(marker, StringComparison.Ordinal);
        if (offset >= 0 && Uri.TryCreate(line[(offset + marker.Length)..].Trim(), UriKind.Absolute, out var address)
            && address.Scheme == "http" && address.Host == "127.0.0.1" && address.Port > 0)
        {
            listening.TrySetResult(address);
        }
    }
    process.OutputDataReceived += (_, args) => Capture("stdout", args.Data);
    process.ErrorDataReceived += (_, args) => Capture("stderr", args.Data);
    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    try
    {
        using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Uri address;
        try
        {
            var exited = process.WaitForExitAsync(startup.Token);
            var completed = await Task.WhenAny(listening.Task, exited);
            if (completed == exited)
            {
                await exited;
                process.WaitForExit();
                throw new InvalidOperationException($"API host exited with code {process.ExitCode} before listening.");
            }
            address = await listening.Task;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"API startup failed. Process exited={process.HasExited}.\n{string.Join(Environment.NewLine, logs)}", ex);
        }

        var port = address.Port;
        using var client = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.Add("X-StackMeet-Api-Key", "phase-e-http-test-key");
        using var ready = await client.GetAsync($"/api/state/{key}");
        if (ready.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"API listened at {address} but SQL state readiness returned {(int)ready.StatusCode}.\n{string.Join(Environment.NewLine, logs)}");
        }
        Console.WriteLine($"CoreIntegrity API host bound {address}; SQL readiness passed.");
        await using (var isolated = new StackMeetDbContext(new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(connectionString).Options))
        {
            var engKey = key + "ENG";
            var eng = new StackMeet.Api.Models.Competition { CompetitionCode = engKey, CompetitionKey = engKey, CompetitionName = "ENG01 Acceptance", Venue = "LocalDB", StartDate = DateOnly.FromDateTime(now), EndDate = DateOnly.FromDateTime(now), Status = "Active", CreatedAt = now, UpdatedAt = now };
            isolated.Add(eng);
            await isolated.SaveChangesAsync();
            for (var i = 1; i <= 4; i++)
                isolated.Stackers.Add(new StackMeet.Api.Models.Stacker { CompetitionId = eng.Id, StackerCode = $"A{i}", FirstName = "Synthetic", LastName = $"{i}", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = now, UpdatedAt = now });
            isolated.CompetitionStates.Add(new StackMeet.Api.Models.CompetitionState { CompetitionKey = engKey, JsonData = "{\"seed\":true}", SchemaVersion = "0.9-online", StateRevision = 1, CreatedAt = now, UpdatedAt = now });
            await isolated.SaveChangesAsync();
            await RunResultAcceptanceAsync(port, eng.Id, engKey, signingKey, isolated);
            await RunPublicationAcceptanceAsync(port, engKey + "PUB", isolated);
            await RunStorageAuthorityAcceptanceAsync(port, engKey + "ST", isolated);
        }
        var original = await ready.Content.ReadAsStringAsync(); var originalEtag = ready.Headers.ETag?.Tag ?? throw new InvalidOperationException("Initial ETag missing.");
        var valid = "{\"doubles\":[{\"id\":\"2.1\",\"one\":\"A1\",\"two\":\"A2\",\"parentName\":\"External Parent\"}],\"relays\":[{\"id\":\"3.1\",\"members\":[\"A1\",\"A2\",\"A3\",\"A4\"]}],\"legacy\":{\"one\":\"A1\",\"two\":\"A2\"}}";
        var saved = await PostState(client, key, originalEtag, valid); Assert(saved.StatusCode == HttpStatusCode.NoContent, "HTTP valid state save");
        var after = await client.GetAsync($"/api/state/{key}"); var afterJson = await after.Content.ReadAsStringAsync(); var newEtag = after.Headers.ETag?.Tag ?? throw new InvalidOperationException("Updated ETag missing."); Assert(afterJson == valid && newEtag == "\"2\"", "HTTP revision and ETag increment");
        var invalid = await PostState(client, key, newEtag, "{\"doubles\":[{\"participantCode\":\"MISSING\",\"two\":\"A2\"}]}"); var invalidBody = await invalid.Content.ReadAsStringAsync(); Assert(invalid.StatusCode == HttpStatusCode.BadRequest, $"HTTP missing participant rejected ({(int)invalid.StatusCode}: {invalidBody})"); var unchanged = await client.GetAsync($"/api/state/{key}"); Assert(await unchanged.Content.ReadAsStringAsync() == valid && unchanged.Headers.ETag?.Tag == newEtag, "HTTP rejected state unchanged");
        var wrongCompetition = await PostState(client, key, newEtag, "{\"doubles\":[{\"one\":\"B1\",\"two\":\"A2\"}]}"); Assert(wrongCompetition.StatusCode == HttpStatusCode.BadRequest, "HTTP wrong-competition participant rejected");

        var referencedTeamResult = new StackMeet.Api.Models.CompetitionResult
        {
            CompetitionId = competition.Id,
            PublicId = Guid.NewGuid(),
            Stage = "Finals",
            ParticipantType = "Doubles",
            ParticipantCode = "2.1",
            EventCode = "Cycle",
            AttemptsJson = "[8.123]",
            Penalty = 0,
            Revision = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.CompetitionResults.Add(referencedTeamResult);
        await db.SaveChangesAsync();
        var removeReferencedTeam = await PostState(client, key, newEtag, "{\"doubles\":[],\"relays\":[{\"id\":\"3.1\",\"members\":[\"A1\",\"A2\",\"A3\",\"A4\"]}]}");
        Assert(removeReferencedTeam.StatusCode == HttpStatusCode.Conflict, "HTTP state cannot orphan existing doubles result");
        var afterRejectedTeamRemoval = await client.GetAsync($"/api/state/{key}");
        Assert(await afterRejectedTeamRemoval.Content.ReadAsStringAsync() == valid && afterRejectedTeamRemoval.Headers.ETag?.Tag == newEtag, "HTTP rejected team removal leaves state unchanged");
        db.CompetitionResults.Remove(referencedTeamResult);
        await db.SaveChangesAsync();
        var externalOnly = await PostState(client, key, newEtag, "{\"doubles\":[{\"one\":\"A1\",\"parentName\":\"External Parent\"}]}"); Assert(externalOnly.StatusCode == HttpStatusCode.NoContent, "HTTP external parent name ignored"); var currentEtag = (await client.GetAsync($"/api/state/{key}")).Headers.ETag!.Tag!;
        var stale = await PostState(client, key, newEtag, "{\"seed\":\"stale\"}"); Assert(stale.StatusCode == HttpStatusCode.Conflict && stale.Headers.ETag?.Tag == currentEtag, "HTTP OCC conflict preserved");
        var malformed = await PostState(client, key, currentEtag, "{malformed"); Assert(malformed.StatusCode == HttpStatusCode.BadRequest, "HTTP malformed JSON rejected");
    }
    finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
}

static async Task<HttpResponseMessage> PostState(HttpClient client, string key, string etag, string json)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/state/{key}") { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
    request.Headers.TryAddWithoutValidation("If-Match", etag);
    return await client.SendAsync(request);
}

static async Task RunResultAcceptanceAsync(int port, int competitionId, string key, string signingKey, StackMeetDbContext db)
{
    var actor = new StackMeet.Api.Models.AppUser
    {
        Email = "eng01@example.test", NormalizedEmail = "ENG01@EXAMPLE.TEST",
        DisplayName = "Synthetic actor", IsSystemAdmin = true, CreatedAt = DateTime.UtcNow
    };
    db.Add(actor);
    await db.SaveChangesAsync();
    var config = new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Security:SessionSigningKey"] = signingKey }).Build();
    var tokens = new SessionTokenService(config);
    using var a = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
    using var b = new HttpClient { BaseAddress = a.BaseAddress };
    using var admin = new HttpClient { BaseAddress = a.BaseAddress };
    var bearer = tokens.CreateForUser(actor.Id, actor.Email, actor.DisplayName, true, actor.SessionVersion);
    a.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{bearer.Payload}.{bearer.Signature}");
    b.DefaultRequestHeaders.Authorization = a.DefaultRequestHeaders.Authorization;
    admin.DefaultRequestHeaders.Add("X-StackMeet-Admin-Key", "synthetic-eng01-admin");
    var route = $"/api/competitions/{competitionId}/results";
    ResultUpsertRequest Upsert(long? revision, decimal seconds, string code = "A1") =>
        new("Prelims", "Individual", code, "Cycle", [seconds], 0, revision);
    ResultDeleteRequest Delete(long? revision) => new("Prelims", "Individual", "A1", "Cycle", revision);
    async Task<HttpStatusCode> Send(HttpClient client, ResultBatchRequest batch)
    {
        using var response = await client.PostAsJsonAsync($"{route}/batch", batch);
        return response.StatusCode;
    }
    async Task<CompetitionResultsResponse> Read() =>
        await a.GetFromJsonAsync<CompetitionResultsResponse>(route) ?? throw new InvalidOperationException("Missing results");

    Assert(await Send(admin, new([Upsert(0, 5)], [], 0)) == HttpStatusCode.Unauthorized, "ENG01 result write requires account actor");
    using (var legacyCreate = await a.PostAsJsonAsync($"{route}/batch", new
    {
        upserts = new[] { new { stage = "Prelims", type = "Individual", participant = "A1", @event = "Cycle", attempts = new[] { 5 }, penalty = 0 } },
        deletes = Array.Empty<object>()
    }))
    {
        Assert(legacyCreate.StatusCode == HttpStatusCode.PreconditionRequired &&
            (await legacyCreate.Content.ReadAsStringAsync()).Contains("result_precondition_required"),
            "ENG01R1 cached client omitting revision rejected with structured refresh requirement");
    }
    Assert(await Send(a, new([Upsert(0, 5)], [])) == HttpStatusCode.PreconditionRequired, "ENG01 explicit create requires dataset revision");
    var creates = await Task.WhenAll(Send(a, new([Upsert(0, 5)], [], 0)), Send(b, new([Upsert(0, 6)], [], 0)));
    Assert(creates.Count(x => x == HttpStatusCode.OK) == 1 && creates.Count(x => x == HttpStatusCode.Conflict) == 1,
        "ENG01 competing creates commit once");
    Assert(await Send(a, new([Upsert(null, 9)], [])) == HttpStatusCode.PreconditionRequired, "ENG01 blind overwrite rejected");
    Assert(await Send(a, new([], [Delete(null)])) == HttpStatusCode.PreconditionRequired, "ENG01 blind delete rejected");
    var revision = (await Read()).Results.Single().Revision;
    var updates = await Task.WhenAll(Send(a, new([Upsert(revision, 7)], [])), Send(b, new([Upsert(revision, 8)], [])));
    Assert(updates.Count(x => x == HttpStatusCode.OK) == 1 && updates.Count(x => x == HttpStatusCode.Conflict) == 1,
        "ENG01 competing edits commit once");
    var after = await Read();
    Assert(after.Revision == revision + 1 && after.Results.Count == 1, "ENG01 only one revision advances");
    Assert(await db.CompetitionResults.AsNoTracking().AnyAsync(x => x.CompetitionId == competitionId && x.UpdatedByUserId == actor.Id),
        "ENG01 result records authenticated account");
    Assert(await Send(a, new([Upsert(0, 11, "A2"), Upsert(revision, 12)], [], after.Revision)) == HttpStatusCode.Conflict,
        "ENG01 stale member rejects whole batch");
    Assert((await Read()).Results.Count == 1 && (await Read()).Revision == after.Revision, "ENG01 failed batch persists nothing");
    Assert(await Send(a, new([], [Delete(after.Revision)])) == HttpStatusCode.OK, "ENG01 versioned delete");
    Assert(await Send(b, new([Upsert(after.Revision, 12)], [])) == HttpStatusCode.Conflict, "ENG01 stale edit cannot recreate deleted row");
    Assert(await Send(b, new([Upsert(0, 12)], [], 0)) == HttpStatusCode.Conflict, "ENG01 stale create cannot recreate deleted row");
    Assert(await Send(a, new([Upsert(null, 10)], [])) == HttpStatusCode.PreconditionRequired, "ENG01R1 null create rejected after delete");
    Assert(await Send(b, new([], [Delete(after.Revision)])) == HttpStatusCode.Conflict, "ENG01R1 stale delete rejects missing row");
    Assert(await Send(a, new([Upsert(0, 10)], [], (await Read()).Revision)) == HttpStatusCode.OK, "ENG01R1 reviewed create can reuse a deleted key");
    Assert(await Send(b, new([], [Delete(after.Revision)])) == HttpStatusCode.Conflict, "ENG01 stale delete cannot remove recreated row");
    Assert(await Send(a, new([], [Delete((await Read()).Revision)])) == HttpStatusCode.OK, "ENG01 clean result fixture");

    a.DefaultRequestHeaders.Add("X-StackMeet-Updated-By", "forged actor");
    Assert((await PostState(a, key, "\"1\"", "{\"seed\":true}")).StatusCode == HttpStatusCode.NoContent, "ENG01 account state save");
    Assert(await db.CompetitionStates.AsNoTracking().AnyAsync(x => x.CompetitionKey == key && x.UpdatedBy == $"account:{actor.Id}"),
        "ENG01 state attribution ignores caller label");

    var stackerId = await db.Stackers.Where(x => x.CompetitionId == competitionId && x.StackerCode == "A4").Select(x => x.Id).SingleAsync();
    var deleteStacker = b.DeleteAsync($"/api/competitions/{competitionId}/stackers/{stackerId}");
    var saveReference = PostState(a, key, "\"2\"", "{\"doubles\":[{\"id\":\"2.9\",\"one\":\"A1\",\"two\":\"A4\"}]}");
    await Task.WhenAll(deleteStacker, saveReference);
    Assert((deleteStacker.Result.StatusCode == HttpStatusCode.NoContent && saveReference.Result.StatusCode == HttpStatusCode.BadRequest)
        || (deleteStacker.Result.StatusCode == HttpStatusCode.Conflict && saveReference.Result.StatusCode == HttpStatusCode.NoContent),
        "ENG01 participant removal and state reference serialize");
    if (deleteStacker.Result.StatusCode == HttpStatusCode.NoContent)
    {
        db.Stackers.Add(new StackMeet.Api.Models.Stacker { CompetitionId = competitionId, StackerCode = "A4", FirstName = "A", LastName = "Four", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }
    var current = (await a.GetAsync($"/api/state/{key}")).Headers.ETag!.Tag!;
    var close = admin.PostAsJsonAsync($"/api/admin/competitions/{key}/status", new { status = "Closed" });
    var save = PostState(a, key, current, "{\"seed\":true}");
    await Task.WhenAll(close, save);
    Assert(close.Result.StatusCode == HttpStatusCode.NoContent && save.Result.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Conflict,
        "ENG01 close and save yield legal serial outcomes");
    current = (await a.GetAsync($"/api/state/{key}")).Headers.ETag!.Tag!;
    using var maintenance = new HttpClient { BaseAddress = a.BaseAddress };
    maintenance.DefaultRequestHeaders.Add("X-StackMeet-Api-Key", "phase-e-http-test-key");
    Assert((await PostState(maintenance, key, current, "{}")).StatusCode == HttpStatusCode.Conflict, "ENG01 locked lifecycle rejects closed maintenance state save");
    Assert(await Send(a, new([Upsert(null, 5)], [])) == HttpStatusCode.Conflict, "ENG01 closed result batch rejected");
    Assert((await admin.PostAsJsonAsync($"/api/admin/competitions/{key}/status", new { status = "Active" })).StatusCode == HttpStatusCode.NoContent,
        "ENG01 fixture reopened");
    Assert((await PostState(maintenance, key, current, "{\"seed\":true}")).StatusCode == HttpStatusCode.NoContent
        && await db.CompetitionStates.AsNoTracking().AnyAsync(x => x.CompetitionKey == key && x.UpdatedBy == "maintenance:api-key"),
        "ENG01 maintenance state attribution");

    // Exercise the actual middleware and logout-all endpoint, not only token parsing.
    async Task<HttpStatusCode> ReadStatus(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        return response.StatusCode;
    }
    Assert(await ReadStatus(a, route) == HttpStatusCode.OK
        && await ReadStatus(a, "/api/admin/competitions") == HttpStatusCode.OK, "ENG02B current account token accepted by both API gates");

    // Actual SignalR JSON protocol over an isolated loopback WebSocket.
    using var hubTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    using var socket = new System.Net.WebSockets.ClientWebSocket();
    await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/hubs/results"), hubTimeout.Token);
    var hubBuffer = new byte[8192];
    var hubPending = new List<byte>();
    var hubFrames = new Queue<string>();
    async Task SendHub(string json)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json + "\u001e");
        await socket.SendAsync(new ArraySegment<byte>(bytes), System.Net.WebSockets.WebSocketMessageType.Text, true, hubTimeout.Token);
    }
    async Task<string> ReceiveHub()
    {
        while (hubFrames.Count == 0)
        {
            var frame = await socket.ReceiveAsync(new ArraySegment<byte>(hubBuffer), hubTimeout.Token);
            if (frame.MessageType != System.Net.WebSockets.WebSocketMessageType.Text)
                throw new InvalidOperationException("Expected SignalR text frame.");
            for (var index = 0; index < frame.Count; index++)
            {
                if (hubBuffer[index] == 0x1e)
                {
                    hubFrames.Enqueue(System.Text.Encoding.UTF8.GetString(hubPending.ToArray()));
                    hubPending.Clear();
                }
                else hubPending.Add(hubBuffer[index]);
            }
        }
        return hubFrames.Dequeue();
    }
    await SendHub("{\"protocol\":\"json\",\"version\":1}");
    using (var handshake = System.Text.Json.JsonDocument.Parse(await ReceiveHub()))
        Assert(!handshake.RootElement.TryGetProperty("error", out _), "ENG02C1 hub handshake");
    var invocation = 0;
    var deliveredChanges = 0;
    async Task<bool> JoinHub(string target, params string[] arguments)
    {
        var id = (++invocation).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await SendHub(System.Text.Json.JsonSerializer.Serialize(new { type = 1, invocationId = id, target, arguments }));
        while (true)
        {
            using var completion = System.Text.Json.JsonDocument.Parse(await ReceiveHub());
            var root = completion.RootElement;
            var messageType = root.GetProperty("type").GetInt32();
            if (messageType == 1 && root.TryGetProperty("target", out var eventTarget)
                && eventTarget.GetString() is "CompetitionChanged" or "ResultsChanged")
                deliveredChanges++;
            if (messageType == 3 && root.GetProperty("invocationId").GetString() == id)
                return !root.TryGetProperty("error", out _);
        }
    }
    Assert(!await JoinHub("JoinCompetition", key), "ENG02C1 anonymous unlisted subscription rejected");
    Assert(!await JoinHub("JoinCompetition", "UNKNOWN-COMPETITION"), "ENG02C1 unknown competition rejected");
    Assert(!await JoinHub("JoinOperatorCompetition", key, "invalid-token"), "ENG02C1 invalid operator token rejected");
    Assert(await JoinHub("JoinOperatorCompetition", key, bearer.ToString()), "ENG02C1 current operator accepted");
    var legacyHubToken = tokens.Create(key, "Synthetic legacy operator").ToString();
    Assert(await JoinHub("JoinOperatorCompetition", key, legacyHubToken), "ENG02C1 scoped legacy operator accepted");
    Assert(!await JoinHub("JoinOperatorCompetition", key, tokens.Create("OTHER-COMPETITION", "Synthetic").ToString()), "ENG02C1 wrong legacy scope rejected");
    var hubCompetition = await db.Competitions.SingleAsync(item => item.Id == competitionId);
    var unassigned = new StackMeet.Api.Models.AppUser
    {
        Email = "eng02c1@example.test", NormalizedEmail = "ENG02C1@EXAMPLE.TEST",
        DisplayName = "Synthetic viewer", CreatedAt = DateTime.UtcNow
    };
    db.AppUsers.Add(unassigned);
    await db.SaveChangesAsync();
    var unassignedToken = tokens.CreateForUser(unassigned.Id, unassigned.Email, unassigned.DisplayName, false, unassigned.SessionVersion).ToString();
    Assert(!await JoinHub("JoinOperatorCompetition", key, unassignedToken), "ENG02C1 unassigned account rejected");
    var viewerAssignment = new StackMeet.Api.Models.CompetitionUser
    {
        CompetitionId = competitionId, UserId = unassigned.Id, RoleId = 4,
        IsActive = true, AssignedAt = DateTime.UtcNow
    };
    db.CompetitionUsers.Add(viewerAssignment);
    await db.SaveChangesAsync();
    Assert(await JoinHub("JoinOperatorCompetition", key, unassignedToken), "ENG02C1 assigned viewer accepted");

    async Task TriggerHubChange()
    {
        using var snapshot = await maintenance.GetAsync($"/api/state/{key}");
        var etag = snapshot.Headers.ETag?.Tag ?? throw new InvalidOperationException("Expected state ETag.");
        using var response = await PostState(maintenance, key, etag, "{\"seed\":true}");
        Assert(response.StatusCode == HttpStatusCode.NoContent, "ENG02C2 synthetic notification-triggering save");
    }
    var deliveredBefore = deliveredChanges;
    await TriggerHubChange();
    Assert(await JoinHub("JoinOperatorCompetition", key, unassignedToken), "ENG02C2 viewer subscription barrier");
    Assert(deliveredChanges == deliveredBefore + 1, "ENG02C2 assigned viewer receives committed change");
    viewerAssignment.IsActive = false;
    await db.SaveChangesAsync();
    deliveredBefore = deliveredChanges;
    await TriggerHubChange();
    Assert(!await JoinHub("JoinOperatorCompetition", key, unassignedToken), "ENG02C2 removed assignment cannot rejoin");
    Assert(deliveredChanges == deliveredBefore, "ENG02C2 already-connected removed viewer receives no change");

    hubCompetition.IsPubliclyListed = true;
    await db.SaveChangesAsync();
    Assert(await JoinHub("JoinCompetition", hubCompetition.CompetitionCode), "ENG02C1 listed public code resolves canonical subscription");
    deliveredBefore = deliveredChanges;
    await TriggerHubChange();
    Assert(await JoinHub("JoinCompetition", key), "ENG02C2 public subscription barrier");
    Assert(deliveredChanges == deliveredBefore + 1, "ENG02C2 listed viewer receives committed change");
    hubCompetition.IsPubliclyListed = false;
    await db.SaveChangesAsync();
    deliveredBefore = deliveredChanges;
    await TriggerHubChange();
    Assert(!await JoinHub("JoinCompetition", key), "ENG02C1 unlisted competition cannot be rejoined");
    Assert(deliveredChanges == deliveredBefore, "ENG02C2 already-connected unlisted viewer receives no change");
    Assert(await JoinHub("JoinOperatorCompetition", key, bearer.ToString()), "ENG02C1 operator can rejoin unlisted competition");
    using (var logout = await a.PostAsync("/api/auth/logout-all", null))
        Assert(logout.StatusCode == HttpStatusCode.NoContent, "ENG02B logout-all succeeds");
    Assert(await ReadStatus(a, route) == HttpStatusCode.Unauthorized
        && await ReadStatus(b, "/api/admin/competitions") == HttpStatusCode.Unauthorized, "ENG02B old token rejected by both API gates after logout-all");
    using (var mixed = new HttpClient { BaseAddress = a.BaseAddress })
    {
        mixed.DefaultRequestHeaders.Add("X-StackMeet-Api-Key", "phase-e-http-test-key");
        mixed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer.ToString());
        Assert(await ReadStatus(mixed, "/api/auth/me") == HttpStatusCode.Unauthorized,
            "ENG02E maintenance key cannot make a revoked bearer current");
    }
    deliveredBefore = deliveredChanges;
    await TriggerHubChange();
    Assert(!await JoinHub("JoinOperatorCompetition", key, bearer.ToString()), "ENG02C1 revoked account token cannot rejoin");
    Assert(deliveredChanges == deliveredBefore, "ENG02C2 already-connected revoked account receives no change");
    socket.Abort();
    await db.Entry(actor).ReloadAsync();
    var renewed = tokens.CreateForUser(actor.Id, actor.Email, actor.DisplayName, true, actor.SessionVersion);
    a.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", renewed.ToString());
    Assert(await ReadStatus(a, route) == HttpStatusCode.OK, "ENG02B current session version accepted");
    actor.IsActive = false;
    await db.SaveChangesAsync();
    Assert(await ReadStatus(a, route) == HttpStatusCode.Unauthorized
        && await ReadStatus(a, "/api/admin/competitions") == HttpStatusCode.Unauthorized, "ENG02B deactivated account session rejected");
    // Exercise role and authority boundaries using real requests without changing existing scoring.
    using var roleClient = new HttpClient { BaseAddress = a.BaseAddress };
    roleClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", unassignedToken);
    var stackerRoute = $"/api/competitions/{competitionId}/stackers";
    var assetRoute = $"/api/competitions/{competitionId}/assets";
    var stateRoute = $"/api/state/{key}";
    Assert(await ReadStatus(roleClient, stateRoute) == HttpStatusCode.Forbidden
        && await ReadStatus(roleClient, route) == HttpStatusCode.Forbidden,
        "ENG02E unassigned account cannot access state or results");
    viewerAssignment.IsActive = true;
    foreach (var roleId in new[] { 4, 3, 2 })
    {
        viewerAssignment.RoleId = roleId;
        await db.SaveChangesAsync();
        foreach (var readRoute in new[] { stateRoute, route, stackerRoute, assetRoute })
            Assert(await ReadStatus(roleClient, readRoute) == HttpStatusCode.OK, $"ENG02E role {roleId} can read assigned resource");
        using (var stateWrite = await roleClient.PostAsync(stateRoute, new StringContent("{}")))
            Assert(stateWrite.StatusCode == (roleId == 2 ? HttpStatusCode.PreconditionRequired : HttpStatusCode.Forbidden),
                $"ENG02E role {roleId} state management gate");
        using (var batch = await roleClient.PostAsJsonAsync(route + "/batch", new { upserts = Array.Empty<object>(), deletes = Array.Empty<object>() }))
            Assert(batch.StatusCode == (roleId == 4 ? HttpStatusCode.Forbidden : HttpStatusCode.BadRequest),
                $"ENG02E role {roleId} result entry gate");
        using (var assetDelete = await roleClient.DeleteAsync(assetRoute + "/logo"))
            Assert(assetDelete.StatusCode == (roleId == 2 ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden),
                $"ENG02E role {roleId} asset management gate");
        Assert(await ReadStatus(roleClient, "/api/admin/competitions") == HttpStatusCode.Forbidden,
            $"ENG02E role {roleId} cannot administer system");
    }
    var unrelated = await db.Competitions.AsNoTracking().FirstAsync(item => item.Id != competitionId && item.Status == "Active");
    Assert(await ReadStatus(roleClient, $"/api/state/{unrelated.CompetitionKey}") == HttpStatusCode.Forbidden
        && await ReadStatus(roleClient, $"/api/competitions/{unrelated.Id}/stackers") == HttpStatusCode.Forbidden,
        "ENG02E account cannot read another competition");
    using var legacyClient = new HttpClient { BaseAddress = a.BaseAddress };
    legacyClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", legacyHubToken);
    Assert(await ReadStatus(legacyClient, stateRoute) == HttpStatusCode.OK
        && await ReadStatus(legacyClient, stackerRoute) == HttpStatusCode.OK
        && await ReadStatus(legacyClient, route) == HttpStatusCode.Unauthorized,
        "ENG02E legacy state and roster access retained, account-only results protected");
    Assert(await ReadStatus(legacyClient, $"/api/state/{unrelated.CompetitionKey}") == HttpStatusCode.Forbidden
        && await ReadStatus(legacyClient, $"/api/competitions/{unrelated.Id}/stackers") == HttpStatusCode.Forbidden,
        "ENG02E legacy scope enforced across resource routes");
    Assert(await ReadStatus(maintenance, stateRoute) == HttpStatusCode.OK
        && await ReadStatus(maintenance, stackerRoute) == HttpStatusCode.OK
        && await ReadStatus(maintenance, route) == HttpStatusCode.Unauthorized
        && await ReadStatus(maintenance, "/api/admin/competitions") == HttpStatusCode.Unauthorized,
        "ENG02E maintenance scope remains distinct from account and admin authority");
    Assert(await ReadStatus(admin, stateRoute) == HttpStatusCode.Unauthorized,
        "ENG02E admin key alone is not an operator or maintenance credential");

    using var browser = new HttpClient { BaseAddress = a.BaseAddress };
    foreach (var page in new[] { "/index.html", "/admin.html", "/account.html", "/all/index.html",
        $"/{key}/Results", "/Stackers/NDT-7K4M2PX" })
    {
        using var response = await browser.GetAsync(page);
        Assert(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true,
            $"ENG02F browser shell no-store {page}");
        Assert(response.Headers.TryGetValues("Content-Security-Policy", out var policies)
            && policies.Any(policy => policy.Contains("script-src 'self'") && !policy.Contains("unsafe-inline")
                && !policy.Contains("unsafe-eval") && policy.Contains("object-src 'none'")
                && policy.Contains("base-uri 'none'") && policy.Contains("frame-ancestors 'none'")
                && policy.Contains("form-action 'self'")),
            $"ENG02F enforced script and embedding policy {page}");
        Assert(response.Headers.Contains("Content-Security-Policy-Report-Only"),
            $"ENG02F staged resource policy {page}");
    }
    using (var privateResponse = await roleClient.GetAsync(stateRoute))
        Assert(privateResponse.StatusCode == HttpStatusCode.OK && privateResponse.Headers.CacheControl?.NoStore == true,
            "ENG02F authorized state is not cacheable");
    using (var deniedResponse = await browser.GetAsync(stateRoute))
        Assert(deniedResponse.StatusCode == HttpStatusCode.Unauthorized && deniedResponse.Headers.CacheControl?.NoStore == true,
            "ENG02F unauthorized API responses are not cacheable");
    using (var scriptResponse = await browser.GetAsync("/app.js"))
        Assert(scriptResponse.StatusCode == HttpStatusCode.OK && !scriptResponse.Headers.Contains("Content-Security-Policy"),
            "ENG02F static script loading remains available");

    // The reviewed manifest is also checked against controller attributes by the Node suite.
    // All probes are confined to the existing isolated loopback host and synthetic database.
    using var endpointMatrix = System.Text.Json.JsonDocument.Parse(
        await File.ReadAllTextAsync("tests/security-endpoint-matrix.json"));
    async Task<HttpStatusCode> Probe(HttpClient client, string verb, string target, object? payload = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(verb), target);
        if (verb is "POST" or "PUT" or "PATCH") request.Content = JsonContent.Create(payload ?? new { });
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }
    var anonymousChecks = 0;
    var adminChecks = 0;
    foreach (var endpoint in endpointMatrix.RootElement.EnumerateArray())
    {
        var authority = endpoint.GetProperty("authority").GetString()!;
        if (authority.StartsWith("public-", StringComparison.Ordinal)) continue;
        var verb = endpoint.GetProperty("method").GetString()!;
        var template = endpoint.GetProperty("route").GetString()!;
        var target = System.Text.RegularExpressions.Regex.Replace(template, @"\{([^}:]+)(?::[^}]+)?\}", match =>
            match.Groups[1].Value switch
            {
                "competitionKey" => key,
                "competitionId" => competitionId.ToString(),
                "nadiTrackId" => "NDT-7K4M2PX",
                "assetType" => "logo",
                "id" or "stackerId" or "accessId" or "linkId" => "2147483647",
                _ => throw new InvalidOperationException("Unreviewed route placeholder: " + match.Value)
            });
        Assert(await Probe(browser, verb, target) == HttpStatusCode.Unauthorized,
            $"ENG02G anonymous denied {verb} {template}");
        anonymousChecks++;
        if (authority == "admin")
        {
            Assert(await Probe(roleClient, verb, target) == HttpStatusCode.Forbidden,
                $"ENG02G non-admin account denied {verb} {template}");
            Assert(await Probe(maintenance, verb, target) == HttpStatusCode.Unauthorized,
                $"ENG02G maintenance key denied {verb} {template}");
            Assert(await Probe(legacyClient, verb, target) == HttpStatusCode.Unauthorized,
                $"ENG02G legacy token denied {verb} {template}");
            adminChecks++;
        }
        else if (authority == "maintenance")
        {
            // Account has manager permission for its own competition but is not maintenance.
            // Replace the ID placeholder for these probes with the assigned competition.
            var assignedTarget = target.Replace("/2147483647", $"/{competitionId}");
            // [ApiController] validates request DTOs before invoking the controller.
            // Use complete synthetic payloads here so a 400 cannot mask a missing maintenance gate.
            var fixtureDate = DateOnly.FromDateTime(DateTime.UtcNow);
            object? payload = verb switch
            {
                "POST" => new CompetitionRequest(key + "MNT", "Synthetic maintenance probe", "LocalDB",
                    fixtureDate, fixtureDate, "Active", false),
                "PUT" when template.EndsWith("/activity", StringComparison.Ordinal) =>
                    new CompetitionActivityAssignmentRequest("sport-stacking"),
                "PUT" => new CompetitionRequest(key + "MNT", "Synthetic maintenance probe", "LocalDB",
                    fixtureDate, fixtureDate, "Active", false),
                _ => null
            };
            var denied = await Probe(roleClient, verb, assignedTarget, payload);
            Assert(denied == HttpStatusCode.Forbidden,
                $"ENG02G competition manager denied maintenance operation {verb} {template} (actual {(int)denied})");
        }
    }
    Assert(anonymousChecks == 59 && adminChecks == 36,
        "ENG02G complete protected-route negative probe inventory");


}

static async Task RunPublicationAcceptanceAsync(int port, string key, StackMeetDbContext db)
{
    var now = DateTime.UtcNow;
    var date = DateOnly.FromDateTime(now);
    var competition = new StackMeet.Api.Models.Competition
    {
        CompetitionKey = key, CompetitionCode = key + "CODE",
        CompetitionName = "Synthetic publication", Venue = "LocalDB", StartDate = date, EndDate = date,
        Status = "Active", CreatedAt = now, UpdatedAt = now, IsPubliclyListed = false
    };
    db.Competitions.Add(competition);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(new StackMeet.Api.Models.CompetitionState
    {
        CompetitionKey = key, JsonData = "{\"settings\":{\"password\":\"synthetic-private-setting\"}}",
        SchemaVersion = "0.9-online", StateRevision = 1, CreatedAt = now, UpdatedAt = now
    });
    var stacker = new StackMeet.Api.Models.Stacker
    {
        CompetitionId = competition.Id, StackerCode = "PUB1", FirstName = "Synthetic", LastName = "Publication",
        Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", BirthDate = new DateOnly(2012, 4, 17),
        Email = "synthetic-private@example.test", Phone = "0123456789", WssaId = "synthetic-private-wssa",
        CreatedAt = now, UpdatedAt = now
    };
    var identity = new StackMeet.Api.Activities.SportStacking.Identity.SportStackerIdentity
    {
        NadiTrackId = "NDT-7K4M2PX", FirstName = "Synthetic", LastName = "Publication", Gender = "M", Country = "MY",
        IsPublicProfile = true, CreatedAt = now, UpdatedAt = now
    };
    db.AddRange(stacker, identity);
    await db.SaveChangesAsync();
    db.StackerIdentityLinks.Add(new StackMeet.Api.Activities.SportStacking.Identity.StackerIdentityLink
    {
        StackerId = stacker.Id, SportStackerIdentityId = identity.Id, MatchMethod = "Synthetic",
        ResolutionReasonCode = "SYNTHETIC_TEST", LinkedAt = now
    });
    await db.SaveChangesAsync();

    using var viewer = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
    using var admin = new HttpClient { BaseAddress = viewer.BaseAddress };
    admin.DefaultRequestHeaders.Add("X-StackMeet-Admin-Key", "synthetic-eng01-admin");
    using var maintenance = new HttpClient { BaseAddress = viewer.BaseAddress };
    maintenance.DefaultRequestHeaders.Add("X-StackMeet-Api-Key", "phase-e-http-test-key");
    string Results(string alias) => $"/api/public/competitions/{alias}/results";
    string Links(string alias) => $"/api/public/competitions/{alias}/profile-links";
    async Task<HttpStatusCode> Status(string path)
    {
        using var response = await viewer.GetAsync(path);
        return response.StatusCode;
    }
    async Task<int> LinkCount(string alias)
    {
        using var response = await viewer.GetAsync(Links(alias));
        Assert(response.StatusCode == HttpStatusCode.OK, "ENG02D profile-link response");
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("links").GetArrayLength();
    }
    using (var response = await viewer.GetAsync(Results(key)))
    {
        Assert(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true,
            "ENG02D unlisted direct results preserved with no-store");
        var body = await response.Content.ReadAsStringAsync();
        Assert(body.Contains("Synthetic Publication") && !body.Contains("synthetic-private")
            && !body.Contains("0123456789") && !body.Contains("2012-04-17"),
            "ENG02D results omit private contact, date of birth, external identity and settings");
        using var json = System.Text.Json.JsonDocument.Parse(body);
        var fields = json.RootElement.GetProperty("stackers")[0].EnumerateObject().Select(item => item.Name).ToArray();
        Assert(fields.OrderBy(x => x).SequenceEqual(new[] { "country", "division", "gender", "id", "name", "org", "region", "special" }.OrderBy(x => x)),
            "ENG02D public participant field allowlist");
    }
    Assert(await Status(Results(competition.CompetitionCode)) == HttpStatusCode.OK, "ENG02D public code alias preserved");
    Assert(await LinkCount(key) == 0, "ENG02D unlisted competition does not expose permanent identity bridge");
    using (var directory = await viewer.GetAsync("/api/public/competitions"))
        Assert(directory.StatusCode == HttpStatusCode.OK && directory.Headers.CacheControl?.NoStore == true
            && !(await directory.Content.ReadAsStringAsync()).Contains(competition.CompetitionCode),
            "ENG02D unlisted competition absent from uncached directory");
    competition.IsPubliclyListed = true;
    await db.SaveChangesAsync();
    Assert(await LinkCount(key) == 1, "ENG02D listed competition public identity bridge available");
    identity.IsPublicProfile = false;
    await db.SaveChangesAsync();
    Assert(await LinkCount(key) == 0, "ENG02D private identity bridge suppressed");
    Assert(await Status("/api/public/stackers/" + identity.NadiTrackId) == HttpStatusCode.NotFound, "ENG02D private profile is not found");
    identity.IsPublicProfile = true;
    await db.SaveChangesAsync();

    // Existing cross-column collisions may predate this patch. Even if one is archived,
    // never arbitrarily resolve a shared public alias to the remaining event.
    var collision = new StackMeet.Api.Models.Competition
    {
        CompetitionKey = competition.CompetitionCode, CompetitionCode = key + "OTHER",
        CompetitionName = "Synthetic legacy collision", Venue = "LocalDB", StartDate = date, EndDate = date,
        Status = "Archived", ArchivedAt = now, CreatedAt = now, UpdatedAt = now
    };
    db.Add(collision);
    await db.SaveChangesAsync();
    Assert(await Status(Results(competition.CompetitionCode)) == HttpStatusCode.NotFound
        && await Status(Links(competition.CompetitionCode)) == HttpStatusCode.NotFound,
        "ENG02D ambiguous public code/key fails closed");
    Assert(await Status(Results(key)) == HttpStatusCode.OK, "ENG02D unique alias unaffected by legacy collision");
    using (var create = await admin.PostAsJsonAsync("/api/admin/competitions", new
    {
        competitionKey = key + "NEW", competitionCode = key, competitionName = "Synthetic collision",
        venue = "LocalDB", startDate = date, endDate = date, status = "Active", password = "Synthetic-only-password"
    }))
        Assert(create.StatusCode == HttpStatusCode.Conflict, "ENG02D admin create rejects code colliding with existing key");
    using (var update = await maintenance.PutAsJsonAsync($"/api/competitions/{collision.Id}", new
    {
        competitionCode = key, competitionName = "Synthetic collision", venue = "LocalDB",
        startDate = date, endDate = date, status = "Archived"
    }))
        Assert(update.StatusCode == HttpStatusCode.Conflict, "ENG02D maintenance code update rejects existing key");
    competition.Status = "Archived";
    competition.ArchivedAt = now;
    await db.SaveChangesAsync();
    Assert(await Status(Results(key)) == HttpStatusCode.NotFound && await Status(Links(key)) == HttpStatusCode.NotFound,
        "ENG02D archived live results and profile links unavailable");
    Assert(await Status("/api/public/stackers/" + identity.NadiTrackId) == HttpStatusCode.OK,
        "ENG02D public career profile remains available after competition archive");
}

// ENG-04B: characterize the existing authority contract in the disposable LocalDB HTTP harness.
static async Task RunStorageAuthorityAcceptanceAsync(int port, string key, StackMeetDbContext db)
{
    var now = DateTime.UtcNow;
    var day = new DateOnly(2026, 1, 1);
    var competition = new StackMeet.Api.Models.Competition
    {
        CompetitionCode = key, CompetitionKey = key, CompetitionName = "SQL authority fixture",
        Venue = "LocalDB", StartDate = day, EndDate = day, Status = "Active",
        CreatedAt = now, UpdatedAt = now, ResultsRevision = 2
    };
    db.Competitions.Add(competition);
    await db.SaveChangesAsync();
    const string legacyJson = """
    {
      "settings":{"name":"JSON display name","advanceIndividuals":8},
      "stackers":[{"id":"A1","name":"Synthetic One","gender":"F","org":"Independent","country":"MY","region":"","division":"U12","special":"No","email":"private@example.test"}],
      "results":[{"id":"legacy-result","stage":"Finals","type":"Individual","participant":"A1","event":"Cycle","attempts":[1.111],"penalty":0}],
      "doubles":[{"id":"D1","one":"A1","parentName":"External Parent","type":"child_parent"}],
      "relays":[],
      "finalQualificationSnapshots":[{"id":"Q1","status":"Approved","selectedQualifiers":[{"participantId":"A1"}]}]
    }
    """;
    var state = new StackMeet.Api.Models.CompetitionState
    {
        CompetitionKey = key, JsonData = legacyJson, SchemaVersion = "0.9-online",
        StateRevision = 7, CreatedAt = now, UpdatedAt = now
    };
    using var viewer = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) };
    using var maintenance = new HttpClient { BaseAddress = viewer.BaseAddress, Timeout = TimeSpan.FromSeconds(10) };
    maintenance.DefaultRequestHeaders.Add("X-StackMeet-Api-Key", "phase-e-http-test-key");
    string route = $"/api/public/competitions/{key}/results";
    async Task<System.Text.Json.JsonElement> ReadPublic()
    {
        using var response = await viewer.GetAsync(route);
        Assert(response.StatusCode == HttpStatusCode.OK, "ENG04B public fixture read");
        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
    string[] Roster(System.Text.Json.JsonElement root) => root.GetProperty("stackers").EnumerateArray()
        .Select(row => row.GetProperty("id").GetString() ?? "").ToArray();
    using (var missing = await viewer.GetAsync(route))
        Assert(missing.StatusCode == HttpStatusCode.NotFound, "ENG04B missing saved state is not a public competition projection");
    db.CompetitionStates.Add(state);
    await db.SaveChangesAsync();
    var legacy = await ReadPublic();
    Assert(Roster(legacy).SequenceEqual(new[] { "A1" }), "ENG04B legacy-only public roster fallback");
    Assert(legacy.GetProperty("results").GetArrayLength() == 0, "ENG04B legacy-only results never become SQL results");
    Assert(!legacy.GetRawText().Contains("private@example.test"), "ENG04B legacy roster private field excluded");
    Assert(legacy.GetProperty("competition").GetProperty("name").GetString() == "SQL authority fixture"
        && legacy.GetProperty("settings").GetProperty("name").GetString() == "JSON display name",
        "ENG04B SQL competition and JSON display settings remain distinct");

    var stacker = new StackMeet.Api.Models.Stacker
    {
        CompetitionId = competition.Id, StackerCode = "A1", FirstName = "Synthetic", LastName = "One",
        Gender = "F", Country = "MY", CustomDivision = "U12", Paid = "No", CheckedIn = "No",
        CreatedAt = now, UpdatedAt = now, Email = "private@example.test"
    };
    db.Stackers.Add(stacker);
    await db.SaveChangesAsync();
    var matched = await ReadPublic();
    var before = legacy.GetProperty("stackers")[0];
    var after = matched.GetProperty("stackers")[0];
    foreach (var field in new[] { "id", "name", "gender", "org", "country", "region", "division", "special" })
        Assert(before.GetProperty(field).GetString() == after.GetProperty(field).GetString(), $"ENG04B equivalent SQL/JSON public field {field}");

    // Deliberately seed inconsistent legacy data directly in the disposable fixture, not through production APIs.
    var mixedJson = legacyJson.Replace("Synthetic One", "Stale Legacy Name", StringComparison.Ordinal)
        .Replace("\"stackers\":[", "\"stackers\":[{\"id\":\"OLD\",\"name\":\"Old Only\"},", StringComparison.Ordinal);
    state.JsonData = mixedJson;
    await db.SaveChangesAsync();
    var finals = new StackMeet.Api.Models.CompetitionResult
    {
        CompetitionId = competition.Id, Stage = "Finals", ParticipantType = "Individual", ParticipantCode = "A1",
        EventCode = "Cycle", AttemptsJson = "[5.123]", Penalty = 0, Revision = 2, CreatedAt = now, UpdatedAt = now
    };
    var prelims = new StackMeet.Api.Models.CompetitionResult
    {
        CompetitionId = competition.Id, Stage = "Prelims", ParticipantType = "Individual", ParticipantCode = "A1",
        EventCode = "Cycle", AttemptsJson = "[2.123]", Penalty = 0, Revision = 1, CreatedAt = now, UpdatedAt = now
    };
    db.CompetitionResults.AddRange(finals, prelims);
    await db.SaveChangesAsync();
    var mixed = await ReadPublic();
    Assert(Roster(mixed).SequenceEqual(new[] { "A1" })
        && mixed.GetProperty("stackers")[0].GetProperty("name").GetString() == "Synthetic One",
        "ENG04B nonempty SQL roster wins as a whole, no union or stale-name overwrite");
    var rows = mixed.GetProperty("results").EnumerateArray().ToArray();
    Assert(rows.Length == 2 && rows.Single(x => x.GetProperty("stage").GetString() == "Finals").GetProperty("attempts")[0].GetDecimal() == 5.123m
        && rows.Single(x => x.GetProperty("stage").GetString() == "Prelims").GetProperty("attempts")[0].GetDecimal() == 2.123m,
        "ENG04B SQL attempts win and preliminary reporting remains separately represented");
    Assert(mixed.GetProperty("doubles")[0].GetProperty("id").GetString() == "D1"
        && !mixed.TryGetProperty("finalQualificationSnapshots", out _), "ENG04B public team projection retained; internal qualification payload not exposed");
    using (var internalState = await maintenance.GetAsync($"/api/state/{key}"))
        Assert(internalState.StatusCode == HttpStatusCode.OK && internalState.Headers.ETag?.Tag == "\"7\""
            && await internalState.Content.ReadAsStringAsync() == mixedJson,
            "ENG04B public reads preserve approved qualification and exact saved state");

    // Characterize clean modern state, then restore the intentionally mixed fixture for capture.
    state.JsonData = """{"stackers":[],"results":[],"doubles":[],"relays":[]}""";
    await db.SaveChangesAsync();
    var modern = await ReadPublic();
    Assert(Roster(modern).SequenceEqual(new[] { "A1" }) && modern.GetProperty("results").GetArrayLength() == 2,
        "ENG04B SQL-only roster/results remain available with empty legacy arrays");
    state.JsonData = mixedJson;
    competition.Status = "Closed";
    await db.SaveChangesAsync();
    var governance = new StackMeet.Api.Activities.SportStacking.Ranking.FinalsRankingGovernanceService(db);
    var captured = await governance.CaptureFinalizedSnapshotAsync(competition.Id, null);
    var snapshotJson = captured.SnapshotJson ?? throw new InvalidOperationException("Missing synthetic snapshot JSON.");
    var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(snapshotJson)));
    Assert(captured.HasSnapshot && string.Equals(expectedHash, captured.SnapshotSha256, StringComparison.OrdinalIgnoreCase),
        "ENG04B captured historical snapshot matches SHA-256 of exact UTF-8 bytes");
    using (var snapshot = System.Text.Json.JsonDocument.Parse(snapshotJson))
    {
        var savedFinals = snapshot.RootElement.GetProperty("finalsResults");
        Assert(savedFinals.GetArrayLength() == 1 && savedFinals[0].GetProperty("attemptsJson").GetString() == "[5.123]",
            "ENG04B historical source captures SQL Finals only, not faster preliminary or legacy attempts");
    }
    await ReadPublic();
    var reread = await governance.GetAsync(competition.Id);
    Assert(reread?.SnapshotJson == snapshotJson && reread?.SnapshotSha256 == captured.SnapshotSha256,
        "ENG04B public reads cannot alter immutable historical evidence");
    bool rejected = false;
    try { await governance.CaptureFinalizedSnapshotAsync(competition.Id, null); }
    catch (InvalidOperationException ex) when (ex.Message.Contains("immutable", StringComparison.OrdinalIgnoreCase)) { rejected = true; }
    Assert(rejected, "ENG04B historical recapture rejected");

    // Separate synthetic competition for deletion/empty-roster characterization; never modify the captured fixture.
    var emptyKey = key + "EMPTY";
    var empty = new StackMeet.Api.Models.Competition
    {
        CompetitionCode = emptyKey, CompetitionKey = emptyKey, CompetitionName = "Empty roster fixture",
        Venue = "LocalDB", StartDate = day, EndDate = day, Status = "Active", CreatedAt = now, UpdatedAt = now
    };
    db.Competitions.Add(empty);
    await db.SaveChangesAsync();
    var emptyState = new StackMeet.Api.Models.CompetitionState
    {
        CompetitionKey = emptyKey, JsonData = legacyJson, SchemaVersion = "0.9-online", StateRevision = 1,
        CreatedAt = now, UpdatedAt = now
    };
    var lastStacker = new StackMeet.Api.Models.Stacker
    {
        CompetitionId = empty.Id, StackerCode = "B1", FirstName = "Last", LastName = "SQL",
        Gender = "F", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = now, UpdatedAt = now
    };
    db.AddRange(emptyState, lastStacker);
    await db.SaveChangesAsync();
    route = $"/api/public/competitions/{emptyKey}/results";
    Assert(Roster(await ReadPublic()).SequenceEqual(new[] { "B1" }), "ENG04B last SQL row suppresses old JSON roster");
    db.Stackers.Remove(lastStacker);
    await db.SaveChangesAsync();
    var emptySql = await ReadPublic();
    Assert(Roster(emptySql).SequenceEqual(new[] { "A1" }) && emptySql.GetProperty("results").GetArrayLength() == 0,
        "ENG04B known risk: empty SQL roster exposes old JSON fallback, never legacy results");
    emptyState.JsonData = """{"stackers":[],"results":[]}""";
    await db.SaveChangesAsync();
    Assert(Roster(await ReadPublic()).Length == 0, "ENG04B genuinely empty SQL and JSON roster stays empty");
    Console.WriteLine("ENG04B storage authority characterization passed; empty-roster ambiguity remains a reconciliation risk.");
}
