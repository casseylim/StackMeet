using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerPersonalRecordsSp5aTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
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
    throw new InvalidOperationException("Refusing SP-5A integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerPersonalRecordsSp5aTest_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-5A cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-5A LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
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
        FirstName = "Record",
        LastName = "Stacker",
        Country = "MY",
        IsPublicProfile = true,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.SportStackerIdentities.Add(identity);

    var c1 = Competition("SP5A-1", "Opening Meet", new DateOnly(2026, 1, 10), "Closed", true, null, now);
    var c2 = Competition("SP5A-2", "Second Meet", new DateOnly(2026, 2, 10), "Closed", true, null, now);
    var c3 = Competition("SP5A-3", "Third Meet", new DateOnly(2026, 3, 10), "Archived", true, now, now);
    var c4 = Competition("SP5A-4", "Fourth Meet", new DateOnly(2026, 4, 10), "Closed", true, null, now);
    var active = Competition("SP5A-ACTIVE", "Active Meet", new DateOnly(2026, 5, 10), "Active", true, null, now);
    var hidden = Competition("SP5A-HIDDEN", "Hidden Meet", new DateOnly(2026, 6, 10), "Closed", false, null, now);
    db.Competitions.AddRange(c1, c2, c3, c4, active, hidden);
    await db.SaveChangesAsync();

    var s1 = Stacker(c1.Id, "S1", now);
    var s2 = Stacker(c2.Id, "S2", now);
    var s3 = Stacker(c3.Id, "S3", now);
    var s4 = Stacker(c4.Id, "S4", now);
    var sa = Stacker(active.Id, "SA", now);
    var sh = Stacker(hidden.Id, "SH", now);
    db.Stackers.AddRange(s1, s2, s3, s4, sa, sh);
    await db.SaveChangesAsync();

    db.StackerIdentityLinks.AddRange(
        Link(identity.Id, s1.Id, now),
        Link(identity.Id, s2.Id, now),
        Link(identity.Id, s3.Id, now),
        Link(identity.Id, s4.Id, now),
        Link(identity.Id, sa.Id, now),
        Link(identity.Id, sh.Id, now));

    db.CompetitionResults.AddRange(
        Result(c1.Id, "Finals", "Individual", "S1", "3-3-3", [5.000m], 0m, now),
        Result(c1.Id, "Finals", "Individual", "S1", "Cycle", [8.000m], 0m, now),
        Result(c2.Id, "Prelims", "Individual", "S2", "3-3-3", [5.200m], 0m, now),
        Result(c2.Id, "Finals", "Individual", "S2", "3-3-3", [5.100m], 0m, now),
        Result(c2.Id, "Finals", "Doubles", "S2", "3-3-3", [1.000m], 0m, now),
        Result(c3.Id, "Finals", "Individual", "S3", "3-3-3", [4.800m], 0.100m, now),
        Result(c3.Id, "Prelims", "Individual", "S3", "Cycle", [7.700m], 0m, now),
        Result(c4.Id, "Finals", "Individual", "S4", "3-3-3", [4.900m], 0m, now),
        Result(c4.Id, "Finals", "Individual", "S4", "Cycle", [7.900m], 0m, now),
        Result(active.Id, "Finals", "Individual", "SA", "3-3-3", [2.000m], 0m, now),
        Result(hidden.Id, "Finals", "Individual", "SH", "3-3-3", [1.500m], 0m, now));
    await db.SaveChangesAsync();

    var service = new SportStackerCareerProfileService(db);
    var profile = await service.GetPublicAsync("NDT-ABCDEFG")
        ?? throw new InvalidOperationException("Expected public SP-5A profile.");

    Assert(profile.PersonalRecords.Select(item => item.EventCode).SequenceEqual(new[] { "3-3-3", "Cycle" }),
        "personal record event order follows canonical Sport Stacking order");

    var three = profile.PersonalRecords.Single(item => item.EventCode == "3-3-3");
    Assert(three.FirstRecordedPersonalBest == 5.000m, "first finalized 3-3-3 performance establishes first recorded PB");
    Assert(three.CurrentPersonalBest == 4.900m, "current 3-3-3 PB follows strict career progression");
    Assert(three.TotalImprovement == 0.100m, "3-3-3 total improvement is first PB minus current PB");
    Assert(three.PersonalBestMilestoneCount == 2, "slower result and later tie do not create fake PB milestones");
    Assert(three.FinalizedPerformanceCount == 4, "all finalized public tournament-best 3-3-3 performances remain counted");
    Assert(three.FirstPersonalBestCompetitionKey == "SP5A-1" && three.FirstPersonalBestDate == new DateOnly(2026, 1, 10),
        "first PB provenance is retained");
    Assert(three.CurrentPersonalBestCompetitionKey == "SP5A-3" && three.CurrentPersonalBestDate == new DateOnly(2026, 3, 10),
        "current PB provenance points to the strict-improvement milestone, not a later tie");
    Assert(three.CurrentPersonalBestStage == "Finals", "current PB retains stage provenance");

    var cycle = profile.PersonalRecords.Single(item => item.EventCode == "Cycle");
    Assert(cycle.FirstRecordedPersonalBest == 8.000m && cycle.CurrentPersonalBest == 7.700m,
        "Cycle first/current personal records are correct");
    Assert(cycle.TotalImprovement == 0.300m && cycle.PersonalBestMilestoneCount == 2,
        "Cycle total improvement and milestone count are correct");
    Assert(cycle.FinalizedPerformanceCount == 3, "Cycle finalized-performance count excludes absent tournaments");
    Assert(cycle.CurrentPersonalBestCompetitionKey == "SP5A-3" && cycle.CurrentPersonalBestStage == "Prelims",
        "personal record provenance may correctly come from Prelims");

    Assert(profile.PersonalBests.Single(item => item.EventCode == "3-3-3").OfficialTime == three.CurrentPersonalBest,
        "existing PersonalBests contract remains aligned with new PersonalRecords summary");
    Assert(profile.CareerProgression.Single(item => item.EventCode == "3-3-3").Points.Count == three.FinalizedPerformanceCount,
        "personal records are derived from the same server-owned career progression");
    Assert(profile.PersonalRecords.All(item => item.CurrentPersonalBest > 2m),
        "active/private competitions cannot contaminate public personal records");

    var recordProperties = typeof(SportStackerPersonalRecordAchievement).GetProperties()
        .Select(item => item.Name)
        .ToHashSet(StringComparer.Ordinal);
    foreach (var forbidden in new[] { "BirthDate", "Email", "Phone", "Gender", "WssaId", "StackerId", "CompetitionId" })
    {
        Assert(!recordProperties.Contains(forbidden), $"public personal-record contract excludes {forbidden}");
    }

    Console.WriteLine("SP-5A personal record achievement integration tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-5A primary failure: {ex}");
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
        Console.WriteLine($"SP-5A cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-5A cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-5A cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-5A tests passed but cleanup failed.", cleanupFailure);

static Competition Competition(string key, string name, DateOnly date, string status, bool publiclyListed, DateTime? archivedAt, DateTime now) => new()
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

static Stacker Stacker(int competitionId, string code, DateTime now) => new()
{
    CompetitionId = competitionId,
    StackerCode = code,
    FirstName = "Record",
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
    ResolutionReasonCode = "TEST_SP5A_LINK",
    ResolutionNote = "SP-5A personal-record fixture.",
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

static void Assert(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"Failed scenario: {name}");
    Console.WriteLine($"PASS {name}");
}
