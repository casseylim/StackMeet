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
