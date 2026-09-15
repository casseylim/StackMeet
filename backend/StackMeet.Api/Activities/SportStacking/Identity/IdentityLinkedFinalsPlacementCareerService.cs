using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Ranking;
using StackMeet.Api.Data;
using StackMeet.Api.Services;

namespace StackMeet.Api.Activities.SportStacking.Identity;

public static class IdentityLinkedFinalsPlacementCareerBlockers
{
    public const string IdentityLinkAmbiguous = "identity-link-ambiguous";
    public const string CompetitionProvenanceMismatch = "competition-provenance-mismatch";
}

/// <summary>
/// One explicitly-scoped SP-4K projection request to bind into an identity career view.
/// The competition id is an internal lookup key and is deliberately not part of the output contract.
/// </summary>
public sealed record IdentityLinkedFinalsPlacementSelection(
    int CompetitionId,
    FinalsHistoricalPlacementScope Scope);

/// <summary>
/// Immutable evidence provenance retained with an identity-linked placement fact.
/// </summary>
public sealed record IdentityLinkedFinalsPlacementEvidence(
    string ProjectionVersion,
    string RuleVersion,
    string SnapshotSchemaVersion,
    string OperatorContractVersion,
    string SnapshotSha256,
    long SourceStateRevision,
    long SourceResultsRevision,
    DateTime SnapshotCapturedAt);

/// <summary>
/// One finalized/public, identity-linked Finals placement fact.
/// Placement remains nullable for Scratch, Missing and Invalid SP-4K rows.
/// </summary>
public sealed record IdentityLinkedFinalsPlacementCareerPoint(
    string CompetitionKey,
    string CompetitionName,
    DateOnly CompetitionDate,
    FinalsHistoricalPlacementScope Scope,
    string ResultStatus,
    decimal? OfficialBestTime,
    int? Placement,
    bool SharesPlacement,
    IdentityLinkedFinalsPlacementEvidence Evidence);

/// <summary>
/// Server-owned SP-4L career read model. It is intentionally not exposed by a controller or public profile yet.
/// </summary>
public sealed record IdentityLinkedFinalsPlacementCareerReadModel(
    string NadiTrackId,
    IReadOnlyList<IdentityLinkedFinalsPlacementCareerPoint> History);

/// <summary>
/// SP-4L read-only bridge from approved historical identity links to immutable SP-4K placement projections.
/// </summary>
/// <remarks>
/// The service never ranks current results and never matches by name. The approved StackerIdentityLink supplies
/// the competition-scoped participant code, while SP-4K remains the sole placement calculator and immutable
/// competition snapshot authority. Only public identities and finalized, publicly-listed competitions are eligible.
/// </remarks>
public sealed class IdentityLinkedFinalsPlacementCareerService(StackMeetDbContext database)
{
    private static readonly string[] EventOrder = ["3-3-3", "3-6-3", "Cycle"];

    public async Task<IdentityLinkedFinalsPlacementCareerReadModel?> GetPublicEligibleAsync(
        string? nadiTrackId,
        IReadOnlyCollection<IdentityLinkedFinalsPlacementSelection>? selections,
        CancellationToken cancellationToken = default)
    {
        if (!NadiTrackIdRules.IsValid(nadiTrackId)) return null;
        var normalizedId = NadiTrackIdRules.Normalize(nadiTrackId!);

        var identity = await database.SportStackerIdentities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.NadiTrackId == normalizedId && item.IsPublicProfile,
                cancellationToken);

        // Preserve the existing public privacy boundary: private, unknown and malformed identities are indistinguishable.
        if (identity is null) return null;

        if (selections is null || selections.Count == 0)
            return new IdentityLinkedFinalsPlacementCareerReadModel(identity.NadiTrackId, []);

        var requestedCompetitionIds = selections
            .Select(item => item.CompetitionId)
            .Distinct()
            .ToArray();

        var linkedRows = await (
            from link in database.StackerIdentityLinks.AsNoTracking()
            join stacker in database.Stackers.AsNoTracking() on link.StackerId equals stacker.Id
            join competition in database.Competitions.AsNoTracking() on stacker.CompetitionId equals competition.Id
            where link.SportStackerIdentityId == identity.Id
                && requestedCompetitionIds.Contains(competition.Id)
                && competition.IsPubliclyListed
                && (competition.Status == "Closed"
                    || competition.Status == "Archived"
                    || competition.ArchivedAt != null)
            select new LinkedCompetitionRow(
                competition.Id,
                competition.CompetitionKey,
                competition.CompetitionName,
                competition.StartDate,
                stacker.StackerCode))
            .ToListAsync(cancellationToken);

        var linkedByCompetition = new Dictionary<int, LinkedCompetitionRow>();
        foreach (var group in linkedRows.GroupBy(item => item.CompetitionId))
        {
            if (group.Count() != 1)
            {
                throw Blocked(
                    IdentityLinkedFinalsPlacementCareerBlockers.IdentityLinkAmbiguous,
                    $"Permanent identity has {group.Count()} reviewed Stacker links for competition {group.Key}; historical placement cannot choose one implicitly.");
            }

            var row = group.Single();
            if (string.IsNullOrWhiteSpace(row.ParticipantCode))
            {
                throw Blocked(
                    IdentityLinkedFinalsPlacementCareerBlockers.IdentityLinkAmbiguous,
                    $"Reviewed identity link for competition {group.Key} has no participant code.");
            }

            linkedByCompetition.Add(group.Key, row);
        }

        var projector = new FinalsHistoricalPlacementProjectionService(database);
        var history = new List<IdentityLinkedFinalsPlacementCareerPoint>();
        var seenScopes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var selection in selections)
        {
            // No eligible reviewed link means this scope cannot contribute to this permanent career.
            if (!linkedByCompetition.TryGetValue(selection.CompetitionId, out var linked)) continue;

            // SP-4K validates and normalizes the scope and remains the only historical placement calculator.
            var projection = await projector.ProjectAsync(
                selection.CompetitionId,
                selection.Scope,
                cancellationToken);

            if (projection.CompetitionId != linked.CompetitionId
                || !string.Equals(projection.CompetitionKey, linked.CompetitionKey, StringComparison.Ordinal))
            {
                throw Blocked(
                    IdentityLinkedFinalsPlacementCareerBlockers.CompetitionProvenanceMismatch,
                    "Immutable placement evidence does not match the linked competition provenance.");
            }

            var placementRow = projection.Rows
                .SingleOrDefault(item => string.Equals(item.ParticipantCode, linked.ParticipantCode, StringComparison.Ordinal));

            // An explicit category/gender scope may legitimately exclude the linked athlete.
            if (placementRow is null) continue;

            var scope = projection.Scope;
            var scopeKey = string.Join(
                "\u001f",
                projection.CompetitionId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                scope.ParticipantType,
                scope.Division,
                scope.EventCode,
                scope.Category,
                scope.Gender);

            if (!seenScopes.Add(scopeKey)) continue;

            history.Add(new IdentityLinkedFinalsPlacementCareerPoint(
                linked.CompetitionKey,
                linked.CompetitionName,
                linked.CompetitionDate,
                scope,
                placementRow.ResultStatus,
                placementRow.OfficialBestTime,
                placementRow.Rank,
                placementRow.SharesRank,
                new IdentityLinkedFinalsPlacementEvidence(
                    projection.ProjectionVersion,
                    projection.RuleVersion,
                    projection.SnapshotSchemaVersion,
                    projection.OperatorContractVersion,
                    projection.SnapshotSha256,
                    projection.SourceStateRevision,
                    projection.SourceResultsRevision,
                    projection.SnapshotCapturedAt)));
        }

        var ordered = history
            .OrderBy(item => item.CompetitionDate)
            .ThenBy(item => item.CompetitionKey, StringComparer.Ordinal)
            .ThenBy(item => EventSort(item.Scope.EventCode))
            .ThenBy(item => item.Scope.Division, StringComparer.Ordinal)
            .ThenBy(item => item.Scope.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Scope.Gender, StringComparer.Ordinal)
            .ToArray();

        return new IdentityLinkedFinalsPlacementCareerReadModel(identity.NadiTrackId, ordered);
    }

    private static int EventSort(string eventCode)
    {
        var index = Array.IndexOf(EventOrder, eventCode);
        return index < 0 ? int.MaxValue : index;
    }

    private static InvalidOperationException Blocked(string code, string detail) =>
        new($"{code}: {detail}");

    private sealed record LinkedCompetitionRow(
        int CompetitionId,
        string CompetitionKey,
        string CompetitionName,
        DateOnly CompetitionDate,
        string ParticipantCode);
}
