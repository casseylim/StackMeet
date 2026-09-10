namespace StackMeet.Api.Activities.SportStacking.Identity;

public enum StackerIdentityMatchStrength
{
    None = 0,
    Possible = 1,
    Strong = 2,
    Authoritative = 3
}

public enum StackerIdentityLookupStatus
{
    CandidateSearch = 0,
    ExactNadiTrackIdMatch = 1,
    InvalidNadiTrackId = 2,
    NadiTrackIdNotFound = 3
}

/// <summary>
/// Identity evidence supplied while deciding whether a competition entrant is an existing NADITrack stacker or a new person.
/// </summary>
public sealed record StackerIdentityMatchQuery(
    string? NadiTrackId,
    string? WssaId,
    string? FirstName,
    string? LastName,
    DateOnly? BirthDate,
    string? Country,
    string? Club,
    string? Email,
    string? Phone);

/// <summary>
/// One ranked duplicate/existing-identity candidate. Only an exact explicit NADITrack ID is authoritative.
/// </summary>
public sealed record StackerIdentityMatchCandidate(
    SportStackerIdentity Identity,
    StackerIdentityMatchStrength Strength,
    IReadOnlyList<string> Evidence,
    bool IsAuthoritativeSelection);

public sealed record StackerIdentityMatchResult(
    StackerIdentityLookupStatus Status,
    StackerIdentityMatchCandidate? AuthoritativeMatch,
    IReadOnlyList<StackerIdentityMatchCandidate> Candidates)
{
    public bool RequiresOperatorConfirmation =>
        Status == StackerIdentityLookupStatus.CandidateSearch && Candidates.Count > 0;
}
