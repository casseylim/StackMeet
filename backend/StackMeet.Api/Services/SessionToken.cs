namespace StackMeet.Api.Services;

/// <summary>
/// Represents the validated identity stored in a StackMeet bearer session token.
/// </summary>
/// <remarks>
/// CompetitionId is populated for legacy sessions; UserId and Email are populated for account sessions.
/// </remarks>
public sealed record SessionToken(
    string CompetitionId,
    string DisplayName,
    DateTimeOffset ExpiresAt,
    int? UserId = null,
    string? Email = null,
    bool IsSystemAdmin = false)
{
    public bool IsAccountSession => UserId is not null;
}
