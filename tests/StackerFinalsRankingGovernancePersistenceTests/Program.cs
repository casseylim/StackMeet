using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Ranking;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerFinalsRankingSp4gTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
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
    throw new InvalidOperationException("Refusing SP-4G integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerFinalsRankingSp4gTest_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-4G cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-4G LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
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

    Assert(await TableExistsAsync(db, "dbo", "FinalsRankingGovernance"), "SP-4G governance table migrated");
    Assert(await TriggerExistsAsync(db, "dbo", "TR_FinalsRankingGovernance_ImmutableSnapshot"), "SP-4G immutable snapshot trigger migrated");

    var now = DateTime.UtcNow;
    var legacyCompetition = NewCompetition("SP4GLEGACY", "Active", null, now);
    legacyCompetition.ResultsRevision = 7;
    db.Competitions.Add(legacyCompetition);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(new CompetitionState
    {
        CompetitionKey = legacyCompetition.CompetitionKey,
        JsonData = "{\"settings\":{\"name\":\"SP-4G Legacy\"},\"stackers\":[{\"id\":\"1.1\",\"name\":\"Final Athlete\",\"gender\":\"M\",\"special\":false,\"division\":\"Male 18-19\"}]}",
        SchemaVersion = "0.9-online",
        StateRevision = 4,
        CreatedAt = now,
        UpdatedAt = now,
        UpdatedBy = "sp4g-test"
    });
    db.CompetitionResults.Add(new CompetitionResult
    {
        PublicId = Guid.NewGuid(),
        CompetitionId = legacyCompetition.Id,
        Stage = "Finals",
        ParticipantType = "Individual",
        ParticipantCode = "1.1",
        EventCode = "3-3-3",
        AttemptsJson = "[5.123,5.456,999]",
        Penalty = 0.5m,
        Revision = 7,
        CreatedAt = now,
        UpdatedAt = now
    });
    await db.SaveChangesAsync();

    var service = new FinalsRankingGovernanceService(db);
    var selected = await service.SelectRuleVersionAsync(legacyCompetition.Id, FinalsRankingRuleVersions.LegacyFinalsV1, 901);
    Assert(selected.RuleVersion == FinalsRankingRuleVersions.LegacyFinalsV1, "legacy rule selection persisted");
    Assert(selected.RuleSelectedByUserId == 901, "rule selector provenance persisted");
    Assert(!selected.HasSnapshot, "rule selection does not fabricate a snapshot");

    var changedSelection = await service.SelectRuleVersionAsync(legacyCompetition.Id, FinalsRankingRuleVersions.GovernedFinalsV2.ToUpperInvariant(), 902);
    Assert(changedSelection.RuleVersion == FinalsRankingRuleVersions.GovernedFinalsV2, "explicit supported rule version normalizes");
    var restoredLegacy = await service.SelectRuleVersionAsync(legacyCompetition.Id, FinalsRankingRuleVersions.LegacyFinalsV1, 903);
    Assert(restoredLegacy.RuleVersion == FinalsRankingRuleVersions.LegacyFinalsV1, "pre-finalization rule selection remains replaceable");

    await AssertThrowsAsync<ArgumentException>(
        () => service.SelectRuleVersionAsync(legacyCompetition.Id, "future-finals-v99", 904),
        "unknown service rule version fails closed");

    legacyCompetition.Status = "Closed";
    legacyCompetition.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    var captured = await service.CaptureFinalizedSnapshotAsync(legacyCompetition.Id, 905);
    Assert(captured.HasSnapshot, "finalized legacy competition snapshot captured");
    Assert(captured.SnapshotSchemaVersion == FinalsRankingGovernanceService.SnapshotSchemaVersion, "snapshot schema version persisted");
    Assert(captured.SourceStateRevision == 4, "competition-state revision provenance persisted");
    Assert(captured.SourceResultsRevision == 7, "durable results revision provenance persisted");
    Assert(captured.SnapshotCapturedByUserId == 905, "snapshot actor provenance persisted");
    Assert(captured.SnapshotJson is not null && captured.SnapshotJson.Contains("\"division\":\"Male 18-19\"", StringComparison.Ordinal), "competition-time division snapshot frozen in evidence");
    Assert(captured.SnapshotJson is not null && captured.SnapshotJson.Contains("\"participantCode\":\"1.1\"", StringComparison.Ordinal), "durable Finals result frozen in evidence");
    Assert(captured.SnapshotJson is not null && captured.SnapshotJson.Contains("\"attemptsJson\":\"[5.123,5.456,999]\"", StringComparison.Ordinal), "raw attempts JSON preserved in evidence");
    var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(captured.SnapshotJson!)));
    Assert(captured.SnapshotSha256 == expectedHash && captured.SnapshotSha256?.Length == 64, "snapshot SHA-256 matches immutable payload");

    var reread = await service.GetAsync(legacyCompetition.Id);
    Assert(reread?.SnapshotSha256 == captured.SnapshotSha256, "captured governance record can be reread");
    await AssertThrowsAsync<InvalidOperationException>(
        () => service.CaptureFinalizedSnapshotAsync(legacyCompetition.Id, 906),
        "snapshot capture is one-time through service boundary");
    await AssertThrowsAsync<InvalidOperationException>(
        () => service.SelectRuleVersionAsync(legacyCompetition.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 907),
        "rule selection is closed after competition finalization/snapshot");

    await AssertThrowsAsync<SqlException>(
        () => db.Database.ExecuteSqlRawAsync($"UPDATE [dbo].[FinalsRankingGovernance] SET [RuleVersion] = 'governed-finals-v2' WHERE [CompetitionId] = {legacyCompetition.Id}"),
        "database trigger rejects mutation after snapshot capture");
    await AssertThrowsAsync<SqlException>(
        () => db.Database.ExecuteSqlRawAsync($"DELETE FROM [dbo].[FinalsRankingGovernance] WHERE [CompetitionId] = {legacyCompetition.Id}"),
        "database trigger rejects deletion after snapshot capture");

    var historicalCompetition = NewCompetition("SP4GHISTORY", "Closed", null, now.AddMinutes(1));
    historicalCompetition.ResultsRevision = 2;
    db.Competitions.Add(historicalCompetition);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(new CompetitionState
    {
        CompetitionKey = historicalCompetition.CompetitionKey,
        JsonData = "{\"stackers\":[{\"id\":\"1.2\",\"division\":\"Female 18-19\"}]}",
        SchemaVersion = "0.9-online",
        StateRevision = 8,
        CreatedAt = now,
        UpdatedAt = now,
        UpdatedBy = "sp4g-test"
    });
    await db.SaveChangesAsync();
    var historicalSnapshot = await service.CaptureFinalizedSnapshotAsync(historicalCompetition.Id, 908);
    Assert(historicalSnapshot.RuleVersion == FinalsRankingRuleVersions.LegacyFinalsV1, "unversioned historical competition freezes as legacy v1");
    Assert(historicalSnapshot.RuleSelectedAt == historicalSnapshot.SnapshotCapturedAt, "implicit legacy selection is made atomically with historical capture");

    var v2Competition = NewCompetition("SP4GV2", "Active", null, now.AddMinutes(2));
    db.Competitions.Add(v2Competition);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(new CompetitionState
    {
        CompetitionKey = v2Competition.CompetitionKey,
        JsonData = "{\"stackers\":[]}",
        SchemaVersion = "0.9-online",
        StateRevision = 1,
        CreatedAt = now,
        UpdatedAt = now,
        UpdatedBy = "sp4g-test"
    });
    await db.SaveChangesAsync();
    var selectedV2 = await service.SelectRuleVersionAsync(v2Competition.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 909);
    Assert(selectedV2.RuleVersion == FinalsRankingRuleVersions.GovernedFinalsV2, "v2 selection can be persisted before finalization");
    v2Competition.Status = "Closed";
    v2Competition.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    await AssertThrowsAsync<InvalidOperationException>(
        () => service.CaptureFinalizedSnapshotAsync(v2Competition.Id, 910),
        "v2 snapshot certification remains blocked until operator engine is version-aware");
    Assert((await service.GetAsync(v2Competition.Id))?.HasSnapshot == false, "blocked v2 capture leaves no false historical snapshot");

    var chessCompetition = NewCompetition("SP4GCHESS", "Active", "chess", now.AddMinutes(3));
    db.Competitions.Add(chessCompetition);
    await db.SaveChangesAsync();
    await AssertThrowsAsync<InvalidOperationException>(
        () => service.SelectRuleVersionAsync(chessCompetition.Id, FinalsRankingRuleVersions.LegacyFinalsV1, 911),
        "non-Sport-Stacking competition cannot enter Finals ranking governance");

    var missingStateCompetition = NewCompetition("SP4GNOSTATE", "Closed", null, now.AddMinutes(4));
    db.Competitions.Add(missingStateCompetition);
    await db.SaveChangesAsync();
    await AssertThrowsAsync<InvalidOperationException>(
        () => service.CaptureFinalizedSnapshotAsync(missingStateCompetition.Id, 912),
        "missing competition-state division provenance blocks capture");

    var malformedStateCompetition = NewCompetition("SP4GBADSTATE", "Closed", null, now.AddMinutes(5));
    db.Competitions.Add(malformedStateCompetition);
    await db.SaveChangesAsync();
    db.CompetitionStates.Add(new CompetitionState
    {
        CompetitionKey = malformedStateCompetition.CompetitionKey,
        JsonData = "{not-json",
        SchemaVersion = "0.9-online",
        StateRevision = 1,
        CreatedAt = now,
        UpdatedAt = now,
        UpdatedBy = "sp4g-test"
    });
    await db.SaveChangesAsync();
    await AssertThrowsAsync<InvalidOperationException>(
        () => service.CaptureFinalizedSnapshotAsync(malformedStateCompetition.Id, 913),
        "malformed competition-state provenance blocks capture");

    var directConstraintCompetition = NewCompetition("SP4GCONSTRAINT", "Active", null, now.AddMinutes(6));
    db.Competitions.Add(directConstraintCompetition);
    await db.SaveChangesAsync();
    await AssertThrowsAsync<SqlException>(
        () => db.Database.ExecuteSqlRawAsync($@"
INSERT INTO [dbo].[FinalsRankingGovernance] ([CompetitionId], [RuleVersion], [RuleSelectedAt])
VALUES ({directConstraintCompetition.Id}, 'future-finals-v99', SYSUTCDATETIME())"),
        "database constraint rejects unknown rule version");
    await AssertThrowsAsync<SqlException>(
        () => db.Database.ExecuteSqlRawAsync($@"
INSERT INTO [dbo].[FinalsRankingGovernance]
    ([CompetitionId], [RuleVersion], [RuleSelectedAt], [SnapshotCapturedAt])
VALUES ({directConstraintCompetition.Id}, 'legacy-finals-v1', SYSUTCDATETIME(), SYSUTCDATETIME())"),
        "database constraint rejects partial snapshot provenance");

    Console.WriteLine("SP-4G Finals ranking governance persistence integration tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-4G primary failure: {ex}");
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
        Console.WriteLine($"SP-4G cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-4G cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-4G cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-4G tests passed but cleanup failed.", cleanupFailure);

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

static async Task<bool> TableExistsAsync(StackMeetDbContext db, string schema, string table)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT CASE WHEN OBJECT_ID(@qualifiedName, 'U') IS NULL THEN 0 ELSE 1 END";
    var parameter = command.CreateParameter();
    parameter.ParameterName = "@qualifiedName";
    parameter.Value = $"{schema}.{table}";
    command.Parameters.Add(parameter);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
}

static async Task<bool> TriggerExistsAsync(StackMeetDbContext db, string schema, string trigger)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT CASE WHEN OBJECT_ID(@qualifiedName, 'TR') IS NULL THEN 0 ELSE 1 END";
    var parameter = command.CreateParameter();
    parameter.ParameterName = "@qualifiedName";
    parameter.Value = $"{schema}.{trigger}";
    command.Parameters.Add(parameter);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
}

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

    throw new InvalidOperationException($"Assertion failed: {name} did not throw {typeof(TException).Name}.");
}
