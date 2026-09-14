using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Ranking;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerFinalsRankingSp4jTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
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
    throw new InvalidOperationException("Refusing SP-4J integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerFinalsRankingSp4jTest_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-4J cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-4J LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
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

    var service = new FinalsRankingGovernanceService(db);
    var readiness = new FinalsRankingCertificationReadinessService(db);
    var now = DateTime.UtcNow;

    var readyV2 = NewCompetition("SP4JREADY", "Active", now, resultsRevision: 2);
    db.Competitions.Add(readyV2);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(readyV2.CompetitionKey, "{\"stackers\":[{\"id\":\"1.1\",\"division\":\"Open\"}]}", 3, now));
    db.CompetitionResults.Add(NewResult(readyV2.Id, "[5.1,5.2,5.3]", 2, 0.2m, now));
    await db.SaveChangesAsync();
    await service.SelectRuleVersionAsync(readyV2.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 2001);
    readyV2.Status = "Closed";
    readyV2.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    var before = await readiness.AssessAsync(readyV2.Id);
    Assert(before.IsReady, "finalized explicit v2 evidence is ready before certification");

    await AssertThrowsAsync<InvalidOperationException>(
        () => service.CaptureFinalizedSnapshotAsync(readyV2.Id, 2002),
        "generic snapshot capture remains fail-closed for governed v2");

    var certified = await service.CertifyGovernedV2SnapshotAsync(readyV2.Id, 2003);
    Assert(certified.HasSnapshot, "explicit SP-4J v2 certification captures immutable snapshot");
    Assert(certified.RuleVersion == FinalsRankingRuleVersions.GovernedFinalsV2, "certified snapshot preserves governed v2 rule");
    Assert(certified.SnapshotSchemaVersion == FinalsRankingGovernanceService.GovernedV2SnapshotSchemaVersion,
        "certified v2 snapshot uses versioned source schema");
    Assert(certified.SourceStateRevision == 3, "certified snapshot preserves state revision");
    Assert(certified.SourceResultsRevision == 2, "certified snapshot preserves results revision");
    Assert(certified.SnapshotCapturedByUserId == 2003, "certification actor provenance persisted");
    Assert(certified.SnapshotJson?.Contains($"\"operatorContractVersion\":\"{FinalsRankingCertificationReadinessService.OperatorContractVersion}\"", StringComparison.Ordinal) == true,
        "certified v2 snapshot freezes reviewed operator contract provenance");
    Assert(certified.SnapshotJson?.Contains("\"attemptsJson\":\"[5.1,5.2,5.3]\"", StringComparison.Ordinal) == true, "raw Finals attempts frozen");
    Assert(certified.SnapshotJson?.Contains("\"penalty\":0.2", StringComparison.Ordinal) == true, "result-level penalty frozen");
    var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(certified.SnapshotJson!)));
    Assert(certified.SnapshotSha256 == expectedHash, "certified v2 snapshot hash matches payload");

    var after = await readiness.AssessAsync(readyV2.Id);
    Assert(!after.IsReady && after.BlockingReasons.Contains(FinalsRankingCertificationBlockers.SnapshotAlreadyCaptured),
        "readiness reports immutable snapshot already captured after certification");
    await AssertThrowsAsync<InvalidOperationException>(
        () => service.CertifyGovernedV2SnapshotAsync(readyV2.Id, 2004),
        "v2 certification is one-time");

    var badAttempts = NewCompetition("SP4JBADJSON", "Active", now.AddMinutes(1), resultsRevision: 1);
    db.Competitions.Add(badAttempts);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(badAttempts.CompetitionKey, "{\"stackers\":[]}", 1, now));
    db.CompetitionResults.Add(NewResult(badAttempts.Id, "[5.0,\"oops\",5.2]", 1, 0m, now));
    await db.SaveChangesAsync();
    await service.SelectRuleVersionAsync(badAttempts.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 2010);
    badAttempts.Status = "Closed";
    await db.SaveChangesAsync();
    await AssertThrowsContainsAsync(
        () => service.CertifyGovernedV2SnapshotAsync(badAttempts.Id, 2011),
        FinalsRankingCertificationBlockers.ResultAttemptsMalformed,
        "malformed v2 attempts fail inside certification transaction");
    Assert((await service.GetAsync(badAttempts.Id))?.HasSnapshot == false, "failed malformed-attempt certification writes no snapshot");

    var badRevision = NewCompetition("SP4JBADREV", "Active", now.AddMinutes(2), resultsRevision: 1);
    db.Competitions.Add(badRevision);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(badRevision.CompetitionKey, "{\"stackers\":[]}", 1, now));
    db.CompetitionResults.Add(NewResult(badRevision.Id, "[5.0,5.1,5.2]", 2, 0m, now));
    await db.SaveChangesAsync();
    await service.SelectRuleVersionAsync(badRevision.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 2020);
    badRevision.Status = "Closed";
    await db.SaveChangesAsync();
    await AssertThrowsContainsAsync(
        () => service.CertifyGovernedV2SnapshotAsync(badRevision.Id, 2021),
        FinalsRankingCertificationBlockers.ResultRevisionInconsistent,
        "result revision inconsistency fails inside certification transaction");
    Assert((await service.GetAsync(badRevision.Id))?.HasSnapshot == false, "failed revision certification writes no snapshot");

    var missingState = NewCompetition("SP4JNOSTATE", "Active", now.AddMinutes(3), resultsRevision: 0);
    db.Competitions.Add(missingState);
    await db.SaveChangesAsync();
    await service.SelectRuleVersionAsync(missingState.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 2030);
    missingState.Status = "Closed";
    await db.SaveChangesAsync();
    await AssertThrowsContainsAsync(
        () => service.CertifyGovernedV2SnapshotAsync(missingState.Id, 2031),
        FinalsRankingCertificationBlockers.CompetitionStateMissing,
        "missing state fails v2 certification with stable blocker code");

    var empty = NewCompetition("SP4JEMPTY", "Active", now.AddMinutes(4), resultsRevision: 0);
    db.Competitions.Add(empty);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(empty.CompetitionKey, "{\"stackers\":[]}", 1, now));
    await db.SaveChangesAsync();
    await service.SelectRuleVersionAsync(empty.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 2040);
    empty.Status = "Closed";
    await db.SaveChangesAsync();
    var emptyCertified = await service.CertifyGovernedV2SnapshotAsync(empty.Id, 2041);
    Assert(emptyCertified.HasSnapshot && emptyCertified.SnapshotJson?.Contains("\"finalsResults\":[]", StringComparison.Ordinal) == true,
        "empty Finals dataset can certify an empty immutable v2 source snapshot");

    var legacy = NewCompetition("SP4JLEGACY", "Active", now.AddMinutes(5), resultsRevision: 0);
    db.Competitions.Add(legacy);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(NewState(legacy.CompetitionKey, "{\"stackers\":[]}", 1, now));
    await db.SaveChangesAsync();
    await service.SelectRuleVersionAsync(legacy.Id, FinalsRankingRuleVersions.LegacyFinalsV1, 2050);
    legacy.Status = "Closed";
    await db.SaveChangesAsync();
    await AssertThrowsAsync<InvalidOperationException>(
        () => service.CertifyGovernedV2SnapshotAsync(legacy.Id, 2051),
        "explicit v2 certification method rejects legacy competition");
    var legacyCaptured = await service.CaptureFinalizedSnapshotAsync(legacy.Id, 2052);
    Assert(legacyCaptured.HasSnapshot && legacyCaptured.RuleVersion == FinalsRankingRuleVersions.LegacyFinalsV1,
        "legacy snapshot capture remains on original generic path");
    Assert(legacyCaptured.SnapshotSchemaVersion == FinalsRankingGovernanceService.LegacySnapshotSchemaVersion,
        "legacy snapshot preserves v1 source schema");
    Assert(legacyCaptured.SnapshotJson?.Contains("operatorContractVersion", StringComparison.Ordinal) == false,
        "legacy snapshot payload remains byte-contract compatible without v2 operator provenance field");

    Console.WriteLine("SP-4J governed Finals v2 snapshot certification activation tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-4J primary failure: {ex}");
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
        Console.WriteLine($"SP-4J cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-4J cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-4J cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-4J tests passed but cleanup failed.", cleanupFailure);

static Competition NewCompetition(string key, string status, DateTime now, long resultsRevision) => new()
{
    CompetitionCode = key,
    CompetitionKey = key,
    CompetitionName = $"{key} Test",
    Venue = "LocalDB",
    StartDate = DateOnly.FromDateTime(now),
    EndDate = DateOnly.FromDateTime(now),
    Status = status,
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
    UpdatedBy = "sp4j-test"
};

static CompetitionResult NewResult(int competitionId, string attemptsJson, long revision, decimal penalty, DateTime now) => new()
{
    PublicId = Guid.NewGuid(),
    CompetitionId = competitionId,
    Stage = "Finals",
    ParticipantType = "Individual",
    ParticipantCode = "1.1",
    EventCode = "Cycle",
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
