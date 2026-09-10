namespace StackMeet.Api.Activities.SportStacking.Identity;

public enum StackerIdentityResolutionAction
{
    None = 0,
    LinkExisting = 1,
    CreateNew = 2
}

public enum StackerIdentityResolutionStatus
{
    Approved = 0,
    LookupBlocked = 1,
    ActionRequired = 2,
    InvalidSelection = 3,
    ConfirmationRequired = 4,
    ResolutionNoteRequired = 5,
    DuplicateOverrideRequired = 6,
    ExactIdentityMustLink = 7
}

/// <summary>
/// Explicit operator intent applied after SP-0B candidate discovery and before any future persistence write.
/// The two confirmation flags are intentionally separate so confirming an existing candidate cannot
/// accidentally authorize creation of a second permanent identity.
/// </summary>
public sealed record StackerIdentityResolutionRequest(
    StackerIdentityResolutionAction RequestedAction,
    string? SelectedNadiTrackId,
    bool CandidateConfirmed,
    bool CreateNewOverrideConfirmed,
    string? ResolutionNote);

/// <summary>
/// Pure policy decision. SP-0C does not persist this decision; SP-1 can later consume only Approved decisions.
/// </summary>
public sealed record StackerIdentityResolutionDecision(
    StackerIdentityResolutionStatus Status,
    StackerIdentityResolutionAction Action,
    SportStackerIdentity? SelectedIdentity,
    string? LinkMatchMethod,
    string ReasonCode)
{
    public bool CanProceedToPersistence => Status == StackerIdentityResolutionStatus.Approved;
}
