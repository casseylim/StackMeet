namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// SP-0C safety policy between candidate matching and future persistence.
/// It never writes, links, merges, or issues identifiers; it only decides whether operator intent is safe enough to proceed.
/// </summary>
public static class StackerIdentityResolutionPolicy
{
    public static StackerIdentityResolutionDecision Resolve(
        StackerIdentityMatchResult matchResult,
        StackerIdentityResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(matchResult);
        ArgumentNullException.ThrowIfNull(request);

        ValidateCandidateIntegrity(matchResult);

        if (matchResult.Status is StackerIdentityLookupStatus.InvalidNadiTrackId
            or StackerIdentityLookupStatus.NadiTrackIdNotFound)
        {
            return Decision(
                StackerIdentityResolutionStatus.LookupBlocked,
                StackerIdentityResolutionAction.None,
                null,
                null,
                "EXPLICIT_NADITRACK_LOOKUP_BLOCKED");
        }

        if (matchResult.Status == StackerIdentityLookupStatus.ExactNadiTrackIdMatch)
        {
            return ResolveExactNadiTrackId(matchResult, request);
        }

        if (matchResult.Status != StackerIdentityLookupStatus.CandidateSearch)
        {
            throw new InvalidOperationException($"Unsupported identity lookup status: {matchResult.Status}");
        }

        return request.RequestedAction switch
        {
            StackerIdentityResolutionAction.LinkExisting => ResolveCandidateLink(matchResult, request),
            StackerIdentityResolutionAction.CreateNew => ResolveCreateNew(matchResult, request),
            _ => Decision(
                StackerIdentityResolutionStatus.ActionRequired,
                StackerIdentityResolutionAction.None,
                null,
                null,
                "RESOLUTION_ACTION_REQUIRED")
        };
    }

    private static StackerIdentityResolutionDecision ResolveExactNadiTrackId(
        StackerIdentityMatchResult matchResult,
        StackerIdentityResolutionRequest request)
    {
        var authoritative = matchResult.AuthoritativeMatch
            ?? throw new InvalidOperationException("Exact NADITrack lookup is missing its authoritative match.");

        if (!authoritative.IsAuthoritativeSelection
            || authoritative.Strength != StackerIdentityMatchStrength.Authoritative)
        {
            throw new InvalidOperationException("Exact NADITrack lookup is not marked authoritative.");
        }

        if (request.RequestedAction == StackerIdentityResolutionAction.CreateNew)
        {
            return Decision(
                StackerIdentityResolutionStatus.ExactIdentityMustLink,
                StackerIdentityResolutionAction.None,
                authoritative.Identity,
                null,
                "EXACT_NADITRACK_IDENTITY_MUST_LINK");
        }

        if (request.RequestedAction != StackerIdentityResolutionAction.LinkExisting)
        {
            return Decision(
                StackerIdentityResolutionStatus.ActionRequired,
                StackerIdentityResolutionAction.None,
                authoritative.Identity,
                null,
                "EXACT_NADITRACK_LINK_ACTION_REQUIRED");
        }

        if (!string.IsNullOrWhiteSpace(request.SelectedNadiTrackId))
        {
            if (!NadiTrackIdRules.IsValid(request.SelectedNadiTrackId)
                || !SameNadiTrackId(request.SelectedNadiTrackId, authoritative.Identity.NadiTrackId))
            {
                return Decision(
                    StackerIdentityResolutionStatus.InvalidSelection,
                    StackerIdentityResolutionAction.None,
                    null,
                    null,
                    "SELECTED_IDENTITY_DOES_NOT_MATCH_EXACT_LOOKUP");
            }
        }

        return Decision(
            StackerIdentityResolutionStatus.Approved,
            StackerIdentityResolutionAction.LinkExisting,
            authoritative.Identity,
            StackerIdentityMatchMethod.NadiTrackId,
            "APPROVED_EXACT_NADITRACK_LINK");
    }

    private static StackerIdentityResolutionDecision ResolveCandidateLink(
        StackerIdentityMatchResult matchResult,
        StackerIdentityResolutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedNadiTrackId)
            || !NadiTrackIdRules.IsValid(request.SelectedNadiTrackId))
        {
            return Decision(
                StackerIdentityResolutionStatus.InvalidSelection,
                StackerIdentityResolutionAction.None,
                null,
                null,
                "VALID_CANDIDATE_SELECTION_REQUIRED");
        }

        var selected = matchResult.Candidates
            .SingleOrDefault(candidate => SameNadiTrackId(candidate.Identity.NadiTrackId, request.SelectedNadiTrackId));

        if (selected is null)
        {
            return Decision(
                StackerIdentityResolutionStatus.InvalidSelection,
                StackerIdentityResolutionAction.None,
                null,
                null,
                "SELECTED_IDENTITY_IS_NOT_A_MATCH_CANDIDATE");
        }

        if (selected.IsAuthoritativeSelection || selected.Strength == StackerIdentityMatchStrength.Authoritative)
        {
            throw new InvalidOperationException("Candidate-search result contains an unexpected authoritative candidate.");
        }

        if (!request.CandidateConfirmed)
        {
            return Decision(
                StackerIdentityResolutionStatus.ConfirmationRequired,
                StackerIdentityResolutionAction.None,
                selected.Identity,
                null,
                "CANDIDATE_CONFIRMATION_REQUIRED");
        }

        if (selected.Strength == StackerIdentityMatchStrength.Possible
            && string.IsNullOrWhiteSpace(request.ResolutionNote))
        {
            return Decision(
                StackerIdentityResolutionStatus.ResolutionNoteRequired,
                StackerIdentityResolutionAction.None,
                selected.Identity,
                null,
                "POSSIBLE_MATCH_REVIEW_NOTE_REQUIRED");
        }

        return Decision(
            StackerIdentityResolutionStatus.Approved,
            StackerIdentityResolutionAction.LinkExisting,
            selected.Identity,
            PreferredLinkMethod(selected),
            "APPROVED_CONFIRMED_CANDIDATE_LINK");
    }

    private static StackerIdentityResolutionDecision ResolveCreateNew(
        StackerIdentityMatchResult matchResult,
        StackerIdentityResolutionRequest request)
    {
        if (matchResult.Candidates.Count == 0)
        {
            return Decision(
                StackerIdentityResolutionStatus.Approved,
                StackerIdentityResolutionAction.CreateNew,
                null,
                StackerIdentityMatchMethod.CreatedNew,
                "APPROVED_NO_DUPLICATE_CANDIDATES");
        }

        if (!request.CreateNewOverrideConfirmed)
        {
            return Decision(
                StackerIdentityResolutionStatus.DuplicateOverrideRequired,
                StackerIdentityResolutionAction.None,
                null,
                null,
                "CREATE_NEW_DUPLICATE_OVERRIDE_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(request.ResolutionNote))
        {
            return Decision(
                StackerIdentityResolutionStatus.ResolutionNoteRequired,
                StackerIdentityResolutionAction.None,
                null,
                null,
                "CREATE_NEW_DUPLICATE_OVERRIDE_NOTE_REQUIRED");
        }

        return Decision(
            StackerIdentityResolutionStatus.Approved,
            StackerIdentityResolutionAction.CreateNew,
            null,
            StackerIdentityMatchMethod.CreatedNew,
            "APPROVED_CREATE_NEW_DESPITE_CANDIDATES");
    }

    private static string PreferredLinkMethod(StackerIdentityMatchCandidate candidate)
    {
        if (candidate.Evidence.Contains(StackerIdentityMatchMethod.WssaId)) return StackerIdentityMatchMethod.WssaId;
        if (candidate.Evidence.Contains(StackerIdentityMatchMethod.NameAndBirthDate)) return StackerIdentityMatchMethod.NameAndBirthDate;
        if (candidate.Evidence.Contains(StackerIdentityMatchMethod.Email)) return StackerIdentityMatchMethod.Email;
        if (candidate.Evidence.Contains(StackerIdentityMatchMethod.Phone)) return StackerIdentityMatchMethod.Phone;
        return StackerIdentityMatchMethod.Manual;
    }

    private static void ValidateCandidateIntegrity(StackerIdentityMatchResult matchResult)
    {
        var all = matchResult.Candidates.ToList();

        foreach (var candidate in all)
        {
            if (!NadiTrackIdRules.IsValid(candidate.Identity.NadiTrackId))
            {
                throw new InvalidOperationException(
                    $"Invalid permanent NADITrack ID found in match candidates: {candidate.Identity.NadiTrackId}");
            }
        }

        var duplicateId = all
            .GroupBy(candidate => NadiTrackIdRules.Normalize(candidate.Identity.NadiTrackId), StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;

        if (duplicateId is not null)
        {
            throw new InvalidOperationException($"Duplicate permanent NADITrack ID detected in candidates: {duplicateId}");
        }
    }

    private static bool SameNadiTrackId(string left, string right) =>
        string.Equals(
            NadiTrackIdRules.Normalize(left),
            NadiTrackIdRules.Normalize(right),
            StringComparison.Ordinal);

    private static StackerIdentityResolutionDecision Decision(
        StackerIdentityResolutionStatus status,
        StackerIdentityResolutionAction action,
        SportStackerIdentity? selectedIdentity,
        string? linkMatchMethod,
        string reasonCode) =>
        new(status, action, selectedIdentity, linkMatchMethod, reasonCode);
}
