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
}
