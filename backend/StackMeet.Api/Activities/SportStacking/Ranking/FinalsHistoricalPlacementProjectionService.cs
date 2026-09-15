using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackMeet.Api.Data;

namespace StackMeet.Api.Activities.SportStacking.Ranking;

public static class FinalsHistoricalPlacementProjectionBlockers
{
    public const string SnapshotNotCaptured = "snapshot-not-captured";
    public const string SnapshotSchemaUnsupported = "snapshot-schema-unsupported";
    public const string SnapshotRuleUnsupported = "snapshot-rule-unsupported";
    public const string SnapshotHashMismatch = "snapshot-hash-mismatch";
    public const string SnapshotPayloadMalformed = "snapshot-payload-malformed";
    public const string SnapshotProvenanceMismatch = "snapshot-provenance-mismatch";
    public const string ScopeInvalid = "scope-invalid";
    public const string ParticipantMetadataIncomplete = "participant-metadata-incomplete";
    public const string DuplicateLogicalResult = "duplicate-logical-result";
}

public sealed record FinalsHistoricalPlacementScope(
    string ParticipantType,
    string Division,
    string EventCode,
    string Category,
    string Gender);

public sealed record FinalsHistoricalPlacementRow(
    Guid ResultPublicId,
    string ParticipantCode,
    string ParticipantName,
    string Division,
    string EventCode,
    string Category,
    string Gender,
    string ResultStatus,
    IReadOnlyList<decimal> Attempts,
    decimal AppliedPenalty,
    decimal? OfficialBestTime,
    int? Rank,
    bool SharesRank);

public sealed record FinalsHistoricalPlacementProjection(
    string ProjectionVersion,
    int CompetitionId,
    string CompetitionCode,
    string CompetitionKey,
    string RuleVersion,
    string SnapshotSchemaVersion,
    string OperatorContractVersion,
    string SnapshotSha256,
    long SourceStateRevision,
    long SourceResultsRevision,
    DateTime SnapshotCapturedAt,
    FinalsHistoricalPlacementScope Scope,
    IReadOnlyList<FinalsHistoricalPlacementRow> Rows);

/// <summary>
/// SP-4K read-only historical Finals placement projector.
/// </summary>
/// <remarks>
/// The projector consumes only the immutable SP-4J governed-v2 source snapshot. It deliberately
/// does not read current CompetitionState, CompetitionResult or Stacker rows and exposes no API.
/// Placement is scoped explicitly because category and gender filters are applied before the
/// operator ranks participant type + competition-snapshot division + event cohorts.
/// </remarks>
public sealed class FinalsHistoricalPlacementProjectionService(StackMeetDbContext database)
{
    public const string ProjectionVersion = "sp4k-historical-finals-placement-v1";

    public async Task<FinalsHistoricalPlacementProjection> ProjectAsync(
        int competitionId,
        FinalsHistoricalPlacementScope scope,
        CancellationToken ct = default)
    {
        var normalizedScope = NormalizeScope(scope);
        var governance = await new FinalsRankingGovernanceService(database).GetAsync(competitionId, ct);
        if (governance?.HasSnapshot != true
            || string.IsNullOrWhiteSpace(governance.SnapshotJson)
            || string.IsNullOrWhiteSpace(governance.SnapshotSha256)
            || governance.SnapshotCapturedAt is null)
        {
            throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotNotCaptured,
                "A finalized immutable Finals ranking source snapshot is required.");
        }

        if (!string.Equals(governance.SnapshotSchemaVersion, FinalsRankingGovernanceService.GovernedV2SnapshotSchemaVersion, StringComparison.Ordinal))
        {
            throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotSchemaUnsupported,
                "SP-4K accepts only finals-ranking-source-v2 evidence with operator-contract provenance.");
        }

        if (!string.Equals(governance.RuleVersion, FinalsRankingRuleVersions.GovernedFinalsV2, StringComparison.Ordinal))
        {
            throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotRuleUnsupported,
                "SP-4K accepts only governed-finals-v2 certified evidence.");
        }

        var snapshotJson = governance.SnapshotJson!;
        var computedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson)));
        if (!string.Equals(computedHash, governance.SnapshotSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotHashMismatch,
                "The stored SHA-256 does not match the immutable snapshot payload.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(snapshotJson);
        }
        catch (JsonException ex)
        {
            throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotPayloadMalformed,
                "The immutable snapshot is not valid JSON.", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotPayloadMalformed, "Snapshot root must be a JSON object.");

            var schemaVersion = RequiredString(root, "schemaVersion");
            var ruleVersion = RequiredString(root, "ruleVersion");
            var operatorContractVersion = RequiredString(root, "operatorContractVersion");
            if (!string.Equals(schemaVersion, FinalsRankingGovernanceService.GovernedV2SnapshotSchemaVersion, StringComparison.Ordinal))
                throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotSchemaUnsupported, "Snapshot envelope schema does not match governed v2.");
            if (!string.Equals(ruleVersion, FinalsRankingRuleVersions.GovernedFinalsV2, StringComparison.Ordinal))
                throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotRuleUnsupported, "Snapshot envelope rule does not match governed v2.");
            if (!string.Equals(operatorContractVersion, FinalsRankingCertificationReadinessService.OperatorContractVersion, StringComparison.Ordinal))
                throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotProvenanceMismatch, "Snapshot operator contract is not the reviewed SP-4H event Finals contract.");

            if (!root.TryGetProperty("competition", out var competition) || competition.ValueKind != JsonValueKind.Object)
                throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotPayloadMalformed, "Snapshot competition provenance is missing.");
            var competitionCode = RequiredString(competition, "competitionCode");
            var competitionKey = RequiredString(competition, "competitionKey");
            var stateRevision = RequiredInt64(competition, "stateRevision");
            var resultsRevision = RequiredInt64(competition, "resultsRevision");
            if (governance.SourceStateRevision != stateRevision || governance.SourceResultsRevision != resultsRevision)
                throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotProvenanceMismatch, "Snapshot revisions do not match persisted governance provenance.");

            if (!root.TryGetProperty("competitionState", out var competitionState) || competitionState.ValueKind != JsonValueKind.Object)
                throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotPayloadMalformed, "CompetitionState provenance is missing from the immutable snapshot.");
            if (!root.TryGetProperty("finalsResults", out var finalsResultsElement) || finalsResultsElement.ValueKind != JsonValueKind.Array)
                throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotPayloadMalformed, "Finals result evidence is missing from the immutable snapshot.");

            var participants = ReadIndividualParticipants(competitionState);
            var results = ReadSnapshotResults(finalsResultsElement);
            EnsureLogicalResultUniqueness(results);
            EnsureCompleteIndividualMetadataForEvent(results, participants, normalizedScope.EventCode);

            var projected = BuildScopedRows(results, participants, normalizedScope);
            return new FinalsHistoricalPlacementProjection(
                ProjectionVersion,
                competitionId,
                competitionCode,
                competitionKey,
                ruleVersion,
                schemaVersion,
                operatorContractVersion,
                governance.SnapshotSha256!,
                stateRevision,
                resultsRevision,
                governance.SnapshotCapturedAt.Value,
                normalizedScope,
                projected);
        }
    }

    static FinalsHistoricalPlacementScope NormalizeScope(FinalsHistoricalPlacementScope? input)
    {
        if (input is null)
            throw Blocked(FinalsHistoricalPlacementProjectionBlockers.ScopeInvalid, "Placement scope is required.");

        var participantType = input.ParticipantType?.Trim() ?? string.Empty;
        var division = input.Division?.Trim() ?? string.Empty;
        var eventCode = NormalizeEvent(input.EventCode);
        var category = (input.Category?.Trim() ?? string.Empty).ToLowerInvariant();
        var rawGender = input.Gender?.Trim() ?? string.Empty;
        var gender = rawGender.Equals("all", StringComparison.OrdinalIgnoreCase) ? "all" : rawGender.ToUpperInvariant();

        var errors = new List<string>();
        if (!participantType.Equals("Individual", StringComparison.OrdinalIgnoreCase))
            errors.Add("participantType must be Individual for Stacker Identity v1 historical placement.");
        if (division.Length == 0 || division.Equals("all", StringComparison.OrdinalIgnoreCase))
            errors.Add("division must be one explicit competition-snapshot division.");
        if (eventCode is null)
            errors.Add("event must be 3-3-3, 3-6-3 or Cycle.");
        if (category is not ("normal" or "special" or "mixed"))
            errors.Add("category must be normal, special or mixed.");
        if (gender is not ("all" or "M" or "F"))
            errors.Add("gender must be all, M or F.");
        if (errors.Count != 0)
            throw Blocked(FinalsHistoricalPlacementProjectionBlockers.ScopeInvalid, string.Join(" ", errors));

        return new FinalsHistoricalPlacementScope("Individual", division, eventCode!, category, gender);
    }

    static Dictionary<string, SnapshotParticipant> ReadIndividualParticipants(JsonElement competitionState)
    {
        if (!competitionState.TryGetProperty("stackers", out var stackers) || stackers.ValueKind != JsonValueKind.Array)
            return new Dictionary<string, SnapshotParticipant>(StringComparer.Ordinal);

        var participants = new Dictionary<string, SnapshotParticipant>(StringComparer.Ordinal);
        foreach (var item in stackers.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var id = OptionalString(item, "id");
            if (id.Length == 0) continue;
            if (!participants.TryAdd(id, new SnapshotParticipant(
                id,
                OptionalString(item, "name"),
                OptionalString(item, "gender"),
                OptionalString(item, "division"),
                OptionalString(item, "special"))))
            {
                throw Blocked(FinalsHistoricalPlacementProjectionBlockers.ParticipantMetadataIncomplete,
                    $"CompetitionState contains duplicate stacker id '{id}'.");
            }
        }
        return participants;
    }

    static List<SnapshotResult> ReadSnapshotResults(JsonElement finalsResults)
    {
        var rows = new List<SnapshotResult>();
        foreach (var item in finalsResults.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotPayloadMalformed, "Finals result evidence must contain JSON objects.");

            var publicIdText = RequiredString(item, "publicId");
            if (!Guid.TryParse(publicIdText, out var publicId))
                throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotPayloadMalformed, "Finals result publicId is malformed.");

            var attemptsJson = RequiredString(item, "attemptsJson");
            rows.Add(new SnapshotResult(
                publicId,
                RequiredString(item, "participantType"),
                RequiredString(item, "participantCode"),
                RequiredString(item, "eventCode"),
                ParseAttempts(attemptsJson),
                RequiredDecimal(item, "penalty"),
                RequiredInt64(item, "revision")));
        }
        return rows;
    }

    static void EnsureLogicalResultUniqueness(IReadOnlyCollection<SnapshotResult> results)
    {
        var duplicate = results
            .GroupBy(item => $"{item.ParticipantType}\u001f{item.ParticipantCode}\u001f{item.EventCode}", StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw Blocked(FinalsHistoricalPlacementProjectionBlockers.DuplicateLogicalResult, "Snapshot contains duplicate Finals logical-result evidence.");
    }

    static void EnsureCompleteIndividualMetadataForEvent(
        IReadOnlyCollection<SnapshotResult> results,
        IReadOnlyDictionary<string, SnapshotParticipant> participants,
        string eventCode)
    {
        foreach (var result in results.Where(item => item.ParticipantType == "Individual" && NormalizeEvent(item.EventCode) == eventCode))
        {
            if (!participants.TryGetValue(result.ParticipantCode, out var participant)
                || string.IsNullOrWhiteSpace(participant.Division)
                || participant.Gender is not ("M" or "F")
                || participant.Special is not ("Yes" or "No"))
            {
                throw Blocked(FinalsHistoricalPlacementProjectionBlockers.ParticipantMetadataIncomplete,
                    $"Immutable competition-time participant metadata is incomplete for '{result.ParticipantCode}'.");
            }
        }
    }

    static IReadOnlyList<FinalsHistoricalPlacementRow> BuildScopedRows(
        IReadOnlyCollection<SnapshotResult> results,
        IReadOnlyDictionary<string, SnapshotParticipant> participants,
        FinalsHistoricalPlacementScope scope)
    {
        var scoped = new List<ComputedRow>();
        foreach (var result in results)
        {
            if (result.ParticipantType != "Individual" || NormalizeEvent(result.EventCode) != scope.EventCode) continue;
            var participant = participants[result.ParticipantCode];
            if (!string.Equals(participant.Division, scope.Division, StringComparison.Ordinal)) continue;
            if (!MatchesCategory(participant.Special, scope.Category)) continue;
            if (scope.Gender != "all" && participant.Gender != scope.Gender) continue;

            var computation = Compute(result);
            scoped.Add(new ComputedRow(result, participant, computation));
        }

        var eligible = scoped.Where(item => item.Computation.Status == "valid")
            .OrderBy(item => item.Computation.TieKey, TieKeyComparer.Instance)
            .ThenBy(item => DisplayName(item.Participant), StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Result.ParticipantCode, StringComparer.Ordinal)
            .ToArray();

        var ranks = new Dictionary<Guid, int>();
        var tieCounts = eligible.GroupBy(item => item.Computation.TieKey).ToDictionary(group => group.Key, group => group.Count());
        TieKey? previous = null;
        var rank = 0;
        for (var index = 0; index < eligible.Length; index++)
        {
            var key = eligible[index].Computation.TieKey!;
            if (previous is null || TieKeyComparer.Instance.Compare(key, previous) != 0) rank = index + 1;
            ranks[eligible[index].Result.PublicId] = rank;
            previous = key;
        }

        return scoped
            .Select(item => new FinalsHistoricalPlacementRow(
                item.Result.PublicId,
                item.Result.ParticipantCode,
                DisplayName(item.Participant),
                item.Participant.Division,
                scope.EventCode,
                scope.Category,
                scope.Gender,
                item.Computation.Status,
                item.Result.Attempts,
                item.Computation.AppliedPenalty,
                item.Computation.OfficialBestTime,
                ranks.TryGetValue(item.Result.PublicId, out var projectedRank) ? projectedRank : null,
                item.Computation.TieKey is not null && tieCounts.TryGetValue(item.Computation.TieKey, out var tieCount) && tieCount > 1))
            .OrderBy(item => item.Rank ?? int.MaxValue)
            .ThenBy(item => item.ParticipantName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ParticipantCode, StringComparer.Ordinal)
            .ToArray();
    }

    static Computation Compute(SnapshotResult result)
    {
        if (result.Penalty >= 999m)
            return new Computation("scratch", 0m, null, null);

        var valid = result.Attempts.Where(value => value > 0m && value < 999m).OrderBy(value => value).ToArray();
        if (valid.Length > 0)
        {
            var appliedPenalty = result.Penalty > 0m && result.Penalty < 999m ? result.Penalty : 0m;
            var key = new TieKey(valid[0] + appliedPenalty, valid.Length > 1 ? valid[1] : null, valid.Length > 2 ? valid[2] : null);
            return new Computation("valid", appliedPenalty, key.First, key);
        }

        if (result.Attempts.Count == 0)
            return new Computation("missing", 0m, null, null);
        if (result.Attempts.All(value => value == 999m))
            return new Computation("scratch", 0m, null, null);
        return new Computation("invalid", 0m, null, null);
    }

    static IReadOnlyList<decimal> ParseAttempts(string attemptsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(attemptsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotPayloadMalformed, "Finals attempts evidence must be a JSON array.");
            var attempts = new List<decimal>();
            foreach (var value in document.RootElement.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var numeric))
                    throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotPayloadMalformed, "Finals attempts evidence must contain only numeric values.");
                attempts.Add(numeric);
            }
            return attempts;
        }
        catch (JsonException ex)
        {
            throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotPayloadMalformed, "Finals attempts evidence is malformed.", ex);
        }
    }

    static bool MatchesCategory(string special, string category) => category switch
    {
        "normal" => special != "Yes",
        "special" => special == "Yes",
        "mixed" => true,
        _ => false
    };

    static string? NormalizeEvent(string? value) => (value?.Trim() ?? string.Empty).ToLowerInvariant() switch
    {
        "3-3-3" => "3-3-3",
        "3-6-3" => "3-6-3",
        "cycle" => "Cycle",
        _ => null
    };

    static string DisplayName(SnapshotParticipant participant) =>
        string.IsNullOrWhiteSpace(participant.Name) ? participant.Id : participant.Name;

    static string RequiredString(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotPayloadMalformed, $"Required snapshot property '{property}' is missing.");
        return value.GetString()!;
    }

    static string OptionalString(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    static long RequiredInt64(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result))
            throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotPayloadMalformed, $"Required snapshot property '{property}' is missing or invalid.");
        return result;
    }

    static decimal RequiredDecimal(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var result))
            throw Blocked(FinalsHistoricalPlacementProjectionBlockers.SnapshotPayloadMalformed, $"Required snapshot property '{property}' is missing or invalid.");
        return result;
    }

    static InvalidOperationException Blocked(string code, string detail, Exception? inner = null) =>
        new($"{code}: {detail}", inner);

    sealed record SnapshotParticipant(string Id, string Name, string Gender, string Division, string Special);
    sealed record SnapshotResult(Guid PublicId, string ParticipantType, string ParticipantCode, string EventCode, IReadOnlyList<decimal> Attempts, decimal Penalty, long Revision);
    sealed record Computation(string Status, decimal AppliedPenalty, decimal? OfficialBestTime, TieKey? TieKey);
    sealed record ComputedRow(SnapshotResult Result, SnapshotParticipant Participant, Computation Computation);
    sealed record TieKey(decimal First, decimal? Second, decimal? Third);

    sealed class TieKeyComparer : IComparer<TieKey>
    {
        public static TieKeyComparer Instance { get; } = new();
        public int Compare(TieKey? left, TieKey? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return 1;
            if (right is null) return -1;
            var first = left.First.CompareTo(right.First);
            if (first != 0) return first;
            var second = CompareNullable(left.Second, right.Second);
            if (second != 0) return second;
            return CompareNullable(left.Third, right.Third);
        }

        static int CompareNullable(decimal? left, decimal? right)
        {
            if (left == right) return 0;
            if (left is null) return 1;
            if (right is null) return -1;
            return left.Value.CompareTo(right.Value);
        }
    }
}
