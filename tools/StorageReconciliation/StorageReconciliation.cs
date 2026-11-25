using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Ranking;
using StackMeet.Api.Data;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

namespace StackMeet.Tools.StorageReconciliation;

// Development-only candidate: linked into the disposable LocalDB harness, not the deployed API.
public sealed record ReconciliationFinding(string Code, string Severity, int Count);
public sealed record ReconciliationReport(string Outcome, DateTime CapturedAtUtc, long? StateRevision,
    long? ResultsRevision, IReadOnlyList<ReconciliationFinding> Findings);

public static class StorageReconciliation
{
    public static async Task<ReconciliationReport> InspectAsync(string connectionString, int competitionId,
        string? isolatedAssetRoot = null, CancellationToken ct = default)
    {
        // Do not accept production servers or arbitrary databases. A future operational adapter
        // needs separate review, read-only permissions and coordinated SQL/file evidence.
        var connection = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(connection.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase)
            || !connection.IntegratedSecurity || !connection.InitialCatalog.StartsWith("StackMeet_CoreIntegrityTest_", StringComparison.Ordinal)
            || connection.InitialCatalog.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_'))
            || !string.IsNullOrEmpty(connection.AttachDBFilename))
            throw new InvalidOperationException("Reconciliation candidate is restricted to generated CoreIntegrity LocalDB fixtures.");
        if (competitionId <= 0) throw new ArgumentOutOfRangeException(nameof(competitionId));

        await using var db = new StackMeetDbContext(new DbContextOptionsBuilder<StackMeetDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var captured = DateTime.UtcNow;
        var findings = new List<ReconciliationFinding>();
        void Add(string code, string severity, int count = 1)
        {
            if (count > 0) findings.Add(new(code, severity, count));
        }
        var competition = await db.Competitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == competitionId, ct);
        if (competition is null)
        {
            Add("AUTH-09-COMPETITION-MISSING", "Inconclusive");
            await tx.RollbackAsync(ct);
            return Report(null, null);
        }

        var states = await db.CompetitionStates.AsNoTracking().Where(x => x.CompetitionKey == competition.CompetitionKey)
            .Take(2).ToListAsync(ct);
        var state = states.Count == 1 ? states[0] : null;
        var stackers = await db.Stackers.AsNoTracking().Where(x => x.CompetitionId == competitionId).ToListAsync(ct);
        var results = await db.CompetitionResults.AsNoTracking().Where(x => x.CompetitionId == competitionId).ToListAsync(ct);
        var assets = await db.CompetitionAssets.AsNoTracking().Where(x => x.CompetitionId == competitionId).ToListAsync(ct);
        var governance = await new FinalsRankingGovernanceService(db).GetAsync(competitionId, ct);
        var codes = stackers.Select(x => Code(x.StackerCode)).ToHashSet(StringComparer.Ordinal);
        Add("AUTH-02-SQL-DUPLICATE-CODE", "Review", stackers.Count - codes.Count);
        var sqlResults = new Dictionary<(string Stage, string Type, string Participant, string Event), (decimal[] Attempts, decimal Penalty)>();
        foreach (var row in results)
        {
            if (Key(row.Stage, row.ParticipantType, row.ParticipantCode, row.EventCode) is not { } key
                || !TryAttempts(row.AttemptsJson, out var attempts))
            {
                Add("AUTH-03-SQL-RESULT-INVALID", "Review");
                continue;
            }
            if (CompetitionResultRules.ValidateAttempts(attempts, row.Penalty).Count != 0)
                Add("AUTH-03-SQL-RESULT-INVALID", "Review");
            if (!sqlResults.TryAdd(key, (attempts, row.Penalty))) Add("AUTH-03-SQL-DUPLICATE-KEY", "Review");
            if (row.Revision <= 0 || row.Revision > competition.ResultsRevision)
                Add("AUTH-03-SQL-REVISION-INVALID", "Review");
            if (key.Type == "Individual" && !codes.Contains(key.Participant))
                Add("AUTH-04-ORPHAN-INDIVIDUAL-RESULT", "Review");
        }

        if (state is null)
            Add("AUTH-09-STATE-MISSING-OR-AMBIGUOUS", "Inconclusive");
        else if (state.SchemaVersion != "0.9-online")
            Add("AUTH-09-STATE-SCHEMA-UNKNOWN", "Inconclusive");
        else
        {
            try
            {
                using var doc = JsonDocument.Parse(state.JsonData);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) Add("AUTH-09-STATE-NOT-OBJECT", "Inconclusive");
                else AnalyzeState(doc.RootElement, state.JsonData);
            }
            catch (JsonException) { Add("AUTH-09-STATE-MALFORMED", "Inconclusive"); }
        }

        AnalyzeGovernance(governance, state);
        await AnalyzeAssetsAsync(assets);
        await tx.RollbackAsync(ct); // SELECT-only transaction; no SaveChanges or mutation method.
        return Report(state?.StateRevision, competition.ResultsRevision);

        ReconciliationReport Report(long? stateRevision, long? resultsRevision)
        {
            var summary = findings.GroupBy(x => (x.Code, x.Severity))
                .Select(g => new ReconciliationFinding(g.Key.Code, g.Key.Severity, g.Sum(x => x.Count)))
                .OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
            var outcome = summary.Any(x => x.Severity == "Inconclusive") ? "Inconclusive"
                : summary.Length > 0 ? "Review" : "Pass";
            return new(outcome, captured, stateRevision, resultsRevision, summary);
        }

        JsonElement[] Collection(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var value)) return [];
            if (value.ValueKind != JsonValueKind.Array)
            {
                Add("AUTH-09-COLLECTION-SHAPE", "Inconclusive");
                return [];
            }
            var rows = value.EnumerateArray().ToArray();
            Add("AUTH-09-COLLECTION-ENTRY", "Inconclusive", rows.Count(x => x.ValueKind != JsonValueKind.Object));
            return rows.Where(x => x.ValueKind == JsonValueKind.Object).ToArray();
        }

        void AnalyzeState(JsonElement root, string json)
        {
            var legacy = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var row in Collection(root, "stackers"))
            {
                var code = Code(Text(row, "id"));
                if (code.Length == 0) { Add("AUTH-02-LEGACY-CODE-MISSING", "Review"); continue; }
                if (!legacy.TryAdd(code, row)) Add("AUTH-02-LEGACY-DUPLICATE-CODE", "Review");
            }
            if (legacy.Count > 0 && codes.Count == 0) Add("AUTH-01-EMPTY-SQL-LEGACY-ROSTER", "Review");
            if (legacy.Count > 0 && codes.Count > 0)
            {
                Add("AUTH-02-LEGACY-ONLY-CODE", "Review", legacy.Keys.Count(x => !codes.Contains(x)));
                Add("AUTH-02-SQL-ONLY-CODE-WITH-LEGACY-COPY", "Review", codes.Count(x => !legacy.ContainsKey(x)));
                foreach (var stacker in stackers)
                    if (legacy.TryGetValue(Code(stacker.StackerCode), out var row))
                    {
                        // Only directly comparable public fields; derived age/division labels are excluded.
                        var fields = new (string Field, string Value)[]
                        {
                            ("name", (stacker.FirstName + " " + stacker.LastName).Trim()), ("gender", stacker.Gender),
                            ("country", stacker.Country), ("org", stacker.Club ?? "Independent"),
                            ("region", stacker.Region ?? ""), ("special", stacker.IsSpecialStacker ? "Yes" : "No")
                        };
                        if (fields.Any(x => row.TryGetProperty(x.Field, out _) &&
                            !string.Equals(Text(row, x.Field).Trim(), x.Value.Trim(), StringComparison.Ordinal)))
                            Add("AUTH-02-OVERLAP-FIELD-CONFLICT", "Review");
                    }
            }

            var legacyKeys = new HashSet<(string, string, string, string)>();
            foreach (var row in Collection(root, "results"))
            {
                if (Key(Text(row, "stage"), Text(row, "type"), Text(row, "participant"), Text(row, "event")) is not { } key
                    || !row.TryGetProperty("attempts", out var values) || !TryAttempts(values.GetRawText(), out var attempts)
                    || !row.TryGetProperty("penalty", out var p) || p.ValueKind != JsonValueKind.Number || !p.TryGetDecimal(out var penalty))
                { Add("AUTH-03-LEGACY-RESULT-INVALID", "Review"); continue; }
                if (!legacyKeys.Add(key)) Add("AUTH-03-LEGACY-DUPLICATE-KEY", "Review");
                if (!sqlResults.TryGetValue(key, out var sql)) Add("AUTH-03-LEGACY-ONLY-RESULT", "Review");
                else if (sql.Penalty != penalty || !sql.Attempts.SequenceEqual(attempts))
                    Add("AUTH-03-RESULT-VALUE-CONFLICT", "Review");
                else Add("AUTH-03-MATCHING-LEGACY-RESULT-COPY", "Review");
            }

            var doubles = Collection(root, "doubles");
            var relays = Collection(root, "relays");
            Add("AUTH-04-TEAM-ID-MISSING", "Review", doubles.Concat(relays).Count(x => Code(Text(x, "id")).Length == 0));
            var teamService = new CompetitionTeamResultIntegrityService(db);
            var validTeams = teamService.TryReadReadyTeamMemberships(json, out var ready, out _);
            if (!validTeams) Add("AUTH-04-TEAM-GRAPH-INVALID", "Review");
            // Inspect all declared team references, including pending teams; exclude external parent names.
            var referenceService = new CompetitionParticipantReferenceService();
            foreach (var row in doubles.Concat(relays))
            {
                var refs = referenceService.ExtractReferencedCodes(row.GetRawText());
                if (refs.Any(x => !codes.Contains(Code(x)))) Add("AUTH-04-TEAM-MEMBER-NOT-IN-SQL", "Review");
            }
            if (validTeams)
            {
                foreach (var key in sqlResults.Keys.Where(x => x.Type != "Individual"))
                {
                    var teams = key.Type == "Doubles" ? ready.Doubles : ready.TimedRelays;
                    if (!teams.TryGetValue(key.Participant, out var members) || members.Any(x => !codes.Contains(Code(x))))
                        Add("AUTH-04-ORPHAN-TEAM-RESULT", "Review");
                }
            }

            foreach (var snapshot in Collection(root, "finalQualificationSnapshots"))
            {
                var type = CompetitionResultRules.NormalizeParticipantType(Text(snapshot, "participantType"));
                var evt = CompetitionResultRules.NormalizeEvent(Text(snapshot, "event"));
                var status = Text(snapshot, "status");
                if (type is null || evt is null || status is not ("Draft" or "Approved" or "Superseded")
                    || Code(Text(snapshot, "id")).Length == 0)
                    Add("AUTH-05-QUALIFICATION-METADATA", "Review");
                foreach (var qualifier in Collection(snapshot, "selectedQualifiers"))
                {
                    var participant = Code(Text(qualifier, "participantId"));
                    if (participant.Length == 0 || (type == "Individual" && !codes.Contains(participant))
                        || (validTeams && type == "Doubles" && !ready.Doubles.ContainsKey(participant))
                        || (validTeams && type == "Timed Relay" && !ready.TimedRelays.ContainsKey(participant)))
                        Add("AUTH-05-QUALIFICATION-REFERENCE", "Review");
                }
                // Historical qualifier selection is not recalculated or compared to current rankings.
            }
            if (root.TryGetProperty("settings", out var settings) && settings.ValueKind == JsonValueKind.Object)
            {
                if (settings.TryGetProperty("name", out _) && Text(settings, "name") != competition.CompetitionName)
                    Add("AUTH-08-COMPETITION-NAME-DIFFERENCE", "Review");
                foreach (var pair in new[] { ("start", competition.StartDate), ("end", competition.EndDate) })
                    if (settings.TryGetProperty(pair.Item1, out _) &&
                        (!DateOnly.TryParseExact(Text(settings, pair.Item1), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var date) || date != pair.Item2))
                        Add("AUTH-08-COMPETITION-DATE-DIFFERENCE", "Review");
            }
        }

        void AnalyzeGovernance(FinalsRankingGovernanceRecord? record, CompetitionState? current)
        {
            // Not every historical competition was certified. Absence alone is not corruption.
            if (record is null) return;
            if (!record.HasSnapshot)
            {
                if (record.SnapshotJson is not null || record.SnapshotSha256 is not null)
                    Add("AUTH-06-SNAPSHOT-METADATA", "Review");
                return;
            }
            if (record.SnapshotJson is null || record.SnapshotSha256 is null)
            { Add("AUTH-06-SNAPSHOT-MISSING", "Review"); return; }
            if (SnapshotDigestFinding(record) is { } digestFinding)
                Add(digestFinding.Code, digestFinding.Severity, digestFinding.Count);
            if (record.SnapshotSchemaVersion is not (FinalsRankingGovernanceService.LegacySnapshotSchemaVersion
                or FinalsRankingGovernanceService.GovernedV2SnapshotSchemaVersion)
                || FinalsRankingRuleVersions.NormalizeExplicit(record.RuleVersion) is null)
                Add("AUTH-06-SNAPSHOT-VERSION", "Inconclusive");
            if (record.SourceStateRevision is not long sr || sr < 0 || (current is not null && sr > current.StateRevision)
                || record.SourceResultsRevision is not long rr || rr < 0 || rr > competition.ResultsRevision)
                Add("AUTH-06-SNAPSHOT-REVISION", "Review");
            try
            {
                using var doc = JsonDocument.Parse(record.SnapshotJson);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || Text(root, "schemaVersion") != record.SnapshotSchemaVersion
                    || Text(root, "ruleVersion") != record.RuleVersion
                    || !root.TryGetProperty("competition", out var metadata) || metadata.ValueKind != JsonValueKind.Object
                    || !string.Equals(Text(metadata, "competitionKey"), competition.CompetitionKey, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Text(metadata, "competitionCode"), competition.CompetitionCode, StringComparison.OrdinalIgnoreCase)
                    || !metadata.TryGetProperty("stateRevision", out var s) || s.ValueKind != JsonValueKind.Number || !s.TryGetInt64(out var sv) || sv != record.SourceStateRevision
                    || !metadata.TryGetProperty("resultsRevision", out var r) || r.ValueKind != JsonValueKind.Number || !r.TryGetInt64(out var rv) || rv != record.SourceResultsRevision
                    || !root.TryGetProperty("finalsResults", out var finals) || finals.ValueKind != JsonValueKind.Array)
                    Add("AUTH-06-SNAPSHOT-METADATA", "Review");
            }
            catch (JsonException) { Add("AUTH-06-SNAPSHOT-MALFORMED", "Review"); }
        }

        async Task AnalyzeAssetsAsync(List<CompetitionAsset> rows)
        {
            if (isolatedAssetRoot is null)
            {
                Add("AUTH-07-ASSET-BYTES-NOT-CHECKED", "Inconclusive", rows.Count);
                return;
            }
            // Only the supplied disposable fixture root and one competition directory.
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(isolatedAssetRoot));
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            if (!string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(root).StartsWith("StackMeet_ENG04C_", StringComparison.Ordinal)
                || !Directory.Exists(root))
            { Add("AUTH-07-ASSET-ROOT-NOT-FIXTURE", "Inconclusive"); return; }
            var directory = Path.Combine(root, competitionId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            try
            {
                if (IsReparse(root) || IsReparse(directory))
                { Add("AUTH-07-ASSET-PATH-UNSAFE", "Inconclusive"); return; }
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    var name = row.StoredFileName;
                    if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Contains('/') || name.Contains('\\')
                        || Path.IsPathRooted(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    { Add("AUTH-07-ASSET-PATH-UNSAFE", "Inconclusive"); continue; }
                    names.Add(name);
                    var path = Path.Combine(directory, name);
                    if (IsReparse(path)) { Add("AUTH-07-ASSET-PATH-UNSAFE", "Inconclusive"); continue; }
                    if (!File.Exists(path)) { Add("AUTH-07-ASSET-MISSING", "Review"); continue; }
                    await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var length = stream.Length;
                    var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
                    if (length != row.FileSize || !string.Equals(digest, row.Sha256, StringComparison.OrdinalIgnoreCase))
                        Add("AUTH-07-ASSET-MISMATCH", "Review");
                }
                if (Directory.Exists(directory))
                    Add("AUTH-07-ASSET-ORPHAN", "Review", Directory.EnumerateFiles(directory)
                        .Count(path => !names.Contains(Path.GetFileName(path))));
            }
            catch (IOException) { Add("AUTH-07-ASSET-READ-FAILED", "Inconclusive"); }
            catch (UnauthorizedAccessException) { Add("AUTH-07-ASSET-READ-FAILED", "Inconclusive"); }
        }
    }

    // Pure evidence validation shared by SQL inspection and synthetic in-memory corruption tests.
    internal static ReconciliationFinding? SnapshotDigestFinding(FinalsRankingGovernanceRecord record)
    {
        if (record.SnapshotJson is null || record.SnapshotSha256 is null)
            return new("AUTH-06-SNAPSHOT-MISSING", "Review", 1);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(record.SnapshotJson)));
        return string.Equals(hash, record.SnapshotSha256, StringComparison.OrdinalIgnoreCase)
            ? null : new("AUTH-06-SNAPSHOT-HASH-MISMATCH", "Review", 1);
    }

    static bool IsReparse(string path) =>
        (File.Exists(path) || Directory.Exists(path)) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);

    static string Text(JsonElement row, string name) => row.ValueKind == JsonValueKind.Object &&
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    static string Code(string? value) => (value ?? "").Trim().ToUpperInvariant();
    static (string Stage, string Type, string Participant, string Event)? Key(string stage, string type, string participant, string evt)
    {
        if (CompetitionResultRules.ValidateIdentity(stage, type, participant, evt).Count != 0) return null;
        return (CompetitionResultRules.NormalizeStage(stage)!, CompetitionResultRules.NormalizeParticipantType(type)!,
            Code(participant), CompetitionResultRules.NormalizeEvent(evt)!);
    }
    static bool TryAttempts(string json, out decimal[] attempts)
    {
        attempts = [];
        try
        {
            var values = JsonSerializer.Deserialize<decimal[]>(json);
            if (values is null) return false;
            attempts = values;
            return true;
        }
        catch (JsonException) { return false; }
    }
}
