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
    var teamState = "{\"doubles\":[{\"id\":\"2.1\",\"one\":\"A1\",\"two\":\"A2\"},{\"id\":\"2.2\",\"one\":\"A1\",\"status\":\"pending\"},{\"id\":\"2.3\",\"type\":\"child_parent\",\"one\":\"A3\",\"parentName\":\"External Parent\"},{\"id\":\"2.4\",\"stackerOneId\":\"A1\",\"stackerTwoId\":\"A2\"}],\"relays\":[{\"id\":\"3.1\",\"members\":[\"A1\",\"A2\",\"A3\",\"A4\"]},{\"id\":\"3.2\",\"members\":[\"A1\",\"A2\",\"A3\"]},{\"id\":\"3.3\",\"one\":\"A1\",\"two\":\"A2\",\"three\":\"A3\",\"four\":\"A4\"}]}";
    Assert(teamIntegrity.TryReadReadyTeams(teamState, out var readyTeams, out var teamStateError) && teamStateError is null, "team state parsed");
    Assert(readyTeams.Doubles.SetEquals(new[] { "2.1", "2.3", "2.4" }), "complete doubles detected including legacy aliases");
    Assert(readyTeams.TimedRelays.SetEquals(new[] { "3.1", "3.3" }), "ready relays detected including legacy member slots");
    Assert(teamIntegrity.ValidateResultUpserts(teamState, [new ResultUpsertRequest("Finals", "Doubles", "2.1", "Cycle", [8.123m], 0, null)]) is null, "valid doubles result team accepted");
    Assert(teamIntegrity.ValidateResultUpserts(teamState, [new ResultUpsertRequest("Finals", "Doubles", "2.2", "Cycle", [8.123m], 0, null)]) is not null, "pending doubles result team rejected");
    Assert(teamIntegrity.ValidateResultUpserts(teamState, [new ResultUpsertRequest("Finals", "Timed Relay", "3.1", "3-6-3", [20.123m], 0, null)]) is null, "valid relay result team accepted");
    Assert(teamIntegrity.ValidateResultUpserts(teamState, [new ResultUpsertRequest("Finals", "Timed Relay", "3.2", "3-6-3", [20.123m], 0, null)]) is not null, "incomplete relay result team rejected");
    Assert(!teamIntegrity.TryReadReadyTeams("{\"doubles\":[{\"id\":\"2.1\",\"two\":\"A2\"},{\"id\":\"2.1\",\"two\":\"A3\"}]}", out _, out var duplicateTeamError) && duplicateTeamError?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true, "duplicate team IDs fail closed");
    Assert(!teamIntegrity.TryReadReadyTeams("{\"relays\":[", out _, out var malformedTeamError) && malformedTeamError is not null, "malformed team state fails closed");
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
    const string accountPassword = "CoreIntegrity-Result-123!";
    db.AppUsers.Add(new StackMeet.Api.Models.AppUser
    {
        Email = "core-integrity@example.test",
        NormalizedEmail = "CORE-INTEGRITY@EXAMPLE.TEST",
        PasswordHash = new PasswordHashService().Hash(accountPassword),
        DisplayName = "Core Integrity Admin",
        IsActive = true,
        EmailConfirmed = true,
        IsSystemAdmin = true,
        CreatedAt = now,
        SessionVersion = 1
    });
    await db.SaveChangesAsync();

    var port = Random.Shared.Next(49152, 59999);
    var startInfo = new ProcessStartInfo("dotnet") { WorkingDirectory = Environment.CurrentDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
    foreach (var argument in new[] { "run", "--project", "backend/StackMeet.Api/StackMeet.Api.csproj", "-c", "Release", "--no-build", "--urls", $"http://127.0.0.1:{port}", "--", $"--ConnectionStrings:StackMeet={connectionString}", "--Security:ApiKey=phase-e-http-test-key" }) startInfo.ArgumentList.Add(argument);
    using var process = new Process { StartInfo = startInfo };
    process.StartInfo.Environment["ConnectionStrings__StackMeet"] = connectionString;
    process.StartInfo.Environment["Security__ApiKey"] = "phase-e-http-test-key";
    process.StartInfo.Environment["Security__SessionSigningKey"] = "phase-e-http-session-signing-key-20260918";
    process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
    process.StartInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
    process.Start();
    try
    {
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.Add("X-StackMeet-Api-Key", "phase-e-http-test-key");
        HttpResponseMessage? ready = null;
        for (var attempt = 0; attempt < 30; attempt++) { try { ready = await client.GetAsync($"/api/state/{key}"); if (ready.StatusCode != HttpStatusCode.ServiceUnavailable) break; } catch { } await Task.Delay(500); }
        if (ready is null || ready.StatusCode != HttpStatusCode.OK)
        {
            var body = ready is null ? "<no response>" : await ready.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"HTTP API did not become ready: {ready?.StatusCode}; body={body}; processExited={process.HasExited}");
        }
        var original = await ready.Content.ReadAsStringAsync(); var originalEtag = ready.Headers.ETag?.Tag ?? throw new InvalidOperationException("Initial ETag missing.");
        var valid = "{\"doubles\":[{\"id\":\"2.1\",\"one\":\"A1\",\"two\":\"A2\",\"parentName\":\"External Parent\"}],\"relays\":[{\"id\":\"3.1\",\"members\":[\"A1\",\"A2\",\"A3\",\"A4\"]}],\"legacy\":{\"one\":\"A1\",\"two\":\"A2\"}}";
        var saved = await PostState(client, key, originalEtag, valid); Assert(saved.StatusCode == HttpStatusCode.NoContent, "HTTP valid state save");
        var after = await client.GetAsync($"/api/state/{key}"); var afterJson = await after.Content.ReadAsStringAsync(); var newEtag = after.Headers.ETag?.Tag ?? throw new InvalidOperationException("Updated ETag missing."); Assert(afterJson == valid && newEtag == "\"2\"", "HTTP revision and ETag increment");
        var invalid = await PostState(client, key, newEtag, "{\"doubles\":[{\"participantCode\":\"MISSING\",\"two\":\"A2\"}]}"); var invalidBody = await invalid.Content.ReadAsStringAsync(); Assert(invalid.StatusCode == HttpStatusCode.BadRequest, $"HTTP missing participant rejected ({(int)invalid.StatusCode}: {invalidBody})"); var unchanged = await client.GetAsync($"/api/state/{key}"); Assert(await unchanged.Content.ReadAsStringAsync() == valid && unchanged.Headers.ETag?.Tag == newEtag, "HTTP rejected state unchanged");
        var wrongCompetition = await PostState(client, key, newEtag, "{\"doubles\":[{\"one\":\"B1\",\"two\":\"A2\"}]}"); Assert(wrongCompetition.StatusCode == HttpStatusCode.BadRequest, "HTTP wrong-competition participant rejected");

        using var accountClient = new HttpClient { BaseAddress = client.BaseAddress, Timeout = TimeSpan.FromSeconds(10) };
        var login = await accountClient.PostAsJsonAsync("/api/auth/login", new { email = "core-integrity@example.test", password = accountPassword });
        var loginBody = await login.Content.ReadAsStringAsync();
        Assert(login.StatusCode == HttpStatusCode.OK, $"HTTP account login for result validation ({(int)login.StatusCode}: {loginBody})");
        using var loginJson = System.Text.Json.JsonDocument.Parse(loginBody);
        var token = loginJson.RootElement.GetProperty("token").GetString() ?? throw new InvalidOperationException("Login token missing.");
        accountClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var invalidTeamResult = await PostResultBatch(accountClient, competition.Id, new
        {
            upserts = new[] { new { stage = "Finals", type = "Doubles", participant = "2.99", @event = "Cycle", attempts = new[] { 8.999m }, penalty = 0m, expectedRevision = (long?)null } },
            deletes = Array.Empty<object>()
        });
        Assert(invalidTeamResult.StatusCode == HttpStatusCode.BadRequest, "HTTP unknown Doubles team result rejected");

        var validTeamResult = await PostResultBatch(accountClient, competition.Id, new
        {
            upserts = new[] { new { stage = "Finals", type = "Doubles", participant = "2.1", @event = "Cycle", attempts = new[] { 8.123m }, penalty = 0m, expectedRevision = (long?)null } },
            deletes = Array.Empty<object>()
        });
        var validTeamResultBody = await validTeamResult.Content.ReadAsStringAsync();
        Assert(validTeamResult.StatusCode == HttpStatusCode.OK, $"HTTP valid Doubles team result accepted ({(int)validTeamResult.StatusCode}: {validTeamResultBody})");
        using var resultJson = System.Text.Json.JsonDocument.Parse(validTeamResultBody);
        var resultRevision = resultJson.RootElement.GetProperty("results")[0].GetProperty("revision").GetInt64();

        var removeReferencedTeam = await PostState(client, key, newEtag, "{\"doubles\":[],\"relays\":[{\"id\":\"3.1\",\"members\":[\"A1\",\"A2\",\"A3\",\"A4\"]}]}");
        Assert(removeReferencedTeam.StatusCode == HttpStatusCode.Conflict, "HTTP state cannot orphan existing doubles result");
        var afterRejectedTeamRemoval = await client.GetAsync($"/api/state/{key}");
        Assert(await afterRejectedTeamRemoval.Content.ReadAsStringAsync() == valid && afterRejectedTeamRemoval.Headers.ETag?.Tag == newEtag, "HTTP rejected team removal leaves state unchanged");

        var deleteTeamResult = await PostResultBatch(accountClient, competition.Id, new
        {
            upserts = Array.Empty<object>(),
            deletes = new[] { new { stage = "Finals", type = "Doubles", participant = "2.1", @event = "Cycle", expectedRevision = (long?)resultRevision } }
        });
        Assert(deleteTeamResult.StatusCode == HttpStatusCode.OK, "HTTP referenced team result can be deleted before team removal");
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

static Task<HttpResponseMessage> PostResultBatch(HttpClient client, int competitionId, object payload) =>
    client.PostAsJsonAsync($"/api/competitions/{competitionId}/results/batch", payload);
