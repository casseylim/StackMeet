using Microsoft.Extensions.DependencyInjection;
using StackMeet.Api.Controllers;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Data;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

ActivatorUtilities.CreateFactory(typeof(PublicStackerProfilesController), Type.EmptyTypes);
Console.WriteLine("PASS MVC public profile controller activation");
if (args.Contains("--activation-probe")) return;

const string server = @"(localdb)\MSSQLLocalDB";
var name = "StackMeet_CareerReadinessTest_" + Guid.NewGuid().ToString("N");
var connection = new SqlConnectionStringBuilder {
    DataSource = server, InitialCatalog = name, IntegratedSecurity = true,
    TrustServerCertificate = true, MultipleActiveResultSets = true, ConnectTimeout = 5 };
if (connection.DataSource != server || !name.StartsWith("StackMeet_CareerReadinessTest_")
    || name.Any(c => !(char.IsLetterOrDigit(c) || c == '_'))) throw new InvalidOperationException("Unsafe local test target.");
var options = new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(connection.ConnectionString).Options;
Process? process = null;
var serverLog = new System.Text.StringBuilder();
try
{
    await using var db = new StackMeetDbContext(options);
    await db.Database.MigrateAsync();
    var now = DateTime.UtcNow;
    var closed = Competition("CAREER-CLOSED", "Closed", true);
    var active = Competition("CAREER-ACTIVE", "Active", true);
    var hidden = Competition("CAREER-HIDDEN", "Closed", false);
    db.Competitions.AddRange(closed, active, hidden);
    var ordinaryUser = new AppUser { Email = "ordinary@example.invalid", NormalizedEmail = "ORDINARY@EXAMPLE.INVALID",
        DisplayName = "Synthetic ordinary account", IsActive = true, SessionVersion = 1, CreatedAt = now };
    var systemUser = new AppUser { Email = "admin@example.invalid", NormalizedEmail = "ADMIN@EXAMPLE.INVALID",
        DisplayName = "Synthetic system administrator", IsActive = true, IsSystemAdmin = true, SessionVersion = 1, CreatedAt = now };
    db.AppUsers.AddRange(ordinaryUser, systemUser);
    await db.SaveChangesAsync();
    var first = Stacker(closed.Id, "1.1", "Synthetic", "Career");
    var other = Stacker(active.Id, "1.2", "Synthetic", "Career");
    var privateCompetition = Stacker(hidden.Id, "1.3", "Synthetic", "Career");
    var raceStacker = Stacker(closed.Id, "1.4", "Race", "Synthetic");
    var placeholder = Stacker(closed.Id, "1.99", "Placeholder", "-");
    db.Stackers.AddRange(first, other, privateCompetition, raceStacker, placeholder);
    await db.SaveChangesAsync();
    db.CompetitionResults.AddRange(Result(closed.Id, "1.1", "Prelims", 5m),
        Result(closed.Id, "1.1", "Finals", 4m), Result(active.Id, "1.2", "Prelims", 1m),
        Result(hidden.Id, "1.3", "Finals", 2m));
    await db.SaveChangesAsync();
    var beforeStackers = await db.Stackers.CountAsync();
    var beforeResults = await db.CompetitionResults.CountAsync();
    var resultSnapshot = await db.CompetitionResults.AsNoTracking().OrderBy(x => x.Id)
        .Select(x => new { x.Id, x.AttemptsJson, x.Revision }).ToListAsync();
    Console.WriteLine($"LIFECYCLE_BEFORE stackers={beforeStackers};results={beforeResults};identities=0;links=0");

    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
    var apiDirectory = Path.GetFullPath("backend/StackMeet.Api");
    var info = new ProcessStartInfo("dotnet") { WorkingDirectory = apiDirectory,
        UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    info.ArgumentList.Add(Path.Combine(apiDirectory, "bin/Release/net8.0/StackMeet.Api.dll"));
    info.Environment["ASPNETCORE_ENVIRONMENT"] = "Production"; // Never load production user-secrets.
    info.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
    info.Environment["ConnectionStrings__StackMeet"] = connection.ConnectionString;
    info.Environment["Security__AdminKey"] = "synthetic-career-admin-key";
    info.Environment["Security__ApiKey"] = "synthetic-career-api-key";
    info.Environment["Security__SessionSigningKey"] = "synthetic-career-session-signing-key";
    process = new Process { StartInfo = info };
    process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (serverLog) serverLog.AppendLine(e.Data); };
    process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (serverLog) serverLog.AppendLine(e.Data); };
    process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
    using var anonymous = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(20) };
    using var admin = new HttpClient { BaseAddress = anonymous.BaseAddress, Timeout = anonymous.Timeout };
    admin.DefaultRequestHeaders.Add("X-StackMeet-Admin-Key", "synthetic-career-admin-key");
    for (var attempt = 0; attempt < 40; attempt++)
    {
        try { using var ready = await anonymous.GetAsync("/api/version"); if (ready.IsSuccessStatusCode) break; }
        catch (HttpRequestException) { }
        if (process.HasExited) throw new InvalidOperationException("Test API exited: " + serverLog);
        await Task.Delay(250);
    }
    await Status(anonymous.GetAsync("/api/public/stackers/not-an-id"), HttpStatusCode.NotFound, "malformed ID safe 404 via real MVC");
    await Status(anonymous.GetAsync("/api/public/stackers/NDT-2345678"), HttpStatusCode.NotFound, "unknown ID safe 404 via real MVC");
    var tokens = new SessionTokenService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["Security:SessionSigningKey"] = "synthetic-career-session-signing-key" }).Build());
    using var systemAdmin = new HttpClient { BaseAddress = anonymous.BaseAddress, Timeout = anonymous.Timeout };
    systemAdmin.DefaultRequestHeaders.Authorization = new("Bearer", tokens.CreateForUser(systemUser.Id, systemUser.Email,
        systemUser.DisplayName, true, systemUser.SessionVersion).ToString());
    using var ordinary = new HttpClient { BaseAddress = anonymous.BaseAddress, Timeout = anonymous.Timeout };
    ordinary.DefaultRequestHeaders.Authorization = new("Bearer", tokens.CreateForUser(ordinaryUser.Id, ordinaryUser.Email,
        ordinaryUser.DisplayName, false, ordinaryUser.SessionVersion).ToString());
    await Status(anonymous.GetAsync("/api/admin/career-profile/capabilities"), HttpStatusCode.Unauthorized, "anonymous capabilities rejected");
    await Status(ordinary.GetAsync("/api/admin/career-profile/capabilities"), HttpStatusCode.Forbidden, "ordinary capabilities forbidden");
    var capabilities = await Json(systemAdmin.GetAsync("/api/admin/career-profile/capabilities"), "current system-admin capabilities");
    Assert(capabilities.EnumerateObject().Count() == 7 && capabilities.EnumerateObject().All(p => p.Value.ValueKind == JsonValueKind.True), "capabilities contain only seven operational booleans");
    await Json(admin.GetAsync("/api/admin/career-profile/capabilities"), "existing admin-key authority uses same gate");
    await Status(ordinary.GetAsync("/api/admin/stacker-identities/profiles/NDT-2345678/publication"), HttpStatusCode.Forbidden, "ordinary publication gate matches capabilities");
    await Status(ordinary.GetAsync("/api/admin/stacker-identities/links/1/unlink"), HttpStatusCode.Forbidden, "ordinary unlink gate matches capabilities");
    await Status(ordinary.PostAsJsonAsync("/api/admin/stacker-identities/links", Create(first.Id)), HttpStatusCode.Forbidden, "failed authorization cannot create identity");
    var review = await Json(systemAdmin.GetAsync("/api/admin/stacker-identities/stackers/" + first.Id), "read-only registration review");
    Assert(review.GetProperty("link").ValueKind == JsonValueKind.Null && !review.GetProperty("proposedIdentity").GetProperty("isPublicProfile").GetBoolean(), "review proposes private identity");
    var search = await Json(systemAdmin.GetAsync($"/api/admin/stacker-identities/stackers?competitionId={closed.Id}&search=1.1&take=1"), "bounded registration search");
    Assert(search.GetProperty("total").GetInt32() == 1 && search.GetProperty("items")[0].GetProperty("stackerId").GetInt32() == first.Id, "search selects exact participant");
    var badName = await Json(admin.GetAsync("/api/admin/stacker-identities/stackers/" + placeholder.Id), "placeholder-name review");
    Assert(!badName.GetProperty("createAllowed").GetBoolean(), "placeholder name cannot be copied into permanent identity");
    await Status(admin.PostAsJsonAsync("/api/admin/stacker-identities/links", Create(placeholder.Id)), HttpStatusCode.Conflict, "server rejects punctuation-only name on creation");
    Assert(IdentityNameQuality.IsUsable("Anne-Marie") && IdentityNameQuality.IsUsable("李") && !IdentityNameQuality.IsUsable("—"), "name quality preserves legitimate Unicode and hyphenated names");
    Assert(await db.SportStackerIdentities.CountAsync() == 0 && await db.StackerIdentityLinks.CountAsync() == 0 && await db.AuditLogs.CountAsync() == 0, "capabilities, reads and rejected requests perform no mutation");
    await Status(anonymous.PostAsJsonAsync("/api/admin/stacker-identities/links", Create(first.Id)), HttpStatusCode.Unauthorized, "anonymous creation rejected");
    var created = await Json(admin.PostAsJsonAsync("/api/admin/stacker-identities/links", Create(first.Id)), "create and link");
    var id = created.GetProperty("nadiTrackId").GetString()!;
    var linkId = created.GetProperty("linkId").GetInt64();
    Assert(!created.GetProperty("isPublicProfile").GetBoolean() && created.GetProperty("identityCreated").GetBoolean(), "new identity defaults private");
    var linkedReview = await Json(systemAdmin.GetAsync("/api/admin/stacker-identities/stackers/" + first.Id), "linked registration review");
    Assert(linkedReview.GetProperty("link").GetProperty("linkId").GetInt64() == linkId
        && linkedReview.GetProperty("link").GetProperty("nadiTrackId").GetString() == id
        && !linkedReview.GetProperty("link").GetProperty("isPublicProfile").GetBoolean(), "linked review retains exact relationship and private state");
    var adminProfile = await Json(admin.GetAsync("/api/admin/stacker-identities/profiles/" + id), "admin can inspect private publication state and link receipt");
    Assert(!adminProfile.GetProperty("isPublicProfile").GetBoolean()
        && adminProfile.GetProperty("links")[0].GetProperty("linkId").GetInt64() == linkId, "private identity review resolves exact link");
    var discovery = await Json(admin.GetAsync("/api/admin/stacker-identities/candidates?competitionId=" + closed.Id), "bounded candidate review");
    Assert(!discovery.GetRawText().Contains("birthDate") && !discovery.GetRawText().Contains("email")
        && !discovery.GetRawText().Contains("phone") && !discovery.GetRawText().Contains("wssaId"), "candidate API omits private matching inputs");
    Assert(await db.SportStackerIdentities.CountAsync() == 1 && await db.StackerIdentityLinks.CountAsync() == 1
        && await db.AuditLogs.CountAsync() == 1, "candidate/profile review performs no writes");
    await Status(anonymous.GetAsync("/api/admin/stacker-identities/candidates"), HttpStatusCode.Unauthorized, "candidate review requires admin");
    await Status(anonymous.GetAsync("/api/public/stackers/" + id), HttpStatusCode.NotFound, "private identity safe 404");
    await Status(admin.PostAsJsonAsync("/api/admin/stacker-identities/links", Create(first.Id)), HttpStatusCode.Conflict, "repeat link rejected");
    await Status(admin.PostAsJsonAsync("/api/admin/stacker-identities/links", Create(other.Id)), HttpStatusCode.Conflict, "possible duplicate person requires explicit review");
    Assert(await db.SportStackerIdentities.CountAsync() == 1, "duplicate creation leaves identity count unchanged");
    await Status(admin.PostAsJsonAsync("/api/admin/stacker-identities/links", Create(int.MaxValue)), HttpStatusCode.NotFound, "nonexistent stacker rejected");
    await Status(admin.PostAsJsonAsync("/api/admin/stacker-identities/links", Existing(other.Id, "NDT-2345678")), HttpStatusCode.Conflict, "nonexistent identity rejected");
    var second = await Json(admin.PostAsJsonAsync("/api/admin/stacker-identities/links", Existing(other.Id, id)), "explicit link to existing identity");
    await Json(admin.PostAsJsonAsync("/api/admin/stacker-identities/links", Existing(privateCompetition.Id, id)), "explicit private-competition link");
    Assert(!(await db.SportStackerIdentities.AsNoTracking().SingleAsync()).IsPublicProfile, "linking preserves private visibility");
    await Status(anonymous.PutAsJsonAsync($"/api/admin/stacker-identities/profiles/{id}/publication", Publication(true)), HttpStatusCode.Unauthorized, "anonymous publish rejected");
    anonymous.DefaultRequestHeaders.Authorization = new("Bearer", tokens.Create("CAREER-CLOSED", "Local manager").ToString());
    await Status(anonymous.PutAsJsonAsync($"/api/admin/stacker-identities/profiles/{id}/publication", Publication(true)), HttpStatusCode.Unauthorized, "competition-scoped session cannot publish");
    anonymous.DefaultRequestHeaders.Authorization = new("Bearer", tokens.CreateForUser(ordinaryUser.Id, ordinaryUser.Email,
        ordinaryUser.DisplayName, false, ordinaryUser.SessionVersion).ToString());
    await Status(anonymous.PutAsJsonAsync($"/api/admin/stacker-identities/profiles/{id}/publication", Publication(true)), HttpStatusCode.Forbidden, "current ordinary account cannot publish");
    anonymous.DefaultRequestHeaders.Authorization = null;
    await Status(admin.PutAsJsonAsync($"/api/admin/stacker-identities/profiles/{id}/publication", new { reason = "Missing flag" }), HttpStatusCode.BadRequest, "publication requires explicit boolean");
    await Json(systemAdmin.PutAsJsonAsync($"/api/admin/stacker-identities/profiles/{id}/publication", Publication(true)), "publish separately using current system-admin account");
    await Status(admin.PostAsJsonAsync("/api/admin/stacker-identities/links", Existing(raceStacker.Id, id)), HttpStatusCode.Conflict, "link cannot expose registration through already public identity");
    var profile = await Json(anonymous.GetAsync("/api/public/stackers/" + id), "public profile HTTP 200");
    Assert(profile.GetProperty("competitionCount").GetInt32() == 1, "only finalized publicly listed competition included");
    Assert(profile.GetProperty("personalBests")[0].GetProperty("officialTime").GetDecimal() == 4m, "Prelims and Finals best aggregated; Active and private results excluded");
    Privacy(profile);
    using var page = await anonymous.GetAsync("/Stackers/" + id);
    Assert(page.StatusCode == HttpStatusCode.OK && (await page.Content.ReadAsStringAsync()).Contains("profile.js"), "public profile shell and script render");
    Assert(page.Headers.CacheControl?.NoStore == true && page.Headers.GetValues("X-Robots-Tag").Contains("noindex, nofollow"), "page cache and crawler privacy gates");
    var finalRow = await db.CompetitionResults.SingleAsync(x => x.CompetitionId == closed.Id && x.Stage == "Finals");
    finalRow.AttemptsJson = "[6.000]"; finalRow.Revision = 2; await db.SaveChangesAsync();
    profile = await Json(anonymous.GetAsync("/api/public/stackers/" + id), "current revision read");
    Assert(profile.GetProperty("personalBests")[0].GetProperty("officialTime").GetDecimal() == 5m, "current persisted result replaces older revision; Prelims now wins");
    finalRow.AttemptsJson = resultSnapshot.Single(x => x.Id == finalRow.Id).AttemptsJson;
    finalRow.Revision = 1; await db.SaveChangesAsync();
    await Json(admin.PutAsJsonAsync($"/api/admin/stacker-identities/profiles/{id}/publication", Publication(false)), "unpublish separately");
    await Status(anonymous.GetAsync("/api/public/stackers/" + id), HttpStatusCode.NotFound, "unpublished profile inaccessible");
    await Status(anonymous.PostAsJsonAsync($"/api/admin/stacker-identities/links/{linkId}/unlink", Unlink(first.Id, id)), HttpStatusCode.Unauthorized, "anonymous unlink rejected");
    await Status(admin.PostAsJsonAsync($"/api/admin/stacker-identities/links/{linkId}/unlink", Unlink(other.Id, id)), HttpStatusCode.Conflict, "unlink requires exact relationship guards");
    await Status(admin.PostAsJsonAsync($"/api/admin/stacker-identities/links/{linkId}/unlink", Unlink(first.Id, id)), HttpStatusCode.NoContent, "selected relationship unlinked");
    await Status(admin.PostAsJsonAsync($"/api/admin/stacker-identities/links/{linkId}/unlink", Unlink(first.Id, id)), HttpStatusCode.NotFound, "repeat unlink safe 404");
    Assert(await db.Stackers.CountAsync() == beforeStackers && await db.CompetitionResults.CountAsync() == beforeResults, "unlink preserves registrations and results");
    var afterResults = await db.CompetitionResults.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.AttemptsJson, x.Revision }).ToListAsync();
    Assert(resultSnapshot.SequenceEqual(afterResults), "all historical result values preserved");
    Assert(await db.SportStackerIdentities.CountAsync() == 1 && await db.StackerIdentityLinks.CountAsync() == 2
        && await db.StackerIdentityLinks.AnyAsync(x => x.Id == second.GetProperty("linkId").GetInt64()), "permanent identity and other links preserved");
    Assert(await db.AuditLogs.CountAsync(x => x.Action == "StackerIdentity.Unlinked") == 1
        && await db.AuditLogs.CountAsync(x => x.Action == "StackerIdentity.PublicationChanged") == 2
        && await db.AuditLogs.CountAsync(x => x.Action == "StackerIdentity.Linked") == 3, "all successful administrative transitions audited");
    Assert(await db.AuditLogs.CountAsync(x => x.Action == "StackerIdentity.PublicationChanged" && x.UserId == systemUser.Id) == 1,
        "publication audit actor comes from validated system-admin session");
    Console.WriteLine($"LIFECYCLE_AFTER stackers={await db.Stackers.CountAsync()};results={await db.CompetitionResults.CountAsync()};identities=1;links=2;public=0;audit=6");

    // Real simultaneous requests use independent server DbContexts/transactions.
    var race = await Task.WhenAll(admin.PostAsJsonAsync("/api/admin/stacker-identities/links", Create(raceStacker.Id)),
        admin.PostAsJsonAsync("/api/admin/stacker-identities/links", Create(raceStacker.Id)));
    Assert(race.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && race.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
        "concurrent repeated creation has one winner and one safe conflict");
    foreach (var response in race) response.Dispose();
    Assert(await db.StackerIdentityLinks.CountAsync(x => x.StackerId == raceStacker.Id) == 1 && await db.SportStackerIdentities.CountAsync() == 2,
        "concurrency creates no orphan or duplicate permanent identity");

    var parallelOne = Stacker(closed.Id, "1.5", "Parallel", "Person");
    var parallelTwo = Stacker(active.Id, "1.6", "Parallel", "Person");
    db.Stackers.AddRange(parallelOne, parallelTwo); await db.SaveChangesAsync();
    var personRace = await Task.WhenAll(admin.PostAsJsonAsync("/api/admin/stacker-identities/links", Create(parallelOne.Id)),
        admin.PostAsJsonAsync("/api/admin/stacker-identities/links", Create(parallelTwo.Id)));
    Assert(personRace.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && personRace.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
        "concurrent creation for matching registrations cannot create two people");
    foreach (var response in personRace) response.Dispose();
    Assert(await db.SportStackerIdentities.CountAsync() == 3 && await db.StackerIdentityLinks.CountAsync() == 4,
        "cross-registration race leaves one identity and one link for the matching person");

    // A stale approved CreateNew decision is rejected inside the persistence transaction.
    var approved = StackerIdentityResolutionPolicy.Resolve(StackerIdentityMatcher.FindMatches(
        new(null, null, "Synthetic", "Career", null, "MY", null, null, null), []), new(StackerIdentityResolutionAction.CreateNew, null, false, false, "Local review"));
    await using var fresh = new StackMeetDbContext(options);
    try { await new StackerIdentityPersistenceService(fresh, new CryptographicNadiTrackIdGenerator())
        .PersistAsync(new(first.Id, approved, "Local review", null)); throw new Exception("Stale CreateNew should be blocked."); }
    catch (InvalidOperationException ex) { Assert(ex.Message.Contains("new operator review"), "persistence rechecks stale duplicate decision"); }
    Console.WriteLine("CAREER_PROFILE_LOCAL_LIFECYCLE=PASS; PRODUCTION_WRITES=0");
}
catch
{
    Console.Error.WriteLine(serverLog.ToString());
    throw;
}
finally
{
    if (process is not null) { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } process.Dispose(); }
    // Drop only the generated, allowlisted LOCAL database, never a configured environment target.
    var master = new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" };
    await using var cleanup = new SqlConnection(master.ConnectionString); await cleanup.OpenAsync();
    await using var command = cleanup.CreateCommand();
    command.CommandText = $"IF DB_ID(@name) IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END";
    command.Parameters.AddWithValue("@name", name); await command.ExecuteNonQueryAsync();
    Console.WriteLine("PASS generated LocalDB test database removed");
}

static object Create(int id) => new { stackerId = id, requestedAction = 2, resolutionNote = "Synthetic local lifecycle review" };
static object Existing(int id, string nadi) => new { stackerId = id, requestedAction = 1, explicitNadiTrackId = nadi,
    selectedNadiTrackId = nadi, candidateConfirmed = true, resolutionNote = "Explicit synthetic identity review" };
static object Publication(bool value) => new { isPublicProfile = value, reason = "Synthetic local lifecycle publication review" };
static object Unlink(int id, string nadi) => new { stackerId = id, nadiTrackId = nadi, reason = "Synthetic local lifecycle rollback" };
static async Task Status(Task<HttpResponseMessage> task, HttpStatusCode expected, string name)
{
    using var response = await task;
    Assert(response.StatusCode == expected, $"{name}: {(int)response.StatusCode} (expected {(int)expected}); {await response.Content.ReadAsStringAsync()}");
}
static async Task<JsonElement> Json(Task<HttpResponseMessage> task, string name)
{
    using var response = await task; var body = await response.Content.ReadAsStringAsync();
    Assert(response.StatusCode == HttpStatusCode.OK, $"{name}: {(int)response.StatusCode}" + (response.IsSuccessStatusCode ? "" : "; " + body));
    using var json = JsonDocument.Parse(body); return json.RootElement.Clone();
}
static void Privacy(JsonElement element)
{
    if (element.ValueKind == JsonValueKind.Object)
        foreach (var property in element.EnumerateObject())
        {
            if (new[] { "email", "phone", "birthDate", "wssaId", "stackerId", "identityId", "resultId", "resolutionNote", "linkedByUserId" }.Contains(property.Name))
                throw new Exception("Private field in public response: " + property.Name);
            Privacy(property.Value);
        }
    else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Privacy(item);
}
static void Assert(bool value, string name) { if (!value) throw new InvalidOperationException(name); Console.WriteLine("PASS " + name); }
static Competition Competition(string key, string status, bool listed) => new() {
    CompetitionKey = key, CompetitionCode = key, CompetitionName = key, Venue = "Synthetic LocalDB",
    Status = status, IsPubliclyListed = listed, StartDate = new(2026, 9, 1), EndDate = new(2026, 9, 1), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
static Stacker Stacker(int competition, string code, string first, string last) => new() {
    CompetitionId = competition, StackerCode = code, FirstName = first, LastName = last, Gender = "F", Country = "MY",
    Email = first + "-private-local@example.invalid", Phone = first == "Synthetic" ? "5550000" : null, BirthDate = new(2000, 1, 1),
    Paid = "No", CheckedIn = "No", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
static CompetitionResult Result(int competition, string code, string stage, decimal time) => new() {
    CompetitionId = competition, ParticipantCode = code, ParticipantType = "Individual", Stage = stage, EventCode = "3-3-3",
    AttemptsJson = JsonSerializer.Serialize(new[] { time }), Penalty = 0, Revision = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
