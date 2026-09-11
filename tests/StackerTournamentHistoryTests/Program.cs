using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerTournamentSp4aTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
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
    throw new InvalidOperationException("Refusing SP-4A integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerTournamentSp4aTest_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-4A cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-4A LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
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
    var identity = new SportStackerIdentity
    {
        NadiTrackId = "NDT-ABCDEFG",
        FirstName = "Ava",
        LastName = "Stacker",
        Gender = "F",
        BirthDate = new DateOnly(2008, 4, 5),
        Country = "MY",
        Club = "NADI Club",
        Region = "Selangor",
        Email = "private@example.test",
        Phone = "60123456789",
        IsPublicProfile = true,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.SportStackerIdentities.Add(identity);

    var closed = NewCompetition("SP4A-CLOSED", "Closed Championship", new DateOnly(2026, 1, 10), "Closed", true, null, now);
    var archived = NewCompetition("SP4A-ARCH", "Archived Championship", new DateOnly(2026, 3, 10), "Archived", true, now, now);
    var active = NewCompetition("SP4A-ACTIVE", "Active Championship", new DateOnly(2026, 4, 10), "Active", true, null, now);
    var privateCompetition = NewCompetition("SP4A-PRIVATE", "Private Championship", new DateOnly(2026, 5, 10), "Closed", false, null, now);
    var appearanceOnly = NewCompetition("SP4A-NORESULT", "Appearance Only", new DateOnly(2026, 6, 10), "Closed", true, null, now);
    db.Competitions.AddRange(closed, archived, active, privateCompetition, appearanceOnly);
    await db.SaveChangesAsync();

    var closedStacker = NewStacker(closed.Id, "S1", now);
    var archivedStacker = NewStacker(archived.Id, "S2", now);
    var activeStacker = NewStacker(active.Id, "S3", now);
    var privateStacker = NewStacker(privateCompetition.Id, "S4", now);
    var appearanceStacker = NewStacker(appearanceOnly.Id, "S5", now);
    db.Stackers.AddRange(closedStacker, archivedStacker, activeStacker, privateStacker, appearanceStacker);
    await db.SaveChangesAsync();

    db.StackerIdentityLinks.AddRange(
        Link(identity.Id, closedStacker.Id, now),
        Link(identity.Id, archivedStacker.Id, now),
        Link(identity.Id, activeStacker.Id, now),
        Link(identity.Id, privateStacker.Id, now),
        Link(identity.Id, appearanceStacker.Id, now));

    db.CompetitionResults.AddRange(
        Result(closed.Id, "Prelims", "Individual", "S1", "3-3-3", [5.100m, 4.900m, 999m], 0m, now),
        Result(closed.Id, "Finals", "Individual", "S1", "3-3-3", [4.800m, 4.700m, 4.750m], 0.100m, now),
        Result(closed.Id, "Finals", "Individual", "S1", "3-6-3", [6.500m, 6.400m, 6.300m], 0m, now),
        Result(closed.Id, "Finals", "Doubles", "S1", "Cycle", [1.000m], 0m, now),
        Result(archived.Id, "Prelims", "Individual", "S2", "3-3-3", [4.850m], 0m, now),
        Result(archived.Id, "Finals", "Individual", "S2", "Cycle", [7.200m, 7.100m, 7.000m], 0.050m, now),
        MalformedResult(archived.Id, "Prelims", "S2", "Cycle", now),
        Result(active.Id, "Finals", "Individual", "S3", "3-3-3", [3.000m], 0m, now),
        Result(privateCompetition.Id, "Finals", "Individual", "S4", "3-3-3", [2.000m], 0m, now));
    await db.SaveChangesAsync();

    var service = new SportStackerCareerProfileService(db);
    var profile = await service.GetPublicAsync("ndt-abcdefg")
        ?? throw new InvalidOperationException("Expected opted-in public profile.");

    Assert(profile.CompetitionCount == 3, "SP-4A history uses the same finalized public appearance boundary");
    Assert(profile.TournamentHistory.Count == 3, "SP-4A history contains every finalized public appearance");
    Assert(profile.TournamentHistory.Select(item => item.CompetitionKey)
        .SequenceEqual(new[] { "SP4A-NORESULT", "SP4A-ARCH", "SP4A-CLOSED" }),
        "SP-4A history is reverse chronological and excludes active/private competitions");

    var appearance = profile.TournamentHistory[0];
    Assert(appearance.Performances.Count == 0,
        "a finalized public appearance remains in history even without a valid individual result");

    var archivedHistory = profile.TournamentHistory.Single(item => item.CompetitionKey == "SP4A-ARCH");
    Assert(archivedHistory.Performances.Select(item => item.EventCode)
        .SequenceEqual(new[] { "3-3-3", "Cycle" }),
        "SP-4A tournament event ordering is stable");
    Assert(archivedHistory.Performances.Single(item => item.EventCode == "3-3-3").OfficialTime == 4.850m,
        "SP-4A retains a valid prelim performance when it is the tournament best");
    var archivedCycle = archivedHistory.Performances.Single(item => item.EventCode == "Cycle");
    Assert(archivedCycle.OfficialTime == 7.050m
        && archivedCycle.RawBestTime == 7.000m
        && archivedCycle.AppliedPenalty == 0.050m
        && archivedCycle.Stage == "Finals",
        "SP-4A tournament performance preserves timing and stage provenance");

    var closedHistory = profile.TournamentHistory.Single(item => item.CompetitionKey == "SP4A-CLOSED");
    var closedThree = closedHistory.Performances.Single(item => item.EventCode == "3-3-3");
    Assert(closedThree.OfficialTime == 4.800m && closedThree.Stage == "Finals",
        "SP-4A selects the best valid individual performance per event within a tournament");
    Assert(closedHistory.Performances.All(item => item.EventCode != "Cycle"),
        "Doubles results cannot enter individual tournament history");

    Assert(profile.TournamentHistory.All(item => item.CompetitionKey != "SP4A-ACTIVE" && item.CompetitionKey != "SP4A-PRIVATE"),
        "active and non-public competitions cannot leak into SP-4A history");
    Assert(profile.PersonalBests.Single(item => item.EventCode == "3-3-3").OfficialTime == 4.800m,
        "SP-4A does not alter the existing career personal-best semantics");

    foreach (var type in new[]
    {
        typeof(PublicSportStackerCareerProfile),
        typeof(SportStackerTournamentHistory),
        typeof(SportStackerTournamentPerformance),
        typeof(SportStackerPersonalBest)
    })
    {
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var forbidden in new[] { "BirthDate", "Email", "Phone", "Gender", "WssaId" })
        {
            Assert(!properties.Contains(forbidden), $"SP-4A public contract {type.Name} excludes {forbidden}");
        }
    }

    Console.WriteLine("SP-4A tournament history integration tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-4A primary failure: {ex}");
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
        Console.WriteLine($"SP-4A cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-4A cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-4A cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-4A tests passed but cleanup failed.", cleanupFailure);

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

static Stacker NewStacker(int competitionId, string code, DateTime now) => new()
{
    CompetitionId = competitionId,
    StackerCode = code,
    FirstName = "Ava",
    LastName = "Stacker",
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
    ResolutionReasonCode = "TEST_SP4A_LINK",
    ResolutionNote = "SP-4A tournament-history fixture.",
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
