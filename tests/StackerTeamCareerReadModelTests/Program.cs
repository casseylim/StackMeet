using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Data;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerTeamCareerSp4qTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
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
    throw new InvalidOperationException("Refusing SP-4Q integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerTeamCareerSp4qTest_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-4Q cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-4Q LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
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
    var athlete = NewIdentity("NDT-ABCDEFG", "Career", "Athlete", true, now);
    var privateIdentity = NewIdentity("NDT-BCDEFGH", "Private", "Athlete", false, now);
    var externalNameIdentity = NewIdentity("NDT-CDEFGHI", "External", "Parent", true, now);
    db.SportStackerIdentities.AddRange(athlete, privateIdentity, externalNameIdentity);

    var c1 = NewCompetition("SP4Q-1", "Team Career One", new DateOnly(2026, 1, 10), "Closed", true, 8, now);
    var c2 = NewCompetition("SP4Q-2", "Team Career Two", new DateOnly(2026, 2, 10), "Archived", true, 2, now);
    c2.ArchivedAt = now;
    var active = NewCompetition("SP4Q-ACTIVE", "Active Team Career", new DateOnly(2026, 3, 10), "Active", true, 1, now);
    var hidden = NewCompetition("SP4Q-HIDDEN", "Hidden Team Career", new DateOnly(2026, 4, 10), "Closed", false, 1, now);
    db.Competitions.AddRange(c1, c2, active, hidden);
    await db.SaveChangesAsync();

    var a1 = NewStacker(c1.Id, "A1", "Career", "Athlete", now);
    var a2 = NewStacker(c1.Id, "A2", "Team", "Mate", now);
    var a3 = NewStacker(c1.Id, "A3", "Relay", "Three", now);
    var a4 = NewStacker(c1.Id, "A4", "Relay", "Four", now);
    var x9 = NewStacker(c1.Id, "X9", "External", "Parent", now);
    var b1 = NewStacker(c2.Id, "B1", "Career", "Athlete", now);
    var b2 = NewStacker(c2.Id, "B2", "Relay", "Two", now);
    var b3 = NewStacker(c2.Id, "B3", "Relay", "Three", now);
    var b4 = NewStacker(c2.Id, "B4", "Relay", "Four", now);
    var aa = NewStacker(active.Id, "AA", "Career", "Athlete", now);
    var ah = NewStacker(hidden.Id, "AH", "Career", "Athlete", now);
    db.Stackers.AddRange(a1, a2, a3, a4, x9, b1, b2, b3, b4, aa, ah);
    await db.SaveChangesAsync();

    db.StackerIdentityLinks.AddRange(
        NewLink(athlete.Id, a1.Id, "TEST_SP4Q_C1", now),
        NewLink(athlete.Id, b1.Id, "TEST_SP4Q_C2", now),
        NewLink(athlete.Id, aa.Id, "TEST_SP4Q_ACTIVE", now),
        NewLink(athlete.Id, ah.Id, "TEST_SP4Q_HIDDEN", now),
        NewLink(externalNameIdentity.Id, x9.Id, "TEST_SP4Q_EXTERNAL_NAME", now));
    await db.SaveChangesAsync();

    var c1StateJson = """
    {
      "doubles": [
        { "id": "D1", "one": "A1", "two": "A2" },
        { "id": "CP1", "one": "A1", "type": "child_parent", "parentName": "External Parent" },
        { "id": "OTHER", "one": "A2", "two": "A3" }
      ],
      "relays": [
        { "id": "R1", "members": [ "A1", "A2", "A3", "A4" ] }
      ]
    }
    """;
    var c2StateJson = """
    {
      "doubles": [],
      "relays": [
        { "id": "R2", "members": [ "B1", "B2", "B3", "B4" ] }
      ]
    }
    """;
    var activeStateJson = """
    {
      "doubles": [
        { "id": "DA", "one": "AA", "two": "A2" }
      ],
      "relays": []
    }
    """;
    var hiddenStateJson = """
    {
      "doubles": [
        { "id": "DH", "one": "AH", "two": "A2" }
      ],
      "relays": []
    }
    """;

    var c1State = NewState(c1.CompetitionKey, c1StateJson, 11, now);
    db.CompetitionStates.AddRange(
        c1State,
        NewState(c2.CompetitionKey, c2StateJson, 12, now),
        NewState(active.CompetitionKey, activeStateJson, 13, now),
        NewState(hidden.CompetitionKey, hiddenStateJson, 14, now));

    db.CompetitionResults.AddRange(
        NewResult(c1.Id, "Finals", "Doubles", "D1", "Cycle", "[8.000,8.200,8.100]", 0.500m, 1, now),
        NewResult(c1.Id, "Prelims", "Doubles", "CP1", "Cycle", "[7.000,7.100,7.200]", 999m, 2, now),
        NewResult(c1.Id, "Finals", "Timed Relay", "R1", "3-6-3", "[20.000,20.100,20.200]", 0m, 3, now),
        NewResult(c1.Id, "Finals", "Doubles", "OTHER", "Cycle", "[6.000,6.100,6.200]", 0m, 4, now),
        NewResult(c2.Id, "Finals", "Relay", "R2", "3-6-3", "[19.000,19.100,19.200]", 0m, 2, now),
        NewResult(active.Id, "Finals", "Doubles", "DA", "Cycle", "[5.000,5.100,5.200]", 0m, 1, now),
        NewResult(hidden.Id, "Finals", "Doubles", "DH", "Cycle", "[5.500,5.600,5.700]", 0m, 1, now));
    await db.SaveChangesAsync();

    var service = new IdentityLinkedTeamCareerService(db);
    var career = await service.GetPublicEligibleAsync(athlete.NadiTrackId)
        ?? throw new InvalidOperationException("Expected public SP-4Q team career read model.");

    Assert(career.NadiTrackId == athlete.NadiTrackId, "team career read model is keyed by permanent NADITrack ID");
    Assert(career.History.Count == 4, "only linked finalized/public team performances enter SP-4Q");

    var d1 = Point(career, "SP4Q-1", "Doubles", "D1");
    Assert(d1.ResultStatus == "Valid"
        && d1.RawBestTime == 8.000m
        && d1.AppliedPenalty == 0.500m
        && d1.OfficialBestTime == 8.500m,
        "Doubles factual time is derived without individual ranking interpretation");
    Assert(d1.RegisteredMemberCount == 2 && !d1.HasExternalPartner,
        "normal Doubles retains only privacy-safe membership shape");

    var childParent = Point(career, "SP4Q-1", "Doubles", "CP1");
    Assert(childParent.ResultStatus == "Scratch"
        && childParent.OfficialBestTime is null
        && childParent.RegisteredMemberCount == 1
        && childParent.HasExternalPartner,
        "Child/Parent external partner remains external and scratch penalty wins before valid-attempt classification");

    var relay = Point(career, "SP4Q-1", "Timed Relay", "R1");
    Assert(relay.ResultStatus == "Valid"
        && relay.RegisteredMemberCount == 4
        && !relay.HasExternalPartner,
        "Timed Relay resolves only registered competition Stackers");

    var legacyRelay = Point(career, "SP4Q-2", "Timed Relay", "R2");
    Assert(legacyRelay.ParticipantType == "Timed Relay" && legacyRelay.OfficialBestTime == 19.000m,
        "legacy Relay result alias normalizes to Timed Relay");

    Assert(career.History.All(item => item.CompetitionKey != "SP4Q-ACTIVE" && item.CompetitionKey != "SP4Q-HIDDEN"),
        "active and non-public competitions cannot enter SP-4Q");
    Assert(career.History.All(item => item.TeamCode != "OTHER"),
        "team result does not enter a career unless the reviewed linked Stacker is a validated team member");

    Assert(d1.Evidence.CompetitionStateRevision == 11
        && d1.Evidence.CompetitionResultsRevision == 8
        && d1.Evidence.ResultRevision == 1
        && d1.Evidence.RegisteredMembershipSha256.Length == 64,
        "SP-4Q retains revision and membership-fingerprint provenance");

    var externalNameCareer = await service.GetPublicEligibleAsync(externalNameIdentity.NadiTrackId)
        ?? throw new InvalidOperationException("Expected public external-name identity read model.");
    Assert(externalNameCareer.History.Count == 0,
        "external parent display name never creates a permanent identity team link");

    Assert(await service.GetPublicEligibleAsync(privateIdentity.NadiTrackId) is null,
        "private identity shares the public not-found boundary");
    Assert(await service.GetPublicEligibleAsync("not-a-naditrack-id") is null,
        "malformed identity shares the public not-found boundary");

    var pointProperties = typeof(IdentityLinkedTeamCareerPoint)
        .GetProperties()
        .Select(item => item.Name)
        .ToHashSet(StringComparer.Ordinal);
    foreach (var forbidden in new[]
    {
        "BirthDate", "Email", "Phone", "WssaId", "ParticipantCode", "MemberParticipantCodes",
        "MemberNames", "PartnerName", "ParentName", "StackerId", "CompetitionId", "Placement", "Rank", "Medal"
    })
    {
        Assert(!pointProperties.Contains(forbidden), $"SP-4Q career point excludes {forbidden}");
    }

    var integrity = new CompetitionTeamResultIntegrityService(db);
    var changedMembershipJson = c1StateJson.Replace(
        """{ "id": "D1", "one": "A1", "two": "A2" }""",
        """{ "id": "D1", "one": "A1", "two": "A3" }""",
        StringComparison.Ordinal);
    var changedMembershipError = await integrity.ValidateStateAgainstExistingResultsAsync(
        c1.Id,
        c1StateJson,
        changedMembershipJson);
    Assert(changedMembershipError?.Contains("cannot change Doubles team members", StringComparison.Ordinal) == true,
        "shared server integrity boundary blocks historical team membership drift while SQL results exist");

    var metadataOnlyJson = c1StateJson.Replace(
        """{ "id": "D1", "one": "A1", "two": "A2" }""",
        """{ "id": "D1", "one": "A1", "two": "A2", "division": "Open" }""",
        StringComparison.Ordinal);
    Assert(await integrity.ValidateStateAgainstExistingResultsAsync(c1.Id, c1StateJson, metadataOnlyJson) is null,
        "non-membership team metadata may change without rewriting team composition");

    var beforeFingerprint = d1.Evidence.RegisteredMembershipSha256;
    c1State.JsonData = metadataOnlyJson;
    c1State.StateRevision++;
    c1State.UpdatedAt = DateTime.UtcNow;
    a1.FirstName = "Mutated";
    a1.LastName = "Registration";
    a1.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    var afterMetadataMutation = await service.GetPublicEligibleAsync(athlete.NadiTrackId)
        ?? throw new InvalidOperationException("Expected SP-4Q career after metadata mutation.");
    var afterD1 = Point(afterMetadataMutation, "SP4Q-1", "Doubles", "D1");
    Assert(afterD1.Evidence.RegisteredMembershipSha256 == beforeFingerprint
        && afterD1.Evidence.CompetitionStateRevision == 12
        && afterD1.OfficialBestTime == 8.500m,
        "profile/metadata changes cannot alter protected team membership or factual team performance");

    var duplicate = NewStacker(c1.Id, "A1-DUP", "Career", "Athlete", now);
    db.Stackers.Add(duplicate);
    await db.SaveChangesAsync();
    db.StackerIdentityLinks.Add(NewLink(athlete.Id, duplicate.Id, "TEST_SP4Q_AMBIGUOUS", now));
    await db.SaveChangesAsync();

    await AssertThrowsContainsAsync(
        async () =>
        {
            await service.GetPublicEligibleAsync(athlete.NadiTrackId);
        },
        IdentityLinkedTeamCareerBlockers.IdentityLinkAmbiguous,
        "multiple reviewed same-competition identity links fail closed");

    Console.WriteLine("SP-4Q identity-linked team career read-model tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-4Q primary failure: {ex}");
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
        command.Parameters.AddWithValue("databaseName", databaseName);
        await command.ExecuteNonQueryAsync(cleanupTimeout.Token);
        Console.WriteLine($"SP-4Q cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-4Q cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-4Q cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-4Q tests passed but cleanup failed.", cleanupFailure);

static IdentityLinkedTeamCareerPoint Point(
    IdentityLinkedTeamCareerReadModel career,
    string competitionKey,
    string participantType,
    string teamCode) =>
    career.History.Single(item =>
        item.CompetitionKey == competitionKey
        && item.ParticipantType == participantType
        && item.TeamCode == teamCode);

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
    DateTime now) => new()
{
    CompetitionId = competitionId,
    StackerCode = code,
    FirstName = firstName,
    LastName = lastName,
    Gender = "M",
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
    ResolutionNote = "SP-4Q reviewed historical identity link fixture.",
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
    UpdatedBy = "sp4q-test"
};

static CompetitionResult NewResult(
    int competitionId,
    string stage,
    string participantType,
    string participantCode,
    string eventCode,
    string attemptsJson,
    decimal penalty,
    long revision,
    DateTime now) => new()
{
    PublicId = Guid.NewGuid(),
    CompetitionId = competitionId,
    Stage = stage,
    ParticipantType = participantType,
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

static async Task AssertThrowsContainsAsync(
    Func<Task> action,
    string expectedText,
    string name)
{
    try
    {
        await action();
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains(expectedText, StringComparison.Ordinal))
    {
        Console.WriteLine($"PASS: {name}");
        return;
    }

    throw new InvalidOperationException(
        $"Assertion failed: {name} (expected InvalidOperationException containing '{expectedText}').");
}
