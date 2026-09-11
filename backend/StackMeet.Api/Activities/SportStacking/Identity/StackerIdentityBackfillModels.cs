namespace StackMeet.Api.Activities.SportStacking.Identity;

public enum StackerIdentityBackfillItemStatus
{
    NoCandidates = 0,
    ReviewRequired = 1
}

public enum StackerIdentityBackfillApplyStatus
{
    Applied = 0,
    AlreadyLinked = 1,
    ResolutionBlocked = 2
}

/// <summary>
/// Privacy-limited candidate summary for historical-link review. Matching may use email/phone evidence,
/// but those private values are deliberately not copied into the backfill report.
/// </summary>
public sealed record StackerIdentityBackfillCandidate(
    long IdentityId,
    string NadiTrackId,
    string DisplayName,
    DateOnly? BirthDate,
    string Country,
    string? Club,
    string? WssaId,
    StackerIdentityMatchStrength Strength,
    IReadOnlyList<string> Evidence);

public sealed record StackerIdentityBackfillItem(
    int StackerId,
    int CompetitionId,
    string StackerCode,
    string DisplayName,
    DateOnly? BirthDate,
    string Country,
    string? Club,
    string? WssaId,
    StackerIdentityBackfillItemStatus Status,
    IReadOnlyList<StackerIdentityBackfillCandidate> Candidates);

public sealed record StackerIdentityBackfillReport(
    int? CompetitionId,
    int TotalStackers,
    int LinkedStackers,
    int UnlinkedStackers,
    int ReturnedItems,
    int ReviewRequiredItems,
    int NoCandidateItems,
    bool HasMore,
    IReadOnlyList<StackerIdentityBackfillItem> Items);

/// <summary>
/// One explicit historical-link action. SP-2 intentionally does not accept a bulk list here;
/// every durable link/create decision crosses SP-0C and SP-1 independently.
/// </summary>
public sealed record StackerIdentityBackfillApplyRequest(
    int StackerId,
    string? ExplicitNadiTrackId,
    StackerIdentityResolutionAction RequestedAction,
    string? SelectedNadiTrackId,
    bool CandidateConfirmed,
    bool CreateNewOverrideConfirmed,
    string? ResolutionNote,
    int? LinkedByUserId);

public sealed record StackerIdentityBackfillApplyResult(
    StackerIdentityBackfillApplyStatus Status,
    int StackerId,
    string? NadiTrackId,
    StackerIdentityResolutionDecision? Resolution,
    StackerIdentityPersistenceResult? Persistence);
