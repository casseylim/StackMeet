namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// Persistence command produced only after SP-0C has approved the identity decision.
/// The competition Stacker row remains the registration snapshot and supplies the initial permanent-profile data for CreateNew.
/// </summary>
public sealed record StackerIdentityPersistenceRequest(
    int StackerId,
    StackerIdentityResolutionDecision Resolution,
    string? ResolutionNote,
    int? LinkedByUserId);

public sealed record StackerIdentityPersistenceResult(
    SportStackerIdentity Identity,
    StackerIdentityLink Link,
    bool IdentityCreated);
