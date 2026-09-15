using StackMeet.Api.Activities.SportStacking.Ranking;

namespace StackMeet.Api.Activities.SportStacking.Identity;

public static class PublicFinalsPlacementPublicationBlockers
{
    public const string ScopeNotPublicationSafe = "public-placement-scope-not-safe";
    public const string EvidenceNotPublicationSafe = "public-placement-evidence-not-safe";
}

/// <summary>
/// One privacy-minimized public Finals placement fact.
/// The exact historical division remains in the server-owned SP-4L source model because a division
/// label may itself encode a personal attribute (for example, Male/Female). The public contract
/// instead states the fixed cohort policy without exposing the raw division label.
/// </summary>
public sealed record PublicFinalsPlacementPoint(
    string CompetitionKey,
    string CompetitionName,
    DateOnly CompetitionDate,
    string EventCode,
    string ResultStatus,
    decimal? OfficialBestTime,
    int? Placement,
    bool SharesPlacement);

/// <summary>
/// SP-4M privacy-safe publication contract for identity-linked immutable Finals placement facts.
/// </summary>
public sealed record PublicFinalsPlacementCareerPublication(
    string PublicationVersion,
    string NadiTrackId,
    string CohortPolicy,
    IReadOnlyList<PublicFinalsPlacementPoint> History);

/// <summary>
/// Converts an SP-4L server-owned placement career read model into the only placement shape that
/// may be considered for public-profile integration.
/// </summary>
/// <remarks>
/// Publication is intentionally restricted to mixed-category projections with no additional gender
/// filter. This avoids disclosing an athlete's gender or Special status through the permanent profile.
/// The exact division remains immutable internal ranking authority but is not copied to the public
/// contract because some historical division labels contain gendered text. The public claim therefore
/// remains explicitly contextualized as placement within the athlete's competition-time division,
/// under the mixed category, with no additional gender filter applied by the ranking projection.
/// </remarks>
public static class PublicFinalsPlacementPublicationContract
{
    public const string PublicationVersion = "sp4m-public-finals-placement-v1";
    public const string CohortPolicy = "Competition-time division · mixed category · no additional gender filter";
    public const string RequiredCategory = "mixed";
    public const string RequiredGender = "all";

    private static readonly HashSet<string> SupportedEvents = new(StringComparer.Ordinal)
    {
        "3-3-3",
        "3-6-3",
        "Cycle"
    };

    public static PublicFinalsPlacementCareerPublication Create(
        IdentityLinkedFinalsPlacementCareerReadModel source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var history = new List<PublicFinalsPlacementPoint>(source.History.Count);
        foreach (var point in source.History)
        {
            EnsurePublicationSafeScope(point.Scope);
            var resultStatus = EnsurePublicationSafeEvidence(point);

            history.Add(new PublicFinalsPlacementPoint(
                point.CompetitionKey,
                point.CompetitionName,
                point.CompetitionDate,
                point.Scope.EventCode,
                resultStatus,
                point.OfficialBestTime,
                point.Placement,
                point.SharesPlacement));
        }

        return new PublicFinalsPlacementCareerPublication(
            PublicationVersion,
            source.NadiTrackId,
            CohortPolicy,
            history);
    }

    private static void EnsurePublicationSafeScope(FinalsHistoricalPlacementScope scope)
    {
        var valid = string.Equals(scope.ParticipantType, "Individual", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(scope.Division)
            && !string.Equals(scope.Division.Trim(), "all", StringComparison.OrdinalIgnoreCase)
            && SupportedEvents.Contains(scope.EventCode)
            && string.Equals(scope.Category, RequiredCategory, StringComparison.Ordinal)
            && string.Equals(scope.Gender, RequiredGender, StringComparison.Ordinal);

        if (!valid)
        {
            throw Blocked(
                PublicFinalsPlacementPublicationBlockers.ScopeNotPublicationSafe,
                "Public Finals placement requires Individual + explicit competition-time division + supported event + mixed category + no additional gender filter.");
        }
    }

    private static string EnsurePublicationSafeEvidence(IdentityLinkedFinalsPlacementCareerPoint point)
    {
        var evidence = point.Evidence;
        var provenanceValid = string.Equals(
                evidence.ProjectionVersion,
                FinalsHistoricalPlacementProjectionService.ProjectionVersion,
                StringComparison.Ordinal)
            && string.Equals(
                evidence.RuleVersion,
                FinalsRankingRuleVersions.GovernedFinalsV2,
                StringComparison.Ordinal)
            && string.Equals(
                evidence.SnapshotSchemaVersion,
                FinalsRankingGovernanceService.GovernedV2SnapshotSchemaVersion,
                StringComparison.Ordinal)
            && string.Equals(
                evidence.OperatorContractVersion,
                FinalsRankingCertificationReadinessService.OperatorContractVersion,
                StringComparison.Ordinal)
            && IsSha256(evidence.SnapshotSha256)
            && evidence.SourceStateRevision > 0
            && evidence.SourceResultsRevision > 0
            && evidence.SnapshotCapturedAt != default;

        if (!provenanceValid)
        {
            throw Blocked(
                PublicFinalsPlacementPublicationBlockers.EvidenceNotPublicationSafe,
                "Public Finals placement requires complete governed-v2 immutable SP-4K provenance from the reviewed operator contract.");
        }

        var resultStatus = NormalizeResultStatus(point.ResultStatus);
        var resultSemanticsValid = resultStatus == "Valid"
            ? point.Placement is > 0 && point.OfficialBestTime is > 0m
            : resultStatus is not null
                && point.Placement is null
                && point.OfficialBestTime is null
                && !point.SharesPlacement;

        if (resultStatus is null || !resultSemanticsValid)
        {
            throw Blocked(
                PublicFinalsPlacementPublicationBlockers.EvidenceNotPublicationSafe,
                "Placement/status semantics are inconsistent with immutable SP-4K Finals evidence.");
        }

        return resultStatus;
    }

    private static string? NormalizeResultStatus(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "valid" => "Valid",
            "scratch" => "Scratch",
            "missing" => "Missing",
            "invalid" => "Invalid",
            _ => null
        };

    private static bool IsSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'A' and <= 'F'
            or >= 'a' and <= 'f');

    private static InvalidOperationException Blocked(string code, string detail) =>
        new($"{code}: {detail}");
}
