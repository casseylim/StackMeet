using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerFinalsCareerSp4dTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
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
    throw new InvalidOperationException("Refusing SP-4D integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerFinalsCareerSp4dTest_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-4D cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-4D LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
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
        FirstName = "Finals",
        LastName = "Stacker",
        Country = "MY",
        IsPublicProfile = true,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.SportStackerIdentities.Add(identity);

    var c1 = Competition("SP4D-1", "January Finals", new DateOnly(2026, 1, 10), "Closed", true, null, now);
    var c2 = Competition("SP4D-2", "February Finals", new DateOnly(2026, 2, 10), "Closed", true, null, now);
    var c3 = Competition("SP4D-3", "March Finals", new DateOnly(2026, 3, 10), "Archived", true, now, now);
    var c4 = Competition("SP4D-4", "April Finals", new DateOnly(2026, 4, 10), "Closed", true, null, now);
    var prelimOnly = Competition("SP4D-PRELIM", "Prelims Only", new DateOnly(2026, 5, 10), "Closed", true, null, now);
    var active = Competition("SP4D-ACTIVE", "Active Meet", new DateOnly(2026, 6, 10), "Active", true, null, now);
    var hidden = Competition("SP4D-HIDDEN", "Hidden Meet", new DateOnly(2026, 7, 10), "Closed", false, null, now);
    db.Competitions.AddRange(c1, c2, c3, c4, prelimOnly, active, hidden);
    await db.SaveChangesAsync();

    var s1 = Stacker(c1.Id, "S1", now);
    var s2 = Stacker(c2.Id, "S2", now);
    var s3 = Stacker(c3.Id, "S3", now);
    var s4 = Stacker(c4.Id, "S4", now);
    var sp = Stacker(prelimOnly.Id, "SP", now);
    var sa = Stacker(active.Id, "SA", now);
    var sh = Stacker(hidden.Id, "SH", now);
    db.Stackers.AddRange(s1, s2, s3, s4, sp, sa, sh);
    await db.SaveChangesAsync();

    db.StackerIdentityLinks.AddRange(
        Link(identity.Id, s1.Id, now),
        Link(identity.Id, s2.Id, now),
        Link(identity.Id, s3.Id, now),
        Link(identity.Id, s4.Id, now),
        Link(identity.Id, sp.Id, now),
        Link(identity.Id, sa.Id, now),
        Link(identity.Id, sh.Id, now));

    db.CompetitionResults.AddRange(
        Result(c1.Id, "Finals", "Individual", "S1", "3-3-3", [5.200m, 5.000m, 5.100m], 0m, now),
        Result(c1.Id, "Finals", "Individual", "S1", "Cycle", [999m, 999m, 999m], 999m, now),
        Result(c1.Id, "Prelims", "Individual", "S1", "3-6-3", [7.000m], 0m, now),

        Result(c2.Id, "Finals", "Individual", "S2", "3-3-3", [], 0m, now),
        Result(c2.Id, "Finals", "Individual", "S2", "3-6-3", [6.500m], 0m, now),
        Result(c2.Id, "Finals", "Individual", "S2", "Cycle", [8.000m], 0.100m, now),
        Result(c2.Id, "Finals", "Doubles", "S2", "Cycle", [1.000m], 0m, now),

        Result(c3.Id, "Finals", "Individual", "S3", "3-3-3", [0m, 0m], 0m, now),
        RawResult(c3.Id, "Finals", "Individual", "S3", "Cycle", "{malformed", 0m, now),

        Result(c4.Id, "Finals", "Individual", "S4", "3-3-3", [4.700m], 0.100m, now),
        Result(c4.Id, "Finals", "Individual", "S4", "3-6-3", [6.200m], 0m, now),
        Result(c4.Id, "Finals", "Individual", "S4", "Cycle", [7.950m], 0m, now),

        Result(prelimOnly.Id, "Prelims", "Individual", "SP", "3-3-3", [4.000m], 0m, now),
        Result(active.Id, "Finals", "Individual", "SA", "3-3-3", [2.000m], 0m, now),
        Result(hidden.Id, "Finals", "Individual", "SH", "3-3-3", [1.500m], 0m, now));
    await db.SaveChangesAsync();

    var service = new SportStackerCareerProfileService(db);
    var profile = await service.GetPublicAsync("NDT-ABCDEFG")
        ?? throw new InvalidOperationException("Expected public SP-4D profile.");

    Assert(profile.FinalsCareer.Select(item => item.EventCode).SequenceEqual(new[] { "3-3-3", "3-6-3", "Cycle" }),
        "Finals career follows canonical event order");

    var three = profile.FinalsCareer.Single(item => item.EventCode == "3-3-3");
    Assert(three.FinalsAppearanceCount == 4 && three.ValidFinalsCount == 2,
        "3-3-3 counts finalized public Finals rows and valid finishes separately");
    Assert(three.FirstFinalDate == new DateOnly(2026, 1, 10) && three.LatestFinalDate == new DateOnly(2026, 4, 10),
        "3-3-3 publishes the first and latest finalized Finals dates");
    Assert(three.BestFinalOfficialTime == 4.800m && three.BestCompetitionKey == "SP4D-4",
        "best Finals performance uses official time including an applicable penalty");
    Assert(three.History.Select(item => item.Status).SequenceEqual(new[] { "Valid", "Missing", "Invalid", "Valid" }),
        "Finals history retains valid, missing and invalid statuses chronologically");
    Assert(three.History.Last().RawBestTime == 4.700m && three.History.Last().AppliedPenalty == 0.100m,
        "valid Finals history retains raw time and penalty provenance");

    var six = profile.FinalsCareer.Single(item => item.EventCode == "3-6-3");
    Assert(six.FinalsAppearanceCount == 2 && six.ValidFinalsCount == 2,
        "Prelims-only 3-6-3 does not enter Finals career");
    Assert(six.BestFinalOfficialTime == 6.200m && six.BestCompetitionKey == "SP4D-4",
        "best 3-6-3 Finals performance is selected across finalized competitions");

    var cycle = profile.FinalsCareer.Single(item => item.EventCode == "Cycle");
    Assert(cycle.FinalsAppearanceCount == 4 && cycle.ValidFinalsCount == 2,
        "Cycle Finals career excludes Doubles while retaining Individual scratch and invalid rows");
    Assert(cycle.History.Select(item => item.Status).SequenceEqual(new[] { "Scratch", "Valid", "Invalid", "Valid" }),
        "Cycle Finals history classifies scratch and malformed JSON without fabricating times");
    Assert(cycle.History[0].OfficialTime is null && cycle.History[0].RawBestTime is null,
        "scratch Finals does not publish a fabricated official or raw time");
    Assert(cycle.History[2].OfficialTime is null && cycle.History[2].RawBestTime is null,
        "malformed Finals does not publish a fabricated official or raw time");
    Assert(cycle.BestFinalOfficialTime == 7.950m && cycle.BestCompetitionKey == "SP4D-4",
        "Cycle best Finals performance uses the fastest valid official time");

    Assert(profile.FinalsCareer.SelectMany(item => item.History).All(item => item.CompetitionKey != "SP4D-ACTIVE" && item.CompetitionKey != "SP4D-HIDDEN"),
        "active and non-public competitions cannot enter Finals career");
    Assert(profile.TournamentHistory.Count == 5,
        "SP-4A tournament appearances remain unchanged by SP-4D Finals aggregation");
    Assert(profile.PersonalBests.Single(item => item.EventCode == "3-3-3").OfficialTime == 4.000m,
        "SP-3A personal best semantics remain unchanged and may still come from Prelims");

    var publicFinalsTypes = new[] { typeof(SportStackerEventFinalsSummary), typeof(SportStackerFinalsHistoryPoint) };
    foreach (var type in publicFinalsTypes)
    {
        var properties = type.GetProperties().Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var forbidden in new[] { "BirthDate", "Email", "Phone", "Gender", "WssaId", "StackerId", "CompetitionId", "Rank", "Placement", "Medal", "Award" })
        {
            Assert(!properties.Contains(forbidden), $"{type.Name} excludes {forbidden}");
        }
    }

    Console.WriteLine("SP-4D Finals career integration tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-4D primary failure: {ex}");
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
        Console.WriteLine($"SP-4D cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-4D cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-4D cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-4D tests passed but cleanup failed.", cleanupFailure);

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
    FirstName = "Finals",
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
    ResolutionReasonCode = "TEST_SP4D_LINK",
    ResolutionNote = "SP-4D Finals career fixture.",
    LinkedAt = now
};

static CompetitionResult Result(int competitionId, string stage, string type, string participantCode, string eventCode, decimal[] attempts, decimal penalty, DateTime now) =>
    RawResult(competitionId, stage, type, participantCode, eventCode, JsonSerializer.Serialize(attempts), penalty, now);

static CompetitionResult RawResult(int competitionId, string stage, string type, string participantCode, string eventCode, string attemptsJson, decimal penalty, DateTime now) => new()
{
    CompetitionId = competitionId,
    Stage = stage,
    ParticipantType = type,
    ParticipantCode = participantCode,
    EventCode = eventCode,
    AttemptsJson = attemptsJson,
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
