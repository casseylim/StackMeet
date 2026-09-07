using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackMeet.Api.Activities;
using StackMeet.Api.Data;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

await Phase4CActivityAssignmentRuntimeAssertions.RunAsync();

internal static class Phase4CActivityAssignmentRuntimeAssertions
{
    const string Server = @"(localdb)\MSSQLLocalDB";
    const string ApiKey = "phase-4c-maintenance-key";
    const string SessionSigningKey = "phase-4c-session-signing-key-32-bytes-minimum";

    internal static async Task RunAsync()
    {
        var databaseName = $"StackMeet_Phase4C_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = databaseName,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            MultipleActiveResultSets = true,
            ConnectTimeout = 5
        };
        if (!string.Equals(builder.DataSource, Server, StringComparison.Ordinal)) throw new InvalidOperationException("Phase 4C refused unauthorized SQL server.");
        if (!databaseName.StartsWith("StackMeet_Phase4C_", StringComparison.Ordinal) || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_'))) throw new InvalidOperationException("Phase 4C refused unsafe generated database name.");

        Exception? failure = null;
        try
        {
            Console.WriteLine($"Phase 4C activity assignment runtime: server={builder.DataSource}; database={builder.InitialCatalog}");
            var options = new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(builder.ConnectionString).Options;
            await using (var db = new StackMeetDbContext(options))
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                await db.Database.MigrateAsync(timeout.Token);
                await SeedAsync(db);
            }

            using var factory = new Phase4CTestApiFactory(builder.ConnectionString);
            await RunHttpMatrixAsync(factory, builder.ConnectionString);
            Console.WriteLine("Phase 4C activity assignment runtime assertions passed.");
        }
        catch (Exception ex)
        {
            failure = ex;
            Console.Error.WriteLine($"Phase 4C failure: {ex.Message}");
        }
        finally
        {
            try { await DropDatabaseAsync(builder.ConnectionString, databaseName); }
            catch (Exception cleanupEx)
            {
                if (failure is null) throw;
                Console.Error.WriteLine($"Phase 4C cleanup secondary failure: {cleanupEx.Message}");
            }
        }

        if (failure is not null) throw failure;
    }

    static async Task SeedAsync(StackMeetDbContext db)
    {
        var now = DateTime.UtcNow;
        var empty = Competition("P4C_EMPTY", "Phase 4C Empty", now);
        var participant = Competition("P4C_PART", "Phase 4C Participant", now);
        var result = Competition("P4C_RESULT", "Phase 4C Result", now);
        db.Competitions.AddRange(empty, participant, result);
        await db.SaveChangesAsync();

        db.Stackers.Add(new Stacker
        {
            CompetitionId = participant.Id,
            StackerCode = "P1",
            FirstName = "Phase",
            LastName = "FourC",
            Gender = "M",
            Country = "MY",
            Paid = "No",
            CheckedIn = "No",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.CompetitionResults.Add(new CompetitionResult
        {
            CompetitionId = result.Id,
            Stage = "Finals",
            ParticipantType = "Individual",
            ParticipantCode = "R1",
            EventCode = "Cycle",
            AttemptsJson = "[1.234,2.345,3.456]",
            Penalty = 0,
            Revision = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    static Competition Competition(string key, string name, DateTime now) => new()
    {
        CompetitionCode = key,
        CompetitionKey = key,
        CompetitionName = name,
        Venue = "LocalDB",
        StartDate = DateOnly.FromDateTime(now),
        EndDate = DateOnly.FromDateTime(now),
        Status = "Active",
        CreatedAt = now,
        UpdatedAt = now
    };

    static async Task RunHttpMatrixAsync(Phase4CTestApiFactory factory, string connectionString)
    {
        var ids = await CompetitionIdsAsync(connectionString);

        using (var anonymous = Client(factory))
        {
            var response = await PutActivity(anonymous, ids.Empty, "test-activity");
            Expect(response.StatusCode == HttpStatusCode.Unauthorized, "Phase 4C HTTP no credential returns 401");
        }

        using (var sessionClient = Client(factory))
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:SessionSigningKey"] = SessionSigningKey
            }).Build();
            var token = new SessionTokenService(configuration).Create("P4C_EMPTY", "Phase 4C session").ToString();
            sessionClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await PutActivity(sessionClient, ids.Empty, "test-activity");
            Expect(response.StatusCode == HttpStatusCode.Forbidden, "Phase 4C authenticated non-maintenance request returns 403");
        }

        using var client = Client(factory);
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Add("X-StackMeet-Api-Key", ApiKey);

        var blank = await PutActivity(client, ids.Empty, " ");
        Expect(blank.StatusCode == HttpStatusCode.BadRequest, "Phase 4C blank module code rejected");
        var unknown = await PutActivity(client, ids.Empty, "unknown-test-module");
        Expect(unknown.StatusCode == HttpStatusCode.BadRequest, "Phase 4C unknown module code rejected");
        Expect(await SelectorAsync(connectionString, ids.Empty) is null, "Phase 4C invalid requests do not persist selector");

        var assigned = await PutActivity(client, ids.Empty, "test-activity");
        Expect(assigned.StatusCode == HttpStatusCode.OK, "Phase 4C empty competition accepts registered module");
        Expect(await ResponseCodeAsync(assigned) == "test-activity", "Phase 4C assignment returns canonical descriptor");
        Expect(await SelectorAsync(connectionString, ids.Empty) == "test-activity", "Phase 4C successful assignment persists selector");

        var descriptor = await client.GetAsync($"/api/competitions/{ids.Empty}/activity");
        Expect(descriptor.StatusCode == HttpStatusCode.OK && await ResponseCodeAsync(descriptor) == "test-activity", "Phase 4C resolver observes persisted assignment");

        await AddStateAsync(connectionString, ids.Empty, "P4C_EMPTY");
        var sameAfterState = await PutActivity(client, ids.Empty, "test-activity");
        Expect(sameAfterState.StatusCode == HttpStatusCode.OK, "Phase 4C same module remains idempotent after saved state");
        var stateSwitch = await PutActivity(client, ids.Empty, SportStackingActivityModule.ModuleCode);
        Expect(stateSwitch.StatusCode == HttpStatusCode.Conflict, "Phase 4C saved state blocks valid module switch");
        Expect(await SelectorAsync(connectionString, ids.Empty) == "test-activity", "Phase 4C state-blocked switch preserves selector");

        var participantSame = await PutActivity(client, ids.Participant, SportStackingActivityModule.ModuleCode);
        Expect(participantSame.StatusCode == HttpStatusCode.OK, "Phase 4C participant competition allows same effective module");
        var participantSwitch = await PutActivity(client, ids.Participant, "test-activity");
        Expect(participantSwitch.StatusCode == HttpStatusCode.Conflict, "Phase 4C participant data blocks valid module switch");
        Expect(await SelectorAsync(connectionString, ids.Participant) == SportStackingActivityModule.ModuleCode, "Phase 4C participant-blocked switch preserves selector");

        var resultSame = await PutActivity(client, ids.Result, SportStackingActivityModule.ModuleCode);
        Expect(resultSame.StatusCode == HttpStatusCode.OK, "Phase 4C result competition allows same effective module");
        var resultSwitch = await PutActivity(client, ids.Result, "test-activity");
        Expect(resultSwitch.StatusCode == HttpStatusCode.Conflict, "Phase 4C result data blocks valid module switch");
        Expect(await SelectorAsync(connectionString, ids.Result) == SportStackingActivityModule.ModuleCode, "Phase 4C result-blocked switch preserves selector");
    }

    static HttpClient Client(Phase4CTestApiFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        client.Timeout = TimeSpan.FromSeconds(15);
        return client;
    }

    static Task<HttpResponseMessage> PutActivity(HttpClient client, int id, string? moduleCode) =>
        client.PutAsJsonAsync($"/api/competitions/{id}/activity", new { activityModuleCode = moduleCode });

    static async Task<string?> ResponseCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    static async Task<(int Empty, int Participant, int Result)> CompetitionIdsAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(connectionString).Options;
        await using var db = new StackMeetDbContext(options);
        var empty = await db.Competitions.Where(x => x.CompetitionKey == "P4C_EMPTY").Select(x => x.Id).SingleAsync();
        var participant = await db.Competitions.Where(x => x.CompetitionKey == "P4C_PART").Select(x => x.Id).SingleAsync();
        var result = await db.Competitions.Where(x => x.CompetitionKey == "P4C_RESULT").Select(x => x.Id).SingleAsync();
        return (empty, participant, result);
    }

    static async Task<string?> SelectorAsync(string connectionString, int id)
    {
        var options = new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(connectionString).Options;
        await using var db = new StackMeetDbContext(options);
        return await db.Competitions.AsNoTracking().Where(x => x.Id == id).Select(x => x.ActivityModuleCode).SingleAsync();
    }

    static async Task AddStateAsync(string connectionString, int competitionId, string competitionKey)
    {
        var options = new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(connectionString).Options;
        await using var db = new StackMeetDbContext(options);
        var now = DateTime.UtcNow;
        db.CompetitionStates.Add(new CompetitionState
        {
            CompetitionKey = competitionKey,
            JsonData = "{\"phase4c\":true}",
            SchemaVersion = "0.9-online",
            StateRevision = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
        Expect(await db.Competitions.AnyAsync(x => x.Id == competitionId), "Phase 4C state seed competition remains available");
    }

    static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        var masterBuilder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        await using var connection = new SqlConnection(masterBuilder.ConnectionString);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await connection.OpenAsync(timeout.Token);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 8;
        var escaped = databaseName.Replace("]", "]]");
        command.CommandText = $"IF DB_ID(@databaseName) IS NOT NULL BEGIN ALTER DATABASE [{escaped}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{escaped}]; END";
        command.Parameters.AddWithValue("@databaseName", databaseName);
        await command.ExecuteNonQueryAsync(timeout.Token);
        Console.WriteLine($"Phase 4C cleanup: deleted {databaseName}");
    }

    static void Expect(bool condition, string scenario)
    {
        if (!condition) throw new InvalidOperationException($"Failed scenario: {scenario}");
        Console.WriteLine($"PASS {scenario}");
    }

    sealed class Phase4CTestApiFactory(string connectionString) : WebApplicationFactory<CompetitionActivityResolver>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "backend", "StackMeet.Api")));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:StackMeet"] = connectionString,
                ["Security:ApiKey"] = ApiKey,
                ["Security:SessionSigningKey"] = SessionSigningKey
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IActivityModule>();
                services.AddSingleton<IActivityModule, SportStackingActivityModule>();
                services.AddSingleton<IActivityModule, Phase4CTestActivityModule>();
            });
        }
    }

    sealed class Phase4CTestActivityModule : IActivityModule
    {
        public string Code => "test-activity";
        public string DisplayName => "Phase 4C Test Activity";
        public string Version => "test-only";
        public ActivityModuleCapabilities Capabilities { get; } = new(false, false, false, false, false, false);
    }
}
