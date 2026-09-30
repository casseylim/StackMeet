namespace StackMeet.Api.Activities.SportStacking.Identity;

public static class PublicTeamCareerPublicationBlockers
{
    public const string SourceNotPublicationSafe = "public-team-career-source-not-safe";
    public const string EvidenceNotPublicationSafe = "public-team-career-evidence-not-safe";
    public const string ContextAmbiguous = "public-team-career-context-ambiguous";
}

/// <summary>
/// One privacy-minimized public Doubles or Timed Relay career fact.
/// Team code, teammate/member information and server provenance are intentionally absent.
/// </summary>
public sealed record PublicTeamCareerPoint(
    string CompetitionKey,
    string CompetitionName,
    DateOnly CompetitionDate,
    string ParticipantType,
    string Stage,
    string EventCode,
    string ResultStatus,
    decimal? OfficialBestTime,
    decimal? RawBestTime,
    decimal AppliedPenalty);

/// <summary>
/// SP-4R publication contract for identity-linked team career history.
/// This contract is not activated on the public profile until a separately reviewed integration phase.
/// </summary>
public sealed record PublicTeamCareerPublication(
    string PublicationVersion,
    string NadiTrackId,
    string PrivacyPolicy,
    IReadOnlyList<PublicTeamCareerPoint> History);

/// <summary>
/// Converts the internal SP-4Q team-career read model into the only team-history shape that may
/// be considered for later permanent public-profile integration.
/// </summary>
/// <remarks>
/// The source must already represent a reviewed permanent identity and finalized/public competition.
/// SP-4R additionally validates team/result/evidence invariants, strips all membership identifiers and
/// refuses histories where multiple internal team entries would collapse into one indistinguishable
/// public competition/type/stage/event context.
/// </remarks>
public static class PublicTeamCareerPublicationContract
{
    public const string PublicationVersion = "sp4r-public-team-career-v1";
    public const string PrivacyPolicy = "Verified team membership · teammate identities withheld";

    private static readonly HashSet<string> SupportedEvents = new(StringComparer.Ordinal)
    {
        "3-3-3",
        "3-6-3",
        "Cycle"
    };

    public static PublicTeamCareerPublication Create(IdentityLinkedTeamCareerReadModel source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!NadiTrackIdRules.IsValid(source.NadiTrackId))
        {
            throw Blocked(
                PublicTeamCareerPublicationBlockers.SourceNotPublicationSafe,
                "Public team career requires a valid permanent NADITrack ID.");
        }

        var history = new List<PublicTeamCareerPoint>(source.History.Count);
        var publicContexts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var point in source.History)
        {
            EnsurePublicationSafeSource(point);
            var resultStatus = EnsurePublicationSafeEvidence(point);

            var publicContext = string.Join(
                "\u001f",
                point.CompetitionKey,
                point.ParticipantType,
                point.Stage,
                point.EventCode);

            if (!publicContexts.Add(publicContext))
            {
                throw Blocked(
                    PublicTeamCareerPublicationBlockers.ContextAmbiguous,
                    "Multiple internal team entries collapse into the same privacy-minimized public context.");
            }

            history.Add(new PublicTeamCareerPoint(
                point.CompetitionKey,
                point.CompetitionName,
                point.CompetitionDate,
                point.ParticipantType,
                point.Stage,
                point.EventCode,
                resultStatus,
                point.OfficialBestTime,
                point.RawBestTime,
                point.AppliedPenalty));
        }

        return new PublicTeamCareerPublication(
            PublicationVersion,
            NadiTrackIdRules.Normalize(source.NadiTrackId),
            PrivacyPolicy,
            history);
    }

    private static void EnsurePublicationSafeSource(IdentityLinkedTeamCareerPoint point)
    {
        var participantTypeValid = point.ParticipantType is "Doubles" or "Timed Relay";
        var stageValid = point.Stage is "Prelims" or "Finals";
        var eventValid = SupportedEvents.Contains(point.EventCode)
            && (point.ParticipantType != "Timed Relay" || point.EventCode == "3-6-3");
        var competitionValid = !string.IsNullOrWhiteSpace(point.CompetitionKey)
            && !string.IsNullOrWhiteSpace(point.CompetitionName)
            && point.CompetitionDate != default;
        var teamReferenceValid = !string.IsNullOrWhiteSpace(point.TeamCode);

        var membershipShapeValid = point.ParticipantType switch
        {
            "Doubles" => point.RegisteredMemberCount is 1 or 2
                && (point.RegisteredMemberCount != 1 || point.HasExternalPartner)
                && (point.RegisteredMemberCount != 2 || !point.HasExternalPartner),
            "Timed Relay" => point.RegisteredMemberCount >= 4 && !point.HasExternalPartner,
            _ => false
        };

        if (!participantTypeValid
            || !stageValid
            || !eventValid
            || !competitionValid
            || !teamReferenceValid
            || !membershipShapeValid)
        {
            throw Blocked(
                PublicTeamCareerPublicationBlockers.SourceNotPublicationSafe,
                "Public team career requires a supported finalized team context with internally consistent membership shape.");
        }
    }

    private static string EnsurePublicationSafeEvidence(IdentityLinkedTeamCareerPoint point)
    {
        var evidence = point.Evidence;
        var provenanceValid = evidence.CompetitionStateRevision > 0
            && evidence.CompetitionResultsRevision > 0
            && evidence.ResultRevision > 0
            && IsSha256(evidence.RegisteredMembershipSha256);

        if (!provenanceValid)
        {
            throw Blocked(
                PublicTeamCareerPublicationBlockers.EvidenceNotPublicationSafe,
                "Public team career requires complete SP-4Q result and registered-membership provenance.");
        }

        var resultStatus = NormalizeResultStatus(point.ResultStatus);
        var resultSemanticsValid = resultStatus == "Valid"
            ? point.OfficialBestTime is > 0m
                && point.RawBestTime is > 0m
                && point.AppliedPenalty is >= 0m and < 999m
                && point.OfficialBestTime == point.RawBestTime + point.AppliedPenalty
            : resultStatus is not null
                && point.OfficialBestTime is null
                && point.RawBestTime is null
                && point.AppliedPenalty == 0m;

        if (resultStatus is null || !resultSemanticsValid)
        {
            throw Blocked(
                PublicTeamCareerPublicationBlockers.EvidenceNotPublicationSafe,
                "Team result status/time/penalty semantics are inconsistent with SP-4Q evidence.");
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
