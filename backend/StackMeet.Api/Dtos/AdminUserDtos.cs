namespace StackMeet.Api.Dtos;

/// <summary>
/// Request for manually creating an account before email invitations exist.
/// </summary>
/// <remarks>
/// Phase 1 uses email as the login name, but no outbound email is sent.
/// </remarks>
public sealed record AdminCreateUserRequest(
    string Email,
    string DisplayName,
    string Password,
    bool IsSystemAdmin = false,
    bool EmailConfirmed = true);

/// <summary>
/// Request for inviting a new account by email.
/// </summary>
/// <remarks>
/// The created user is inactive until they open the activation link and set a password.
/// </remarks>
public sealed record AdminInviteUserRequest(
    string Email,
    string DisplayName,
    bool IsSystemAdmin = false,
    IReadOnlyCollection<AdminCompetitionAccessRequest>? CompetitionAccess = null);

/// <summary>
/// Request for updating account status and display metadata.
/// </summary>
/// <remarks>
/// Password changes use a separate endpoint so user metadata updates cannot accidentally rotate credentials.
/// </remarks>
public sealed record AdminUpdateUserRequest(
    string DisplayName,
    bool IsActive,
    bool IsSystemAdmin,
    bool EmailConfirmed);

/// <summary>
/// Request for replacing an account password during manual Phase 1 administration.
/// </summary>
/// <remarks>
/// This is intentionally admin-driven until self-service reset emails are implemented.
/// </remarks>
public sealed record AdminSetUserPasswordRequest(string Password);

/// <summary>
/// Request for deleting an account after an explicit admin confirmation.
/// </summary>
/// <remarks>
/// The confirmation text prevents accidental deletion from the admin edit screen.
/// </remarks>
public sealed record AdminDeleteUserRequest(string? Confirmation);

/// <summary>
/// Response returned after sending account email links.
/// </summary>
/// <remarks>
/// PreviewLink is returned for local/manual testing; production UI should not expose it broadly.
/// </remarks>
public sealed record AdminEmailLinkResponse(string Message, string? PreviewLink);

/// <summary>
/// Request for assigning or changing one user's role for a competition.
/// </summary>
/// <remarks>
/// The unique key is user plus competition; posting again updates the existing assignment.
/// </remarks>
public sealed record AdminCompetitionAccessRequest(int CompetitionId, string Role, bool IsActive = true);

/// <summary>
/// Account summary returned to system administrators.
/// </summary>
/// <remarks>
/// Password hashes are never returned.
/// </remarks>
public sealed record AdminUserResponse(
    int Id,
    string Email,
    string DisplayName,
    bool IsActive,
    bool EmailConfirmed,
    bool IsSystemAdmin,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    IReadOnlyCollection<AdminCompetitionAccessResponse> CompetitionAccess);

/// <summary>
/// Competition access assignment summary returned with account administration responses.
/// </summary>
/// <remarks>
/// This mirrors the per-competition role model used by the authorization service.
/// </remarks>
public sealed record AdminCompetitionAccessResponse(
    int Id,
    int CompetitionId,
    string CompetitionKey,
    string CompetitionName,
    string Role,
    bool IsActive,
    DateTime AssignedAt);
