using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

// Development-only measurements, compiled into the existing disposable CoreIntegrity harness.
internal static class Eng04DPerformanceAssertions
{
    private static readonly Regex LogicalReads = new(@"logical reads\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly XNamespace Showplan = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";

    public static async Task RunAsync(string connectionString, int port, string prefix)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase)
            || !builder.IntegratedSecurity || !builder.InitialCatalog.StartsWith("StackMeet_CoreIntegrityTest_", StringComparison.Ordinal)
            || builder.InitialCatalog.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_'))
            || !string.IsNullOrEmpty(builder.AttachDBFilename))
            throw new InvalidOperationException("ENG04D measurements require disposable CoreIntegrity LocalDB.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(100));
        var ct = timeout.Token;
        await using var db = new StackMeetDbContext(new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(builder.ConnectionString).Options);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Add("X-StackMeet-Api-Key", "phase-e-http-test-key");

        foreach (var size in new[] { 32, 512 })
        {
            var key = prefix + size.ToString(CultureInfo.InvariantCulture);
            var now = DateTime.UtcNow;
            var day = DateOnly.FromDateTime(now);
            var competition = new Competition
            {
                CompetitionCode = key, CompetitionKey = key, CompetitionName = "ENG04D fixture",
                Venue = "LocalDB", StartDate = day, EndDate = day, Status = "Active",
                CreatedAt = now, UpdatedAt = now, ResultsRevision = 1
            };
            db.Competitions.Add(competition);
            await db.SaveChangesAsync(ct);
            var codes = Enumerable.Range(1, size).Select(n => $"P{n:D4}").ToArray();
            var state = new CompetitionState
            {
                CompetitionKey = key, JsonData = """{"stackers":[],"results":[]}""",
                SchemaVersion = "0.9-online", StateRevision = 2, CreatedAt = now, UpdatedAt = now
            };
            db.CompetitionStates.Add(state);
            db.Stackers.AddRange(codes.Select(code => new Stacker
            {
                CompetitionId = competition.Id, StackerCode = code, FirstName = "Synthetic",
                LastName = code, Gender = "F", Country = "MY", Paid = "No", CheckedIn = "No",
                CreatedAt = now, UpdatedAt = now
            }));
            db.CompetitionResults.AddRange(codes.Select(code => new CompetitionResult
            {
                CompetitionId = competition.Id, Stage = "Finals", ParticipantType = "Individual",
                ParticipantCode = code, EventCode = "Cycle", AttemptsJson = "[5.123]",
                Penalty = 0, Revision = 1, CreatedAt = now, UpdatedAt = now
            }));
            await db.SaveChangesAsync(ct);

            // Representative parameterized SQL, not a claim about the exact EF-generated execution plan.
            // Both fixture sizes use the same projected columns and predicates.
            await using var sql = new SqlConnection(builder.ConnectionString);
            await sql.OpenAsync(ct);
            var reads = new List<long>();
            sql.InfoMessage += (_, e) =>
            {
                foreach (Match match in LogicalReads.Matches(e.Message))
                    if (long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                        reads.Add(value);
            };
            await MeasureQuery("roster", "SELECT [StackerCode] FROM [dbo].[Stacker] WHERE [CompetitionId]=@id", size);
            await MeasureQuery("results", "SELECT [ParticipantCode],[Stage],[EventCode] FROM [dbo].[CompetitionResult] WHERE [CompetitionId]=@id", size);
            await MeasureQuery("team-references", "SELECT [ParticipantType],[ParticipantCode] FROM [dbo].[CompetitionResult] WHERE [CompetitionId]=@id AND [ParticipantType] IN ('Doubles','Timed Relay','Relay')", 0);
            await MeasureQuery("member-lookup", "SELECT [StackerCode] FROM [dbo].[Stacker] WHERE [CompetitionId]=@id AND [StackerCode]=@code", 1);

            var proposed = JsonSerializer.Serialize(new { members = codes });
            var invalid = JsonSerializer.Serialize(new { members = codes.Append("NOT-IN-FIXTURE") });
            var staleSamples = new List<double>();
            var publicSamples = new List<double>();
            for (var sample = -1; sample < 5; sample++)
            {
                var watch = Stopwatch.StartNew();
                using var response = await client.GetAsync($"/api/public/competitions/{key}/results", ct);
                using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                watch.Stop();
                Check(response.StatusCode == System.Net.HttpStatusCode.OK
                    && payload.RootElement.GetProperty("stackers").GetArrayLength() == size
                    && payload.RootElement.GetProperty("results").GetArrayLength() == size, "public SQL roster/result count");
                if (sample >= 0) publicSamples.Add(watch.Elapsed.TotalMilliseconds);

                watch.Restart();
                using var stale = await SaveAsync(proposed, 1);
                watch.Stop();
                Check(stale.StatusCode == System.Net.HttpStatusCode.Conflict && stale.Headers.ETag?.Tag == "\"2\"",
                    "valid stale state returns current ETag");
                if (sample >= 0) staleSamples.Add(watch.Elapsed.TotalMilliseconds);
            }
            using (var rejected = await SaveAsync(invalid, 1))
                Check(rejected.StatusCode == System.Net.HttpStatusCode.BadRequest
                    && (await rejected.Content.ReadAsStringAsync(ct)).Contains("participant reference", StringComparison.OrdinalIgnoreCase),
                    "invalid participant takes precedence over stale revision");
            using (var malformed = await SaveAsync("{broken", 1))
                Check(malformed.StatusCode == System.Net.HttpStatusCode.BadRequest, "malformed JSON takes precedence over stale revision");
            using (var response = await client.GetAsync($"/api/state/{key}", ct))
                Check(response.Headers.ETag?.Tag == "\"2\"" && await response.Content.ReadAsStringAsync(ct) == state.JsonData,
                    "rejected state requests preserve original bytes and revision");
            WriteTiming(size, "public-http-read", publicSamples);
            WriteTiming(size, "stale-state-http", staleSamples);

            // Contention probe: hold the same update lock used by the state/result write path,
            // observe a second SQL session waiting, then release the holder. No data mutation.
            await MeasureContention(builder.ConnectionString, competition.Id, size, ct);

            async Task<HttpResponseMessage> SaveAsync(string json, int revision)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/state/{key}")
                { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
                request.Headers.TryAddWithoutValidation("If-Match", $"\"{revision}\"");
                return await client.SendAsync(request, ct);
            }

            async Task MeasureQuery(string name, string query, int expectedRows)
            {
                var elapsed = new List<double>();
                var io = new List<long>();
                var operators = new SortedSet<string>(StringComparer.Ordinal);
                for (var sample = -1; sample < 5; sample++)
                {
                    // Runtime IO and execution plan are requested in the same SQL session.
                    // Plan XML is parsed locally; only physical operator names are logged.
                    reads.Clear();
                    await using var command = sql.CreateCommand();
                    command.CommandTimeout = 10;
                    command.CommandText = "SET STATISTICS IO ON; SET STATISTICS XML ON; " + query + "; SET STATISTICS XML OFF; SET STATISTICS IO OFF;";
                    command.Parameters.Add("@id", SqlDbType.Int).Value = competition.Id;
                    if (name == "member-lookup") command.Parameters.Add("@code", SqlDbType.NVarChar, 50).Value = codes[0];
                    var watch = Stopwatch.StartNew();
                    var count = 0;
                    await using (var reader = await command.ExecuteReaderAsync(ct))
                    {
                        while (await reader.ReadAsync(ct)) count++;
                        while (await reader.NextResultAsync(ct))
                            while (await reader.ReadAsync(ct))
                            {
                                if (reader.FieldCount != 1 || reader.IsDBNull(0)) continue;
                                var value = reader.GetValue(0);
                                var xml = value is System.Data.SqlTypes.SqlXml sx ? sx.Value : Convert.ToString(value, CultureInfo.InvariantCulture);
                                if (string.IsNullOrWhiteSpace(xml) || !xml.Contains("ShowPlanXML", StringComparison.Ordinal)) continue;
                                var plan = XDocument.Parse(xml);
                                foreach (var op in plan.Descendants(Showplan + "RelOp"))
                                    if (op.Attribute("PhysicalOp")?.Value is { Length: > 0 } physical) operators.Add(physical);
                            }
                    }
                    watch.Stop();
                    Check(count == expectedRows, $"{name} representative SQL count");
                    if (sample >= 0)
                    {
                        elapsed.Add(watch.Elapsed.TotalMilliseconds);
                        if (reads.Count > 0) io.Add(reads.Sum());
                    }
                }
                Check(operators.Count > 0 && io.Count == 5, $"{name} plan and logical read evidence captured");
                Console.WriteLine("ENG04D_METRIC " + JsonSerializer.Serialize(new
                {
                    fixtureSize = size, metric = name, samples = elapsed.Count,
                    p50Ms = Percentile(elapsed, .50), p95Ms = Percentile(elapsed, .95),
                    logicalReadsMin = io.Min(), logicalReadsMax = io.Max(), physicalOperators = operators.ToArray()
                }));
            }
        }
    }

    private static async Task MeasureContention(string connectionString, int id, int size, CancellationToken ct)
    {
        await using var holder = new SqlConnection(connectionString);
        await using var waiter = new SqlConnection(connectionString);
        await holder.OpenAsync(ct);
        await waiter.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await holder.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await using var lockCommand = holder.CreateCommand();
        lockCommand.Transaction = transaction;
        lockCommand.CommandText = "SELECT [Id] FROM [dbo].[Competition] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]=@id";
        lockCommand.Parameters.Add("@id", SqlDbType.Int).Value = id;
        Check(Convert.ToInt32(await lockCommand.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) == id, "holder acquired competition lock");
        var holdWatch = Stopwatch.StartNew();
        await using var waitCommand = waiter.CreateCommand();
        waitCommand.CommandText = "SELECT [Id] FROM [dbo].[Competition] WITH (UPDLOCK,ROWLOCK) WHERE [Id]=@id";
        waitCommand.CommandTimeout = 12;
        waitCommand.Parameters.Add("@id", SqlDbType.Int).Value = id;

        // Prove actual engine-level blocking by requiring SQL error 1222 on a bounded
        // lock timeout. Do not rely on DMV session visibility or sampling a transient wait.
        // This is test-only session configuration; it changes no application timeout.
        async Task SetLockTimeout(int milliseconds)
        {
            await using var command = waiter.CreateCommand();
            command.CommandText = "SET LOCK_TIMEOUT " + milliseconds.ToString(CultureInfo.InvariantCulture);
            await command.ExecuteNonQueryAsync(ct);
        }

        bool timedOut = false;
        double blockedMs = 0;
        Task<object?>? pending = null;
        Stopwatch? releaseWatch = null;
        bool pendingBeforeRelease = false;
        try
        {
            await SetLockTimeout(750);
            var blockedWatch = Stopwatch.StartNew();
            try { await waitCommand.ExecuteScalarAsync(ct); }
            catch (SqlException ex) when (ex.Number == 1222) { timedOut = true; }
            finally
            {
                blockedWatch.Stop();
                blockedMs = blockedWatch.Elapsed.TotalMilliseconds;
            }
            Check(timedOut, "SQL lock timeout 1222 proves controlled competition blocking");
            await SetLockTimeout(10000);
            releaseWatch = Stopwatch.StartNew();
            pending = waitCommand.ExecuteScalarAsync(ct);
            await Task.Delay(150, ct);
            pendingBeforeRelease = !pending.IsCompleted;
        }
        finally
        {
            await transaction.RollbackAsync(CancellationToken.None);
            holdWatch.Stop();
        }

        // A second request must wait while the holder owns the lock and succeed
        // once it is released. Neither request modifies persisted competition data.
        if (pending is null || releaseWatch is null)
            throw new InvalidOperationException("ENG04D failed: missing release observation");
        var returned = await pending;
        releaseWatch.Stop();
        Check(pendingBeforeRelease && Convert.ToInt32(returned, CultureInfo.InvariantCulture) == id,
            "blocked SQL waiter completed after holder release");
        Console.WriteLine("ENG04D_METRIC " + JsonSerializer.Serialize(new
        {
            fixtureSize = size, metric = "controlled-competition-lock", samples = 1,
            holderMs = Math.Round(holdWatch.Elapsed.TotalMilliseconds, 3),
            timeoutProbeMs = Math.Round(blockedMs, 3),
            waiterMs = Math.Round(releaseWatch.Elapsed.TotalMilliseconds, 3),
            blockingObserved = timedOut, lockTimeoutError = 1222, pendingBeforeRelease
        }));
    }

    private static void WriteTiming(int size, string name, List<double> values) =>
        Console.WriteLine("ENG04D_METRIC " + JsonSerializer.Serialize(new
        {
            fixtureSize = size, metric = name, samples = values.Count,
            p50Ms = Percentile(values, .50), p95Ms = Percentile(values, .95)
        }));

    private static double Percentile(List<double> values, double p)
    {
        var sorted = values.OrderBy(x => x).ToArray();
        return Math.Round(sorted[Math.Max(0, (int)Math.Ceiling(p * sorted.Length) - 1)], 3);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("ENG04D failed: " + name);
    }
}
