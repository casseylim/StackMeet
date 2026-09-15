using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Ranking;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerFinalsPlacementSp4kTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
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
    throw new InvalidOperationException("Refusing SP-4K integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerFinalsPlacementSp4kTest_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-4K cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-4K LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
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

    var governance = new FinalsRankingGovernanceService(db);
    var projector = new FinalsHistoricalPlacementProjectionService(db);
    var now = DateTime.UtcNow;

    var competition = NewCompetition("SP4KMAIN", now, resultsRevision: 5);
    db.Competitions.Add(competition);
    await db.SaveChangesAsync();

    var state = NewState(
        competition.CompetitionKey,
        """
        {
          "stackers": [
            { "id": "1.1", "name": "Alpha",   "gender": "M", "division": "Open", "special": "No" },
            { "id": "1.2", "name": "Beta",    "gender": "M", "division": "Open", "special": "No" },
            { "id": "1.3", "name": "Gamma",   "gender": "F", "division": "Open", "special": "No" },
            { "id": "1.4", "name": "Special", "gender": "M", "division": "Open", "special": "Yes" },
            { "id": "1.5", "name": "Scratch", "gender": "M", "division": "Open", "special": "No" }
          ]
        }
        """,
        revision: 3,
        now);
    db.CompetitionStates.Add(state);

    var alpha = NewResult(competition.Id, "1.1", "Cycle", "[5.0,5.2,5.3]", 0.5m, revision: 1, now);
    var beta = NewResult(competition.Id, "1.2", "Cycle", "[5.1,5.2,5.3]", 0m, revision: 2, now);
    var gamma = NewResult(competition.Id, "1.3", "Cycle", "[5.1,5.2,5.3]", 0m, revision: 3, now);
    var special = NewResult(competition.Id, "1.4", "Cycle", "[4.0,4.1,4.2]", 0m, revision: 4, now);
    var scratch = NewResult(competition.Id, "1.5", "Cycle", "[3.0,3.1,3.2]", 999m, revision: 5, now);
    db.CompetitionResults.AddRange(alpha, beta, gamma, special, scratch);
    await db.SaveChangesAsync();

    await governance.SelectRuleVersionAsync(competition.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 3001);
    competition.Status = "Closed";
    competition.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    var certified = await governance.CertifyGovernedV2SnapshotAsync(competition.Id, 3002);
    Assert(certified.HasSnapshot, "governed v2 source evidence certified before projection");

    var normalAll = await projector.ProjectAsync(
        competition.Id,
        new FinalsHistoricalPlacementScope("Individual", "Open", "Cycle", "normal", "all"));
    Assert(normalAll.ProjectionVersion == FinalsHistoricalPlacementProjectionService.ProjectionVersion, "projection contract version is explicit");
    Assert(normalAll.SnapshotSchemaVersion == FinalsRankingGovernanceService.GovernedV2SnapshotSchemaVersion, "projection requires source-v2 snapshot");
    Assert(normalAll.OperatorContractVersion == FinalsRankingCertificationReadinessService.OperatorContractVersion, "projection carries reviewed operator-contract provenance");
    Assert(normalAll.Rows.Count == 4, "normal/all scope excludes Special participant before ranking");
    Assert(Row(normalAll, "1.2").Rank == 1 && Row(normalAll, "1.3").Rank == 1, "equal complete v2 tie keys share rank 1");
    Assert(Row(normalAll, "1.2").SharesRank && Row(normalAll, "1.3").SharesRank, "tie membership is factual for both tied rows");
    Assert(Row(normalAll, "1.1").Rank == 3, "competition ranking leaves rank gap after tie");
    Assert(Row(normalAll, "1.1").OfficialBestTime == 5.5m, "finite penalty contributes to governed v2 official-best comparator");
    Assert(Row(normalAll, "1.5").ResultStatus == "scratch" && Row(normalAll, "1.5").Rank is null, "999 penalty overrides valid attempts and receives no rank");

    var normalMale = await projector.ProjectAsync(
        competition.Id,
        new FinalsHistoricalPlacementScope("individual", "Open", "cycle", "NORMAL", "m"));
    Assert(normalMale.Rows.Count == 3, "gender filter is applied before ranking");
    Assert(Row(normalMale, "1.2").Rank == 1 && Row(normalMale, "1.1").Rank == 2, "male-only scope produces its own governed placement cohort");

    var mixedAll = await projector.ProjectAsync(
        competition.Id,
        new FinalsHistoricalPlacementScope("Individual", "Open", "Cycle", "mixed", "all"));
    Assert(mixedAll.Rows.Count == 5, "mixed scope includes normal and special participants before ranking");
    Assert(Row(mixedAll, "1.4").Rank == 1, "special participant participates in mixed cohort");
    Assert(Row(mixedAll, "1.2").Rank == 2 && Row(mixedAll, "1.3").Rank == 2, "mixed cohort preserves equal rank after faster special result");
    Assert(Row(mixedAll, "1.1").Rank == 4, "mixed cohort competition ranking preserves the tie gap");

    var specialOnly = await projector.ProjectAsync(
        competition.Id,
        new FinalsHistoricalPlacementScope("Individual", "Open", "Cycle", "special", "all"));
    Assert(specialOnly.Rows.Count == 1 && Row(specialOnly, "1.4").Rank == 1, "special category is an independent pre-ranking scope");

    // Mutate current/live source tables after certification. Historical projection must remain
    // unchanged because SP-4K is allowed to consume only the immutable certified snapshot.
    state.JsonData = """
        {
          "stackers": [
            { "id": "1.1", "name": "Alpha changed", "gender": "F", "division": "Changed", "special": "Yes" },
            { "id": "1.2", "name": "Beta changed", "gender": "F", "division": "Changed", "special": "Yes" }
          ]
        }
        """;
    state.UpdatedAt = DateTime.UtcNow;
    special.AttemptsJson = "[1.0,1.1,1.2]";
    special.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    var afterLiveMutation = await projector.ProjectAsync(
        competition.Id,
        new FinalsHistoricalPlacementScope("Individual", "Open", "Cycle", "mixed", "all"));
    Assert(afterLiveMutation.Rows.Count == 5, "current CompetitionState mutation cannot alter historical cohort");
    Assert(Row(afterLiveMutation, "1.4").Rank == 1 && Row(afterLiveMutation, "1.4").OfficialBestTime == 4.0m,
        "current CompetitionResult mutation cannot alter historical rank or official time");
    Assert(Row(afterLiveMutation, "1.1").ParticipantName == "Alpha", "historical participant name comes from immutable competition-time state");

    await AssertThrowsContainsAsync(
        () => projector.ProjectAsync(competition.Id, new FinalsHistoricalPlacementScope("Individual", "all", "Cycle", "normal", "all")),
        FinalsHistoricalPlacementProjectionBlockers.ScopeInvalid,
        "bare all-division rank is forbidden");
    await AssertThrowsContainsAsync(
        () => projector.ProjectAsync(competition.Id, new FinalsHistoricalPlacementScope("Doubles", "Open", "Cycle", "mixed", "all")),
        FinalsHistoricalPlacementProjectionBlockers.ScopeInvalid,
        "SP-4K Stacker Identity projection remains Individual-only");

    var legacy = NewCompetition("SP4KLEGACY", now.AddMinutes(1), resultsRevision: 0);
    db.Competitions.Add(legacy);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(legacy.CompetitionKey, "{\"stackers\":[]}", 1, now));
    await db.SaveChangesAsync();
    await governance.SelectRuleVersionAsync(legacy.Id, FinalsRankingRuleVersions.LegacyFinalsV1, 3010);
    legacy.Status = "Closed";
    await db.SaveChangesAsync();
    await governance.CaptureFinalizedSnapshotAsync(legacy.Id, 3011);
    await AssertThrowsContainsAsync(
        () => projector.ProjectAsync(legacy.Id, new FinalsHistoricalPlacementScope("Individual", "Open", "Cycle", "normal", "all")),
        FinalsHistoricalPlacementProjectionBlockers.SnapshotSchemaUnsupported,
        "legacy source-v1 evidence fails closed because operator-contract provenance is absent");

    var incomplete = NewCompetition("SP4KINCOMPLETE", now.AddMinutes(2), resultsRevision: 1);
    db.Competitions.Add(incomplete);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(
        incomplete.CompetitionKey,
        "{\"stackers\":[{\"id\":\"9.1\",\"name\":\"Incomplete\",\"gender\":\"M\",\"division\":\"\",\"special\":\"No\"}]}",
        1,
        now));
    db.CompetitionResults.Add(NewResult(incomplete.Id, "9.1", "Cycle", "[6.0,6.1,6.2]", 0m, 1, now));
    await db.SaveChangesAsync();
    await governance.SelectRuleVersionAsync(incomplete.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 3020);
    incomplete.Status = "Closed";
    await db.SaveChangesAsync();
    await governance.CertifyGovernedV2SnapshotAsync(incomplete.Id, 3021);
    await AssertThrowsContainsAsync(
        () => projector.ProjectAsync(incomplete.Id, new FinalsHistoricalPlacementScope("Individual", "Open", "Cycle", "normal", "all")),
        FinalsHistoricalPlacementProjectionBlockers.ParticipantMetadataIncomplete,
        "missing authoritative competition-time division blocks historical placement instead of guessing");

    var uncaptured = NewCompetition("SP4KUNCAPTURED", now.AddMinutes(3), resultsRevision: 0);
    db.Competitions.Add(uncaptured);
    await db.SaveChangesAsync();
    await AssertThrowsContainsAsync(
        () => projector.ProjectAsync(uncaptured.Id, new FinalsHistoricalPlacementScope("Individual", "Open", "Cycle", "normal", "all")),
        FinalsHistoricalPlacementProjectionBlockers.SnapshotNotCaptured,
        "uncaptured competitions cannot project historical placement");

    Console.WriteLine("SP-4K immutable historical Finals placement projection tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-4K primary failure: {ex}");
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
        Console.WriteLine($"SP-4K cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-4K cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-4K cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-4K tests passed but cleanup failed.", cleanupFailure);

static FinalsHistoricalPlacementRow Row(FinalsHistoricalPlacementProjection projection, string participantCode) =>
    projection.Rows.Single(item => item.ParticipantCode == participantCode);

static Competition NewCompetition(string key, DateTime now, long resultsRevision) => new()
{
    CompetitionCode = key,
    CompetitionKey = key,
    CompetitionName = $"{key} Test",
    Venue = "LocalDB",
    StartDate = DateOnly.FromDateTime(now),
    EndDate = DateOnly.FromDateTime(now),
    Status = "Active",
    IsPubliclyListed = true,
    ResultsRevision = resultsRevision,
    CreatedAt = now,
    UpdatedAt = now
};

static CompetitionState NewState(string competitionKey, string json, long revision, DateTime now) => new()
{
    CompetitionKey = competitionKey,
    JsonData = json,
    SchemaVersion = "0.9-online",
    StateRevision = revision,
    CreatedAt = now,
    UpdatedAt = now,
    UpdatedBy = "sp4k-test"
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

static async Task AssertThrowsContainsAsync(Func<Task> action, string expectedText, string name)
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

    throw new InvalidOperationException($"Assertion failed: {name} (expected InvalidOperationException containing '{expectedText}').");
}
