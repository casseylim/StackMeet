using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerCareerSp3aTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
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
    throw new InvalidOperationException("Refusing SP-3A integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerCareerSp3aTest_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-3A cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-3A LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
var options = new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(builder.ConnectionString).Options;
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
    var publicIdentity = new SportStackerIdentity
    {
        NadiTrackId = "NDT-ABCDEFG",
        WssaId = "WSSA-PRIVATE-REF",
        FirstName = "Ava",
        LastName = "Stacker",
        Gender = "F",
        BirthDate = new DateOnly(2008, 4, 5),
        Country = "MY",
        Club = "NADI Club",
        Region = "Selangor",
        Email = "ava.private@example.test",
        Phone = "60123456789",
        IsPublicProfile = true,
        CreatedAt = now,
        UpdatedAt = now
    };
    var privateIdentity = new SportStackerIdentity
    {
        NadiTrackId = "NDT-HJKMNPQ",
        FirstName = "Private",
        LastName = "Athlete",
        Gender = "M",
        BirthDate = new DateOnly(2007, 1, 2),
        Country = "MY",
        Email = "private@example.test",
        Phone = "60111111111",
        IsPublicProfile = false,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.SportStackerIdentities.AddRange(publicIdentity, privateIdentity);

    var closed = NewCompetition("SP3A-CLOSED", "Closed Championship", new DateOnly(2026, 1, 10), "Closed", true, null, now);
    var archived = NewCompetition("SP3A-ARCH", "Archived Championship", new DateOnly(2026, 3, 10), "Archived", true, now, now);
    var active = NewCompetition("SP3A-ACTIVE", "Active Championship", new DateOnly(2026, 4, 10), "Active", true, null, now);
    var privateCompetition = NewCompetition("SP3A-PRIVATE", "Private Championship", new DateOnly(2026, 5, 10), "Closed", false, null, now);
    var noResults = NewCompetition("SP3A-NORESULT", "Public Appearance Only", new DateOnly(2026, 6, 10), "Closed", true, null, now);
    db.Competitions.AddRange(closed, archived, active, privateCompetition, noResults);
    await db.SaveChangesAsync();

    var s1 = NewStacker(closed.Id, "S1", "Ava", "Stacker", now);
    var s2 = NewStacker(archived.Id, "S2", "Ava", "Stacker", now);
    var s3 = NewStacker(active.Id, "S3", "Ava", "Stacker", now);
    var s4 = NewStacker(privateCompetition.Id, "S4", "Ava", "Stacker", now);
    var s5 = NewStacker(noResults.Id, "S5", "Ava", "Stacker", now);
    var privateStacker = NewStacker(closed.Id, "PVT", "Private", "Athlete", now);
    db.Stackers.AddRange(s1, s2, s3, s4, s5, privateStacker);
    await db.SaveChangesAsync();

    db.StackerIdentityLinks.AddRange(
        Link(publicIdentity.Id, s1.Id, now),
        Link(publicIdentity.Id, s2.Id, now),
        Link(publicIdentity.Id, s3.Id, now),
        Link(publicIdentity.Id, s4.Id, now),
        Link(publicIdentity.Id, s5.Id, now),
        Link(privateIdentity.Id, privateStacker.Id, now));

    db.CompetitionResults.AddRange(
        Result(closed.Id, "Prelims", "Individual", "S1", "3-3-3", [5.100m, 4.900m, 999m], 0m, now),
        Result(closed.Id, "Finals", "Individual", "S1", "3-3-3", [4.800m, 4.700m, 4.750m], 0.100m, now),
        Result(closed.Id, "Finals", "Individual", "S1", "3-6-3", [6.500m, 6.400m, 6.300m], 0m, now),
        Result(closed.Id, "Prelims", "Individual", "S1", "Cycle", [999m, 999m, 999m], 0m, now),
        Result(closed.Id, "Finals", "Doubles", "S1", "3-3-3", [1.000m], 0m, now),
        Result(archived.Id, "Prelims", "Individual", "S2", "3-3-3", [4.850m], 0m, now),
        Result(archived.Id, "Finals", "Individual", "S2", "Cycle", [7.200m, 7.100m, 7.000m], 0.050m, now),
        MalformedResult(archived.Id, "Prelims", "S2", "Cycle", now),
        Result(active.Id, "Finals", "Individual", "S3", "3-3-3", [3.000m], 0m, now),
        Result(privateCompetition.Id, "Finals", "Individual", "S4", "3-3-3", [2.000m], 0m, now),
        Result(closed.Id, "Finals", "Individual", "PVT", "3-3-3", [1.500m], 0m, now));
    await db.SaveChangesAsync();

    var service = new SportStackerCareerProfileService(db);
    var profile = await service.GetPublicAsync("ndt-abcdefg")
        ?? throw new InvalidOperationException("Expected opted-in public profile.");

    Assert(profile.NadiTrackId == "NDT-ABCDEFG", "public lookup normalizes NADITrack ID");
    Assert(profile.DisplayName == "Ava Stacker" && profile.Country == "MY", "public profile exposes safe identity summary");
    Assert(profile.Club == "NADI Club" && profile.Region == "Selangor", "public profile exposes configured public club and region");
    Assert(profile.CompetitionCount == 3, "only finalized publicly-listed competitions count as career appearances");
    Assert(profile.FirstCompetitionDate == new DateOnly(2026, 1, 10), "first finalized public appearance date");
    Assert(profile.LatestCompetitionDate == new DateOnly(2026, 6, 10), "appearance without result still counts toward latest competition date");
    Assert(profile.PersonalBests.Select(item => item.EventCode).SequenceEqual(new[] { "3-3-3", "3-6-3", "Cycle" }), "personal best event order is stable");

    var threeThreeThree = profile.PersonalBests.Single(item => item.EventCode == "3-3-3");
    Assert(threeThreeThree.OfficialTime == 4.800m
        && threeThreeThree.RawBestTime == 4.700m
        && threeThreeThree.AppliedPenalty == 0.100m, "personal best uses best valid attempt plus applicable penalty");
    Assert(threeThreeThree.CompetitionKey == "SP3A-CLOSED"
        && threeThreeThree.Stage == "Finals", "personal best retains competition and stage provenance");

    var threeSixThree = profile.PersonalBests.Single(item => item.EventCode == "3-6-3");
    Assert(threeSixThree.OfficialTime == 6.300m, "3-6-3 personal best is calculated from finalized individual results");

    var cycle = profile.PersonalBests.Single(item => item.EventCode == "Cycle");
    Assert(cycle.OfficialTime == 7.050m
        && cycle.RawBestTime == 7.000m
        && cycle.AppliedPenalty == 0.050m, "scratch and malformed Cycle rows do not displace a valid PB");

    Assert(profile.PersonalBests.All(item => item.OfficialTime > 2.000m), "faster active/private competition results cannot become public career PBs");
    Assert(await service.GetPublicAsync("NDT-HJKMNPQ") is null, "private profile is indistinguishable from not found");
    Assert(await service.GetPublicAsync("NDT-RSTUVWX") is null, "unknown valid NADITrack ID returns no public profile");
    Assert(await service.GetPublicAsync("not-an-id") is null, "malformed NADITrack ID returns no public profile");

    var publicProperties = typeof(PublicSportStackerCareerProfile).GetProperties().Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
    foreach (var forbidden in new[] { "BirthDate", "Email", "Phone", "Gender", "WssaId" })
    {
        Assert(!publicProperties.Contains(forbidden), $"public career contract excludes {forbidden}");
    }

    Console.WriteLine("SP-3A public career profile integration tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-3A primary failure: {ex}");
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
        var escapedName = databaseName.Replace("]", "]]");
        command.CommandText = $"IF DB_ID(@databaseName) IS NOT NULL BEGIN ALTER DATABASE [{escapedName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{escapedName}]; END";
        command.Parameters.AddWithValue("@databaseName", databaseName);
        await command.ExecuteNonQueryAsync(cleanupTimeout.Token);
        Console.WriteLine($"SP-3A cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-3A cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-3A cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-3A tests passed but cleanup failed.", cleanupFailure);

static Competition NewCompetition(string key, string name, DateOnly date, string status, bool publiclyListed, DateTime? archivedAt, DateTime now) => new()
{
    CompetitionCode = key,
    CompetitionKey = key,
    CompetitionName = name,
    Venue = "LocalDB",
    StartDate = date,
    EndDate = date,
    Status = status,
    IsPubliclyListed = publiclyListed,
    ArchivedAt = archivedAt,
    CreatedAt = now,
    UpdatedAt = now
};

static Stacker NewStacker(int competitionId, string code, string firstName, string lastName, DateTime now) => new()
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

static StackerIdentityLink Link(long identityId, int stackerId, DateTime now) => new()
{
    SportStackerIdentityId = identityId,
    StackerId = stackerId,
    MatchMethod = StackerIdentityMatchMethod.Manual,
    ResolutionReasonCode = "TEST_SP3A_LINK",
    ResolutionNote = "SP-3A read-model fixture.",
    LinkedAt = now
};

static CompetitionResult Result(int competitionId, string stage, string type, string participantCode, string eventCode, decimal[] attempts, decimal penalty, DateTime now) => new()
{
    CompetitionId = competitionId,
    Stage = stage,
    ParticipantType = type,
    ParticipantCode = participantCode,
    EventCode = eventCode,
    AttemptsJson = JsonSerializer.Serialize(attempts),
    Penalty = penalty,
    Revision = 1,
    CreatedAt = now,
    UpdatedAt = now
};

static CompetitionResult MalformedResult(int competitionId, string stage, string participantCode, string eventCode, DateTime now) => new()
{
    CompetitionId = competitionId,
    Stage = stage,
    ParticipantType = "Individual",
    ParticipantCode = participantCode,
    EventCode = eventCode,
    AttemptsJson = "{malformed",
    Penalty = 0m,
    Revision = 1,
    CreatedAt = now,
    UpdatedAt = now
};

static void Assert(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"Failed scenario: {name}");
    Console.WriteLine($"PASS {name}");
}
