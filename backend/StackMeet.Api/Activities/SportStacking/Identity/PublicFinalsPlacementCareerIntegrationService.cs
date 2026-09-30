using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Ranking;
using StackMeet.Api.Data;

namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// SP-4N read-only integration boundary that discovers the only publication-safe SP-4L selections
/// from certified immutable Finals evidence, then applies the SP-4M public publication contract.
/// </summary>
/// <remarks>
/// Current Stacker demographic fields and current CompetitionResult rows are never used to discover
/// a ranking cohort. The reviewed identity link supplies only the historical participant code. The
/// participant's competition-time division and eligible event list are read from the immutable
/// governed-v2 snapshot. Legacy, missing, ambiguous or malformed evidence contributes no placement.
/// </remarks>
public sealed class PublicFinalsPlacementCareerIntegrationService(StackMeetDbContext database)
{
    private static readonly string[] EventOrder = ["3-3-3", "3-6-3", "Cycle"];

    public async Task<PublicFinalsPlacementCareerPublication?> GetPublicEligibleAsync(
        string? nadiTrackId,
        CancellationToken cancellationToken = default)
    {
        if (!NadiTrackIdRules.IsValid(nadiTrackId)) return null;
        var normalizedId = NadiTrackIdRules.Normalize(nadiTrackId!);

        var identity = await database.SportStackerIdentities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.NadiTrackId == normalizedId && item.IsPublicProfile,
                cancellationToken);

        if (identity is null) return null;

        var linkedRows = await (
            from link in database.StackerIdentityLinks.AsNoTracking()
            join stacker in database.Stackers.AsNoTracking() on link.StackerId equals stacker.Id
            join competition in database.Competitions.AsNoTracking() on stacker.CompetitionId equals competition.Id
            where link.SportStackerIdentityId == identity.Id
                && competition.IsPubliclyListed
                && (competition.Status == "Closed"
                    || competition.Status == "Archived"
                    || competition.ArchivedAt != null)
            select new LinkedCompetitionRow(
                competition.Id,
                competition.CompetitionKey,
                stacker.StackerCode))
            .ToListAsync(cancellationToken);

        var selections = new List<IdentityLinkedFinalsPlacementSelection>();
        var governanceService = new FinalsRankingGovernanceService(database);

        foreach (var group in linkedRows.GroupBy(item => item.CompetitionId))
        {
            // Historical participant-code association must be unambiguous. Do not choose by name,
            // current registration attributes or row order.
            if (group.Count() != 1) continue;
            var linked = group.Single();
            if (string.IsNullOrWhiteSpace(linked.ParticipantCode)) continue;

            FinalsRankingGovernanceRecord? governance;
            try
            {
                governance = await governanceService.GetAsync(linked.CompetitionId, cancellationToken);
            }
            catch (DbException)
            {
                // Preserve the existing public profile if a deployment reaches an environment where
                // the governance persistence boundary has not been installed yet.
                return Empty(normalizedId);
            }

            selections.AddRange(ReadImmutableSelections(governance, linked));
        }

        try
        {
            var source = await new IdentityLinkedFinalsPlacementCareerService(database)
                .GetPublicEligibleAsync(normalizedId, selections, cancellationToken);
            if (source is null) return null;
            return PublicFinalsPlacementPublicationContract.Create(source);
        }
        catch (InvalidOperationException)
        {
            // Fail closed for permanent placement while preserving the pre-existing public profile.
            return Empty(normalizedId);
        }
    }

    private static IReadOnlyList<IdentityLinkedFinalsPlacementSelection> ReadImmutableSelections(
        FinalsRankingGovernanceRecord? governance,
        LinkedCompetitionRow linked)
    {
        if (governance?.HasSnapshot != true
            || !string.Equals(governance.RuleVersion, FinalsRankingRuleVersions.GovernedFinalsV2, StringComparison.Ordinal)
            || !string.Equals(governance.SnapshotSchemaVersion, FinalsRankingGovernanceService.GovernedV2SnapshotSchemaVersion, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(governance.SnapshotJson)
            || string.IsNullOrWhiteSpace(governance.SnapshotSha256)
            || governance.SourceStateRevision.GetValueOrDefault() <= 0
            || governance.SourceResultsRevision.GetValueOrDefault() <= 0
            || governance.SnapshotCapturedAt is null)
        {
            return [];
        }

        var snapshotJson = governance.SnapshotJson!;
        var computedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson)));
        if (!string.Equals(computedHash, governance.SnapshotSha256, StringComparison.OrdinalIgnoreCase))
            return [];

        try
        {
            using var document = JsonDocument.Parse(snapshotJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !StringEquals(root, "schemaVersion", FinalsRankingGovernanceService.GovernedV2SnapshotSchemaVersion)
                || !StringEquals(root, "ruleVersion", FinalsRankingRuleVersions.GovernedFinalsV2)
                || !StringEquals(root, "operatorContractVersion", FinalsRankingCertificationReadinessService.OperatorContractVersion))
            {
                return [];
            }

            if (!root.TryGetProperty("competition", out var competition)
                || competition.ValueKind != JsonValueKind.Object
                || !StringEquals(competition, "competitionKey", linked.CompetitionKey)
                || ReadInt64(competition, "stateRevision") != governance.SourceStateRevision
                || ReadInt64(competition, "resultsRevision") != governance.SourceResultsRevision)
            {
                return [];
            }

            if (!root.TryGetProperty("competitionState", out var competitionState)
                || competitionState.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("finalsResults", out var finalsResults)
                || finalsResults.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var division = ReadParticipantDivision(competitionState, linked.ParticipantCode);
            if (string.IsNullOrWhiteSpace(division)
                || division.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            var events = new HashSet<string>(StringComparer.Ordinal);
            foreach (var result in finalsResults.EnumerateArray())
            {
                if (result.ValueKind != JsonValueKind.Object) return [];
                if (!StringEquals(result, "participantType", "Individual")
                    || !StringEquals(result, "participantCode", linked.ParticipantCode))
                {
                    continue;
                }

                var eventCode = NormalizeEvent(ReadString(result, "eventCode"));
                if (eventCode is not null) events.Add(eventCode);
            }

            return events
                .OrderBy(EventSort)
                .Select(eventCode => new IdentityLinkedFinalsPlacementSelection(
                    linked.CompetitionId,
                    new FinalsHistoricalPlacementScope(
                        "Individual",
                        division,
                        eventCode,
                        PublicFinalsPlacementPublicationContract.RequiredCategory,
                        PublicFinalsPlacementPublicationContract.RequiredGender)))
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ReadParticipantDivision(JsonElement competitionState, string participantCode)
    {
        if (!competitionState.TryGetProperty("stackers", out var stackers)
            || stackers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? division = null;
        var matches = 0;
        foreach (var participant in stackers.EnumerateArray())
        {
            if (participant.ValueKind != JsonValueKind.Object
                || !StringEquals(participant, "id", participantCode))
            {
                continue;
            }

            matches++;
            division = ReadString(participant, "division")?.Trim();
        }

        return matches == 1 ? division : null;
    }

    private static string? NormalizeEvent(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "3-3-3" => "3-3-3",
            "3-6-3" => "3-6-3",
            "cycle" => "Cycle",
            _ => null
        };

    private static int EventSort(string eventCode)
    {
        var index = Array.IndexOf(EventOrder, eventCode);
        return index < 0 ? int.MaxValue : index;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static long? ReadInt64(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Number
        && property.TryGetInt64(out var value)
            ? value
            : null;

    private static bool StringEquals(JsonElement element, string propertyName, string expected) =>
        string.Equals(ReadString(element, propertyName), expected, StringComparison.Ordinal);

    private static PublicFinalsPlacementCareerPublication Empty(string normalizedId) =>
        new(
            PublicFinalsPlacementPublicationContract.PublicationVersion,
            normalizedId,
            PublicFinalsPlacementPublicationContract.CohortPolicy,
            []);

    private sealed record LinkedCompetitionRow(
        int CompetitionId,
        string CompetitionKey,
        string ParticipantCode);
}
