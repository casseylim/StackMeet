using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Ranking;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerFinalsRankingSp4iTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
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
    throw new InvalidOperationException("Refusing SP-4I integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerFinalsRankingSp4iTest_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-4I cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-4I LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
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
    var readiness = new FinalsRankingCertificationReadinessService(db);
    var now = DateTime.UtcNow;

    var missing = await readiness.AssessAsync(987654321);
    Assert(!missing.IsReady, "unknown competition is not certification-ready");
    AssertHas(missing, FinalsRankingCertificationBlockers.CompetitionNotFound, "unknown competition blocker is explicit");

    var v2 = NewCompetition("SP4IREADY", "Active", null, now);
    v2.ResultsRevision = 2;
    db.Competitions.Add(v2);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(v2.CompetitionKey,
        "{\"settings\":{\"name\":\"SP-4I Ready\"},\"stackers\":[{\"id\":\"1.1\",\"division\":\"Open\"}]}",
        3,
        now));
    db.CompetitionResults.Add(new CompetitionResult
    {
        PublicId = Guid.NewGuid(),
        CompetitionId = v2.Id,
        Stage = "Finals",
        ParticipantType = "Individual",
        ParticipantCode = "1.1",
        EventCode = "Cycle",
        AttemptsJson = "[5.1,5.2,5.3]",
        Penalty = 0.2m,
        Revision = 2,
        CreatedAt = now,
        UpdatedAt = now
    });
    await db.SaveChangesAsync();
    await governance.SelectRuleVersionAsync(v2.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 1001);

    var activeAssessment = await readiness.AssessAsync(v2.Id);
    Assert(!activeAssessment.IsReady, "active v2 competition is not certification-ready");
    AssertHas(activeAssessment, FinalsRankingCertificationBlockers.CompetitionNotFinalized, "active v2 blocker requires finalization");
    Assert(activeAssessment.RuleVersion == FinalsRankingRuleVersions.GovernedFinalsV2, "readiness reports persisted v2 rule");
    Assert(activeAssessment.ExplicitRuleSelection, "readiness records explicit rule selection");
    Assert(activeAssessment.OperatorContractVersion == FinalsRankingCertificationReadinessService.OperatorContractVersion, "readiness reports reviewed SP-4H operator contract");

    v2.Status = "Closed";
    v2.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    var ready = await readiness.AssessAsync(v2.Id);
    Assert(ready.IsReady, "finalized explicit v2 competition with durable evidence is certification-ready");
    Assert(ready.BlockingReasons.Count == 0, "ready v2 assessment has no blockers");
    Assert(ready.SourceStateRevision == 3, "readiness carries authoritative state revision");
    Assert(ready.SourceResultsRevision == 2, "readiness carries durable results revision");
    Assert(ready.FinalsResultCount == 1, "readiness reports durable Finals result count");
    Assert(!ready.SnapshotAlreadyCaptured, "readiness does not fabricate snapshot state");

    await AssertThrowsAsync<InvalidOperationException>(
        () => governance.CaptureFinalizedSnapshotAsync(v2.Id, 1002),
        "SP-4I does not activate governed-v2 snapshot capture");
    Assert((await governance.GetAsync(v2.Id))?.HasSnapshot == false, "readiness leaves governance immutable snapshot uncaptured");

    var noFinals = NewCompetition("SP4IEMPTY", "Active", null, now.AddMinutes(1));
    db.Competitions.Add(noFinals);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(noFinals.CompetitionKey, "{\"stackers\":[]}", 1, now));
    await db.SaveChangesAsync();
    await governance.SelectRuleVersionAsync(noFinals.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 1003);
    noFinals.Status = "Closed";
    await db.SaveChangesAsync();
    var emptyReady = await readiness.AssessAsync(noFinals.Id);
    Assert(emptyReady.IsReady && emptyReady.FinalsResultCount == 0, "empty finalized Finals dataset can still certify an empty source snapshot");

    var legacy = NewCompetition("SP4ILEGACY", "Closed", null, now.AddMinutes(2));
    db.Competitions.Add(legacy);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(legacy.CompetitionKey, "{\"stackers\":[]}", 1, now));
    await db.SaveChangesAsync();
    var legacyAssessment = await readiness.AssessAsync(legacy.Id);
    Assert(!legacyAssessment.IsReady, "unversioned legacy competition is not governed-v2 certification-ready");
    Assert(legacyAssessment.RuleVersion == FinalsRankingRuleVersions.LegacyFinalsV1, "unversioned competition still resolves to legacy v1");
    AssertHas(legacyAssessment, FinalsRankingCertificationBlockers.GovernedV2NotExplicitlySelected, "v2 certification requires explicit persisted v2 selection");

    var missingState = NewCompetition("SP4INOSTATE", "Active", null, now.AddMinutes(3));
    db.Competitions.Add(missingState);
    await db.SaveChangesAsync();
    await governance.SelectRuleVersionAsync(missingState.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 1004);
    missingState.Status = "Closed";
    await db.SaveChangesAsync();
    var missingStateAssessment = await readiness.AssessAsync(missingState.Id);
    Assert(!missingStateAssessment.IsReady, "missing state blocks v2 certification readiness");
    AssertHas(missingStateAssessment, FinalsRankingCertificationBlockers.CompetitionStateMissing, "missing state blocker is explicit");

    var malformedState = NewCompetition("SP4IBADSTATE", "Active", null, now.AddMinutes(4));
    db.Competitions.Add(malformedState);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(malformedState.CompetitionKey, "{not-json", 1, now));
    await db.SaveChangesAsync();
    await governance.SelectRuleVersionAsync(malformedState.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 1005);
    malformedState.Status = "Closed";
    await db.SaveChangesAsync();
    var malformedStateAssessment = await readiness.AssessAsync(malformedState.Id);
    Assert(!malformedStateAssessment.IsReady, "malformed state blocks v2 certification readiness");
    AssertHas(malformedStateAssessment, FinalsRankingCertificationBlockers.CompetitionStateMalformed, "malformed state blocker is explicit");

    var badRevision = NewCompetition("SP4IBADREV", "Active", null, now.AddMinutes(5));
    badRevision.ResultsRevision = 1;
    db.Competitions.Add(badRevision);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(badRevision.CompetitionKey, "{\"stackers\":[]}", 1, now));
    db.CompetitionResults.Add(NewResult(badRevision.Id, "[5.0,5.1,5.2]", 2, now));
    await db.SaveChangesAsync();
    await governance.SelectRuleVersionAsync(badRevision.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 1006);
    badRevision.Status = "Closed";
    await db.SaveChangesAsync();
    var badRevisionAssessment = await readiness.AssessAsync(badRevision.Id);
    Assert(!badRevisionAssessment.IsReady, "result revision ahead of competition revision blocks readiness");
    AssertHas(badRevisionAssessment, FinalsRankingCertificationBlockers.ResultRevisionInconsistent, "revision inconsistency blocker is explicit");

    var badAttempts = NewCompetition("SP4IBADJSON", "Active", null, now.AddMinutes(6));
    badAttempts.ResultsRevision = 1;
    db.Competitions.Add(badAttempts);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(badAttempts.CompetitionKey, "{\"stackers\":[]}", 1, now));
    db.CompetitionResults.Add(NewResult(badAttempts.Id, "[5.0,\"oops\",5.2]", 1, now));
    await db.SaveChangesAsync();
    await governance.SelectRuleVersionAsync(badAttempts.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 1007);
    badAttempts.Status = "Closed";
    await db.SaveChangesAsync();
    var badAttemptsAssessment = await readiness.AssessAsync(badAttempts.Id);
    Assert(!badAttemptsAssessment.IsReady, "malformed result attempts block readiness");
    AssertHas(badAttemptsAssessment, FinalsRankingCertificationBlockers.ResultAttemptsMalformed, "malformed attempts blocker is explicit");

    var zeroStateRevision = NewCompetition("SP4IZEROSTATE", "Active", null, now.AddMinutes(7));
    db.Competitions.Add(zeroStateRevision);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(zeroStateRevision.CompetitionKey, "{\"stackers\":[]}", 0, now));
    await db.SaveChangesAsync();
    await governance.SelectRuleVersionAsync(zeroStateRevision.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 1008);
    zeroStateRevision.Status = "Closed";
    await db.SaveChangesAsync();
    var zeroStateAssessment = await readiness.AssessAsync(zeroStateRevision.Id);
    Assert(!zeroStateAssessment.IsReady, "non-positive state revision blocks readiness");
    AssertHas(zeroStateAssessment, FinalsRankingCertificationBlockers.StateRevisionInvalid, "state revision blocker is explicit");

    var chess = NewCompetition("SP4ICHESS", "Closed", "chess", now.AddMinutes(8));
    db.Competitions.Add(chess);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(chess.CompetitionKey, "{\"players\":[]}", 1, now));
    await db.SaveChangesAsync();
    var chessAssessment = await readiness.AssessAsync(chess.Id);
    Assert(!chessAssessment.IsReady, "non-Sport-Stacking competition is not Finals v2 certification-ready");
    AssertHas(chessAssessment, FinalsRankingCertificationBlockers.ActivityNotSportStacking, "activity boundary blocker is explicit");

    Console.WriteLine("SP-4I governed Finals v2 certification readiness integration tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-4I primary failure: {ex}");
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
        Console.WriteLine($"SP-4I cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-4I cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-4I cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-4I tests passed but cleanup failed.", cleanupFailure);

static Competition NewCompetition(string key, string status, string? activityModuleCode, DateTime now) => new()
{
    CompetitionCode = key,
    CompetitionKey = key,
    CompetitionName = $"{key} Test",
    Venue = "LocalDB",
    StartDate = DateOnly.FromDateTime(now),
    EndDate = DateOnly.FromDateTime(now),
    Status = status,
    ActivityModuleCode = activityModuleCode,
    IsPubliclyListed = true,
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
    UpdatedBy = "sp4i-test"
};

static CompetitionResult NewResult(int competitionId, string attemptsJson, long revision, DateTime now) => new()
{
    PublicId = Guid.NewGuid(),
    CompetitionId = competitionId,
    Stage = "Finals",
    ParticipantType = "Individual",
    ParticipantCode = "1.1",
    EventCode = "3-3-3",
    AttemptsJson = attemptsJson,
    Penalty = 0,
    Revision = revision,
    CreatedAt = now,
    UpdatedAt = now
};

static void AssertHas(FinalsRankingCertificationReadiness assessment, string blocker, string name) =>
    Assert(assessment.BlockingReasons.Contains(blocker, StringComparer.Ordinal), name);

static void Assert(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"Assertion failed: {name}");
    Console.WriteLine($"PASS: {name}");
}

static async Task AssertThrowsAsync<TException>(Func<Task> action, string name)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        Console.WriteLine($"PASS: {name}");
        return;
    }

    throw new InvalidOperationException($"Assertion failed: {name} (expected {typeof(TException).Name}).");
}
