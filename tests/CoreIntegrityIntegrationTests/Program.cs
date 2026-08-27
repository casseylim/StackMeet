using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Services;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_CoreIntegrityTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
var builder = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = databaseName, IntegratedSecurity = true, TrustServerCertificate = true, MultipleActiveResultSets = true, ConnectTimeout = 5 };
if (!string.Equals(builder.DataSource, server, StringComparison.Ordinal)) throw new InvalidOperationException("Refusing integration test: unauthorized SQL server.");
Console.WriteLine($"CoreIntegrity LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
var options = new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(builder.ConnectionString).Options;
try
{
    var probeBuilder = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };
    await using (var probe = new SqlConnection(probeBuilder.ConnectionString))
    {
        using var probeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try { await probe.OpenAsync(probeTimeout.Token); }
        catch (Exception ex) { throw new InvalidOperationException($"LocalDB preflight failed for {server}. Verify SqlLocalDB instance health before rerunning. {ex.Message}", ex); }
    }
    await using var db = new StackMeetDbContext(options);
    using var migrationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    try { await db.Database.MigrateAsync(migrationTimeout.Token); }
    catch (Exception ex) { throw new InvalidOperationException($"LocalDB migration phase failed for {databaseName}. {ex.Message}", ex); }
    Assert(CompetitionResultRules.MaximumBatchSize == 500, "batch limit");
    Assert(CompetitionResultRules.NormalizeParticipantType(" relay ") == "Timed Relay", "legacy relay normalization");
    Assert(CompetitionResultRules.NormalizeStage(" finals ") == "Finals", "stage normalization");
    Assert(CompetitionResultRules.NormalizeEvent(" cycle ") == "Cycle", "event normalization");
    Assert(CompetitionResultRules.ValidateIdentity("Finals", "Individual", "A1", "3-3-3").Count == 0, "valid identity");
    Assert(CompetitionResultRules.ValidateIdentity("Finals", "Timed Relay", "A1", "Cycle").Count > 0, "relay event rule");
    Assert(CompetitionResultRules.ValidateAttempts([1.234m, 2.345m, 3.456m], 0).Count == 0, "valid attempts");
    Assert(CompetitionResultRules.ValidateAttempts([], 0).Count > 0, "empty attempts rejected");
    Assert(CompetitionResultRules.ValidateAttempts([1.2345m], 0).Count > 0, "precision rejected");
    Assert(CompetitionResultRules.ValidateIdentity("Finals", "Individual", new string('x', 51), "Cycle").Count > 0, "participant length rejected");
    Console.WriteLine("CoreIntegrity LocalDB integration harness passed.");
}
finally
{
    await using var cleanup = new StackMeetDbContext(options);
    try { using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8)); await cleanup.Database.EnsureDeletedAsync(cleanupTimeout.Token); Console.WriteLine($"Cleanup: deleted {databaseName}"); }
    catch (Exception ex) { Console.Error.WriteLine($"Cleanup failed for {databaseName}: {ex.Message}"); throw; }
}
static void Assert(bool condition, string name) { if (!condition) throw new InvalidOperationException($"Failed scenario: {name}"); Console.WriteLine($"PASS {name}"); }
