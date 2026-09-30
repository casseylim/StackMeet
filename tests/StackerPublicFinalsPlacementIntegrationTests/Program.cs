using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Activities.SportStacking.Ranking;
using StackMeet.Api.Controllers;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerPublicPlacementSp4nTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
var builder = new SqlConnectionStringBuilder
{
    DataSource = server,
    InitialCatalog = databaseName,
    IntegratedSecurity = true,
    TrustServerCertificate = true,
    MultipleActiveResultSets = true,
    ConnectTimeout = 5
};

if (!string.Equals(builder.DataSource, server, StringComparison.Ordinal))
    throw new InvalidOperationException("Refusing SP-4N integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerPublicPlacementSp4nTest_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-4N cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-4N LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
var options = new DbContextOptionsBuilder<StackMeetDbContext>()
    .UseSqlServer(builder.ConnectionString)
    .Options;

Exception? primaryFailure = null;
Exception? cleanupFailure = null;

try
{
    var masterBuilder = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };
    await using (var probe = new SqlConnection(masterBuilder.ConnectionString))
    {
        using var probeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await probe.OpenAsync(probeTimeout.Token);
    }

    await using var db = new StackMeetDbContext(options);
    using (var migrationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
    {
        await db.Database.MigrateAsync(migrationTimeout.Token);
    }

    var now = DateTime.UtcNow;
    var publicIdentity = NewIdentity("NDT-ABCDEFG", "Ava", "Stacker", true, now);
    var privateIdentity = NewIdentity("NDT-HJKMNPQ", "Private", "Stacker", false, now);
    db.SportStackerIdentities.AddRange(publicIdentity, privateIdentity);

    var certified = NewCompetition(
        "SP4N-CERT",
        "SP-4N Certified Championship",
        new DateOnly(2026, 5, 10),
        "Active",
        true,
        resultsRevision: 5,
        now);
    var legacy = NewCompetition(
        "SP4N-LEGACY",
        "SP-4N Legacy Championship",
        new DateOnly(2025, 5, 10),
        "Closed",
        true,
        resultsRevision: 1,
        now);
    db.Competitions.AddRange(certified, legacy);
    await db.SaveChangesAsync();

    var target = NewStacker(certified.Id, "A1", "Ava", "Stacker", "F", now);
    var competitor = NewStacker(certified.Id, "B1", "Beta", "Stacker", "F", now);
    var specialCompetitor = NewStacker(certified.Id, "S1", "Special", "Stacker", "F", now);
    var legacyTarget = NewStacker(legacy.Id, "L1", "Ava", "Stacker", "F", now);
    db.Stackers.AddRange(target, competitor, specialCompetitor, legacyTarget);
    await db.SaveChangesAsync();

    db.StackerIdentityLinks.AddRange(
        NewLink(publicIdentity.Id, target.Id, "TEST_SP4N_CERT", now),
        NewLink(publicIdentity.Id, legacyTarget.Id, "TEST_SP4N_LEGACY", now));

    db.CompetitionStates.Add(NewState(
        certified.CompetitionKey,
        """
        {
          "stackers": [
            { "id": "A1", "name": "Ava Stacker", "gender": "F", "division": "Female 12U", "special": "No" },
            { "id": "B1", "name": "Beta Stacker", "gender": "F", "division": "Female 12U", "special": "No" },
            { "id": "S1", "name": "Special Stacker", "gender": "F", "division": "Female 12U", "special": "Yes" }
          ]
        }
        """,
        revision: 11,
        now));

    var targetCycle = NewResult(certified.Id, "A1", "Cycle", "[5.5,5.7,5.8]", 0m, 1, now);
    db.CompetitionResults.AddRange(
        targetCycle,
        NewResult(certified.Id, "B1", "Cycle", "[5.0,5.1,5.2]", 0m, 2, now),
        NewResult(certified.Id, "S1", "Cycle", "[4.0,4.1,4.2]", 0m, 3, now),
        NewResult(certified.Id, "A1", "3-3-3", "[3.0,3.1,3.2]", 999m, 4, now),
        NewResult(certified.Id, "B1", "3-3-3", "[3.2,3.3,3.4]", 0m, 5, now),
        NewResult(legacy.Id, "L1", "Cycle", "[6.0,6.1,6.2]", 0m, 1, now));
    await db.SaveChangesAsync();

    var governance = new FinalsRankingGovernanceService(db);
    await governance.SelectRuleVersionAsync(certified.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 4201);
    certified.Status = "Closed";
    certified.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    var captured = await governance.CertifyGovernedV2SnapshotAsync(certified.Id, 4202);
    Assert(captured.HasSnapshot, "governed-v2 immutable snapshot is certified before public placement integration");

    var profileService = new SportStackerCareerProfileService(db);
    var placementService = new PublicFinalsPlacementCareerIntegrationService(db);
    var controller = new PublicStackerProfilesController(profileService, placementService);

    var profile = await PublicProfile(controller, publicIdentity.NadiTrackId);
    var publication = profile.FinalsPlacements
        ?? throw new InvalidOperationException("Expected SP-4N Finals placement publication.");

    Assert(publication.PublicationVersion == PublicFinalsPlacementPublicationContract.PublicationVersion,
        "public profile uses the reviewed SP-4M publication version");
    Assert(publication.CohortPolicy == PublicFinalsPlacementPublicationContract.CohortPolicy,
        "public profile states the fixed privacy-safe cohort policy");
    Assert(publication.History.Count == 2,
        "automatic selector publishes only supported immutable events for the linked athlete");

    var cycle = publication.History.Single(item => item.EventCode == "Cycle");
    Assert(cycle.ResultStatus == "Valid" && cycle.Placement == 3 && cycle.OfficialBestTime == 5.5m,
        "real SP-4L lowercase Valid evidence is normalized and published with mixed/all placement");
    Assert(!cycle.SharesPlacement, "non-tied immutable placement is published factually");

    var scratch = publication.History.Single(item => item.EventCode == "3-3-3");
    Assert(scratch.ResultStatus == "Scratch"
        && scratch.Placement is null
        && scratch.OfficialBestTime is null
        && !scratch.SharesPlacement,
        "Scratch evidence remains a factual unplaced Finals result without fabricated placement");

    Assert(profile.CompetitionCount == 2,
        "legacy finalized/public competition remains in the existing career profile");
    Assert(publication.History.All(item => item.CompetitionKey != legacy.CompetitionKey),
        "legacy/non-certified competition contributes no permanent placement");

    var serialized = JsonSerializer.Serialize(publication);
    Assert(!serialized.Contains("Female 12U", StringComparison.Ordinal),
        "raw gendered historical division label never reaches the public placement payload");
    Assert(!serialized.Contains("A1", StringComparison.Ordinal)
        && !serialized.Contains(captured.SnapshotSha256 ?? "__missing__", StringComparison.Ordinal),
        "participant code and immutable snapshot hash never reach the public placement payload");

    var publicPointProperties = typeof(PublicFinalsPlacementPoint)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(item => item.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var forbidden in new[]
    {
        "Division", "Category", "Gender", "Special", "ParticipantCode", "ParticipantName",
        "SnapshotSha256", "SourceStateRevision", "SourceResultsRevision", "CompetitionId",
        "Medal", "Award", "Podium", "Record"
    })
    {
        Assert(!publicPointProperties.Contains(forbidden), $"public placement point excludes {forbidden}");
    }

    // Mutate today's registration and result rows after certification. The permanent placement must
    // remain anchored to the immutable snapshot, even though the legacy-compatible career views may
    // legitimately observe today's finalized result row.
    target.Gender = "M";
    target.CustomDivision = "Changed Current Division";
    target.UpdatedAt = DateTime.UtcNow;
    targetCycle.AttemptsJson = "[1.0,1.1,1.2]";
    targetCycle.Penalty = 0m;
    targetCycle.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    var afterMutation = await PublicProfile(controller, publicIdentity.NadiTrackId);
    var immutableCycle = afterMutation.FinalsPlacements?.History.Single(item => item.EventCode == "Cycle")
        ?? throw new InvalidOperationException("Expected immutable Cycle placement after live-row mutation.");
    Assert(immutableCycle.Placement == 3 && immutableCycle.OfficialBestTime == 5.5m,
        "current Stacker demographics and current CompetitionResult mutation cannot rewrite permanent placement");

    Assert((await controller.Get(privateIdentity.NadiTrackId, CancellationToken.None)).Result is NotFoundResult,
        "private permanent identity retains the public not-found boundary");
    Assert((await controller.Get("not-a-naditrack-id", CancellationToken.None)).Result is NotFoundResult,
        "malformed identity retains the public not-found boundary");

    Console.WriteLine("SP-4N public Finals placement integration tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-4N primary failure: {ex}");
}
finally
{
    try
    {
        var masterBuilder = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };
        await using var cleanup = new SqlConnection(masterBuilder.ConnectionString);
        using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await cleanup.OpenAsync(cleanupTimeout.Token);
        await using var command = cleanup.CreateCommand();
        command.CommandTimeout = 8;
        var escapedName = databaseName.Replace("]", "]]", StringComparison.Ordinal);
        command.CommandText = $"IF DB_ID(@databaseName) IS NOT NULL BEGIN ALTER DATABASE [{escapedName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{escapedName}]; END";
        command.Parameters.AddWithValue("@databaseName", databaseName);
        await command.ExecuteNonQueryAsync(cleanupTimeout.Token);
        Console.WriteLine($"SP-4N cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-4N cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-4N cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-4N tests passed but cleanup failed.", cleanupFailure);

static async Task<PublicSportStackerCareerProfile> PublicProfile(
    PublicStackerProfilesController controller,
    string nadiTrackId)
{
    var action = await controller.Get(nadiTrackId, CancellationToken.None);
    var ok = action.Result as OkObjectResult
        ?? throw new InvalidOperationException("Expected SP-4N public profile to return HTTP 200.");
    return ok.Value as PublicSportStackerCareerProfile
        ?? throw new InvalidOperationException("Expected SP-4N endpoint to return the public career contract.");
}

static SportStackerIdentity NewIdentity(
    string nadiTrackId,
    string firstName,
    string lastName,
    bool isPublic,
    DateTime now) => new()
{
    NadiTrackId = nadiTrackId,
    FirstName = firstName,
    LastName = lastName,
    Country = "MY",
    IsPublicProfile = isPublic,
    CreatedAt = now,
    UpdatedAt = now
};

static Competition NewCompetition(
    string key,
    string name,
    DateOnly date,
    string status,
    bool publiclyListed,
    long resultsRevision,
    DateTime now) => new()
{
    CompetitionCode = key,
    CompetitionKey = key,
    CompetitionName = name,
    Venue = "LocalDB",
    StartDate = date,
    EndDate = date,
    Status = status,
    IsPubliclyListed = publiclyListed,
    ResultsRevision = resultsRevision,
    CreatedAt = now,
    UpdatedAt = now
};

static Stacker NewStacker(
    int competitionId,
    string code,
    string firstName,
    string lastName,
    string gender,
    DateTime now) => new()
{
    CompetitionId = competitionId,
    StackerCode = code,
    FirstName = firstName,
    LastName = lastName,
    Gender = gender,
    Country = "MY",
    Paid = "No",
    CheckedIn = "No",
    CreatedAt = now,
    UpdatedAt = now
};

static StackerIdentityLink NewLink(long identityId, int stackerId, string reasonCode, DateTime now) => new()
{
    SportStackerIdentityId = identityId,
    StackerId = stackerId,
    MatchMethod = StackerIdentityMatchMethod.Manual,
    ResolutionReasonCode = reasonCode,
    ResolutionNote = "SP-4N reviewed historical identity link fixture.",
    LinkedAt = now
};

static CompetitionState NewState(string competitionKey, string json, long revision, DateTime now) => new()
{
    CompetitionKey = competitionKey,
    JsonData = json,
    SchemaVersion = "0.9-online",
    StateRevision = revision,
    CreatedAt = now,
    UpdatedAt = now,
    UpdatedBy = "sp4n-test"
};

static CompetitionResult NewResult(
    int competitionId,
    string participantCode,
    string eventCode,
    string attemptsJson,
    decimal penalty,
    long revision,
    DateTime now) => new()
{
    PublicId = Guid.NewGuid(),
    CompetitionId = competitionId,
    Stage = "Finals",
    ParticipantType = "Individual",
    ParticipantCode = participantCode,
    EventCode = eventCode,
    AttemptsJson = attemptsJson,
    Penalty = penalty,
    Revision = revision,
    CreatedAt = now,
    UpdatedAt = now
};

static void Assert(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"Assertion failed: {name}");
    Console.WriteLine($"PASS: {name}");
}
