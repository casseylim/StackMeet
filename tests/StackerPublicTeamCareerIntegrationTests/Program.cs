using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Controllers;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerPublicTeamSp4sTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
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
    throw new InvalidOperationException("Refusing SP-4S integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerPublicTeamSp4sTest_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-4S cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-4S LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
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

    var competition = NewCompetition(
        "SP4S-TEAM",
        "SP-4S Team Championship",
        new DateOnly(2026, 7, 12),
        "Closed",
        true,
        resultsRevision: 9,
        now);
    db.Competitions.Add(competition);
    await db.SaveChangesAsync();

    var a1 = NewStacker(competition.Id, "A1", "Ava", "Stacker", now);
    var a2 = NewStacker(competition.Id, "A2", "Team", "Mate", now);
    var a3 = NewStacker(competition.Id, "A3", "Relay", "Three", now);
    var a4 = NewStacker(competition.Id, "A4", "Relay", "Four", now);
    db.Stackers.AddRange(a1, a2, a3, a4);
    await db.SaveChangesAsync();

    db.StackerIdentityLinks.Add(NewLink(publicIdentity.Id, a1.Id, "TEST_SP4S_LINK", now));

    db.CompetitionStates.Add(NewState(
        competition.CompetitionKey,
        """
        {
          "doubles": [
            { "id": "D1", "one": "A1", "two": "A2" },
            { "id": "CP1", "one": "A1", "type": "child_parent", "parentName": "External Parent Secret" }
          ],
          "relays": [
            { "id": "R1", "members": [ "A1", "A2", "A3", "A4" ] }
          ]
        }
        """,
        revision: 14,
        now));

    db.CompetitionResults.AddRange(
        NewResult(competition.Id, "Finals", "Doubles", "D1", "Cycle", "[8.000,8.200,8.100]", 0.500m, 1, now),
        NewResult(competition.Id, "Prelims", "Doubles", "CP1", "Cycle", "[7.000,7.100,7.200]", 999m, 2, now),
        NewResult(competition.Id, "Finals", "Timed Relay", "R1", "3-6-3", "[20.000,20.100,20.200]", 0m, 3, now));
    await db.SaveChangesAsync();

    var profileService = new SportStackerCareerProfileService(db);
    var teamService = new PublicTeamCareerIntegrationService(db);
    var controller = new PublicStackerProfilesController(profileService, null, teamService);

    var profile = await PublicProfile(controller, publicIdentity.NadiTrackId);
    var publication = profile.TeamCareer
        ?? throw new InvalidOperationException("Expected SP-4S team career publication.");

    Assert(publication.PublicationVersion == PublicTeamCareerPublicationContract.PublicationVersion,
        "public profile uses the reviewed SP-4R publication version");
    Assert(publication.PrivacyPolicy == PublicTeamCareerPublicationContract.PrivacyPolicy,
        "public profile states the reviewed teammate-withholding privacy policy");
    Assert(publication.History.Count == 3,
        "public profile publishes the three verified privacy-safe team contexts");

    var doubles = publication.History.Single(item =>
        item.ParticipantType == "Doubles" && item.Stage == "Finals");
    Assert(doubles.EventCode == "Cycle"
        && doubles.ResultStatus == "Valid"
        && doubles.RawBestTime == 8.000m
        && doubles.AppliedPenalty == 0.500m
        && doubles.OfficialBestTime == 8.500m,
        "normal Doubles factual timing survives SP-4Q to SP-4R to public profile");

    var childParent = publication.History.Single(item =>
        item.ParticipantType == "Doubles" && item.Stage == "Prelims");
    Assert(childParent.ResultStatus == "Scratch"
        && childParent.OfficialBestTime is null
        && childParent.RawBestTime is null,
        "Child/Parent publishes only the registered athlete's factual team result without relationship detail");

    var relay = publication.History.Single(item => item.ParticipantType == "Timed Relay");
    Assert(relay.EventCode == "3-6-3"
        && relay.ResultStatus == "Valid"
        && relay.OfficialBestTime == 20.000m,
        "Timed Relay factual history reaches the permanent public profile");

    var serialized = JsonSerializer.Serialize(publication);
    foreach (var forbiddenText in new[]
    {
        ""TeamCode"",
        ""RegisteredMemberCount"",
        ""HasExternalPartner"",
        ""Evidence"",
        ""RegisteredMembershipSha256"",
        ""CompetitionStateRevision"",
        ""CompetitionResultsRevision"",
        ""ResultRevision"",
        "External Parent Secret"
    })
    {
        Assert(!serialized.Contains(forbiddenText, StringComparison.OrdinalIgnoreCase),
            $"public team career payload excludes {forbiddenText}");
    }

    var properties = typeof(PublicTeamCareerPoint)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(item => item.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var forbidden in new[]
    {
        "TeamCode", "RegisteredMemberCount", "HasExternalPartner", "Evidence",
        "RegisteredMembershipSha256", "CompetitionStateRevision", "CompetitionResultsRevision", "ResultRevision",
        "ParticipantCode", "ParticipantName", "MemberParticipantCodes", "MemberNames", "PartnerName", "ParentName",
        "StackerId", "CompetitionId", "Placement", "Rank", "Medal", "Award", "Podium", "Record"
    })
    {
        Assert(!properties.Contains(forbidden), $"public team career point excludes {forbidden}");
    }

    a1.FirstName = "Changed";
    a1.LastName = "Registration";
    a1.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    var afterProfileMutation = await PublicProfile(controller, publicIdentity.NadiTrackId);
    var immutableDoubles = afterProfileMutation.TeamCareer?.History.Single(item =>
        item.ParticipantType == "Doubles" && item.Stage == "Finals")
        ?? throw new InvalidOperationException("Expected Doubles history after profile mutation.");
    Assert(immutableDoubles.OfficialBestTime == 8.500m,
        "current participant display-name mutation cannot rewrite team performance history");

    var duplicate = NewStacker(competition.Id, "A1-DUP", "Duplicate", "Link", now);
    db.Stackers.Add(duplicate);
    await db.SaveChangesAsync();
    db.StackerIdentityLinks.Add(NewLink(publicIdentity.Id, duplicate.Id, "TEST_SP4S_AMBIGUOUS", now));
    await db.SaveChangesAsync();

    var failClosedProfile = await PublicProfile(controller, publicIdentity.NadiTrackId);
    Assert(failClosedProfile.TeamCareer is not null
        && failClosedProfile.TeamCareer.History.Count == 0,
        "ambiguous same-competition identity link fails team history closed without taking down the public profile");
    Assert(failClosedProfile.DisplayName == "Ava Stacker",
        "team-history failure preserves the existing public identity profile");

    Assert((await controller.Get(privateIdentity.NadiTrackId, CancellationToken.None)).Result is NotFoundResult,
        "private permanent identity retains the public not-found boundary");
    Assert((await controller.Get("not-a-naditrack-id", CancellationToken.None)).Result is NotFoundResult,
        "malformed identity retains the public not-found boundary");

    Console.WriteLine("SP-4S public team career integration tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-4S primary failure: {ex}");
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
        Console.WriteLine($"SP-4S cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-4S cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-4S cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-4S tests passed but cleanup failed.", cleanupFailure);

static async Task<PublicSportStackerCareerProfile> PublicProfile(
    PublicStackerProfilesController controller,
    string nadiTrackId)
{
    var action = await controller.Get(nadiTrackId, CancellationToken.None);
    var ok = action.Result as OkObjectResult
        ?? throw new InvalidOperationException("Expected SP-4S public profile to return HTTP 200.");
    return ok.Value as PublicSportStackerCareerProfile
        ?? throw new InvalidOperationException("Expected SP-4S endpoint to return the public career contract.");
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
    DateTime now) => new()
{
    CompetitionId = competitionId,
    StackerCode = code,
    FirstName = firstName,
    LastName = lastName,
    Gender = "F",
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
    ResolutionNote = "SP-4S reviewed historical identity link fixture.",
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
    UpdatedBy = "sp4s-test"
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
