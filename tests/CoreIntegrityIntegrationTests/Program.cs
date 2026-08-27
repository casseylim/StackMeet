using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
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
    db.Stackers.AddRange(new StackMeet.Api.Models.Stacker { CompetitionId = competition.Id, StackerCode = "A1", FirstName = "A", LastName = "One", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = now, UpdatedAt = now }, new StackMeet.Api.Models.Stacker { CompetitionId = competition.Id, StackerCode = "A2", FirstName = "A", LastName = "Two", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = now, UpdatedAt = now }, new StackMeet.Api.Models.Stacker { CompetitionId = competition.Id, StackerCode = "A3", FirstName = "A", LastName = "Three", Gender = "M", Country = "MY", Paid = "No", CheckedIn = "No", CreatedAt = now, UpdatedAt = now });
    db.CompetitionStates.Add(new StackMeet.Api.Models.CompetitionState { CompetitionKey = key, JsonData = "{\"seed\":true}", SchemaVersion = "0.9-online", StateRevision = 1, CreatedAt = now, UpdatedAt = now });
    await db.SaveChangesAsync();

    var port = Random.Shared.Next(49152, 59999);
    var startInfo = new ProcessStartInfo("dotnet") { WorkingDirectory = Environment.CurrentDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
    foreach (var argument in new[] { "run", "--project", "backend/StackMeet.Api/StackMeet.Api.csproj", "-c", "Release", "--no-build", "--urls", $"http://127.0.0.1:{port}", "--", $"--ConnectionStrings:StackMeet={connectionString}", "--Security:ApiKey=phase-e-http-test-key" }) startInfo.ArgumentList.Add(argument);
    using var process = new Process { StartInfo = startInfo };
    process.StartInfo.Environment["ConnectionStrings__StackMeet"] = connectionString;
    process.StartInfo.Environment["Security__ApiKey"] = "phase-e-http-test-key";
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
        var valid = "{\"doubles\":[{\"one\":\"A1\",\"two\":\"A2\",\"parentName\":\"External Parent\"}],\"relays\":[{\"members\":[\"A3\"]}],\"legacy\":{\"one\":\"A1\",\"two\":\"A2\"}}";
        var saved = await PostState(client, key, originalEtag, valid); Assert(saved.StatusCode == HttpStatusCode.NoContent, "HTTP valid state save");
        var after = await client.GetAsync($"/api/state/{key}"); var afterJson = await after.Content.ReadAsStringAsync(); var newEtag = after.Headers.ETag?.Tag ?? throw new InvalidOperationException("Updated ETag missing."); Assert(afterJson == valid && newEtag == "\"2\"", "HTTP revision and ETag increment");
        var invalid = await PostState(client, key, newEtag, "{\"doubles\":[{\"participantCode\":\"MISSING\",\"two\":\"A2\"}]}"); var invalidBody = await invalid.Content.ReadAsStringAsync(); Assert(invalid.StatusCode == HttpStatusCode.BadRequest, $"HTTP missing participant rejected ({(int)invalid.StatusCode}: {invalidBody})"); var unchanged = await client.GetAsync($"/api/state/{key}"); Assert(await unchanged.Content.ReadAsStringAsync() == valid && unchanged.Headers.ETag?.Tag == newEtag, "HTTP rejected state unchanged");
        var wrongCompetition = await PostState(client, key, newEtag, "{\"doubles\":[{\"one\":\"B1\",\"two\":\"A2\"}]}"); Assert(wrongCompetition.StatusCode == HttpStatusCode.BadRequest, "HTTP wrong-competition participant rejected");
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
