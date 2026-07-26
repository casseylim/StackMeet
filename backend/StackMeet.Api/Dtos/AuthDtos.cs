namespace StackMeet.Api.Dtos;

public sealed record LoginRequest(string? CompetitionId, string? Email, string? Password, string? DisplayName);

public sealed record LoginResponse(
    string Token,
    string? CompetitionId,
    string DisplayName,
    DateTimeOffset ExpiresAt,
    int? UserId = null,
    string? Email = null,
    bool IsSystemAdmin = false,
    IReadOnlyCollection<CompetitionAccessResponse>? CompetitionAccess = null);

public sealed record CurrentUserResponse(
    int? UserId,
    string? Email,
    string DisplayName,
    bool IsSystemAdmin,
    DateTimeOffset ExpiresAt,
    IReadOnlyCollection<CompetitionAccessResponse> CompetitionAccess);

public sealed record CompetitionAccessResponse(
    int CompetitionId,
    string CompetitionKey,
    string CompetitionName,
    string Role);

/// <summary>
/// Request for setting a password from an activation email link.
/// </summary>
/// <remarks>
/// The token is consumed once and activates the account.
/// </remarks>
public sealed record ActivateAccountRequest(string Token, string Password, string? DisplayName = null);

/// <summary>
/// Request for setting a new password from a reset email link.
/// </summary>
/// <remarks>
/// The token is consumed once; the account must already exist.
/// </remarks>
public sealed record ResetPasswordRequest(string Token, string Password);
