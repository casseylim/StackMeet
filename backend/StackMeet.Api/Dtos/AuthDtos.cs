namespace StackMeet.Api.Dtos;

public sealed record LoginRequest(string? CompetitionId, string? Password, string? DisplayName);

public sealed record LoginResponse(string Token, string CompetitionId, string DisplayName, DateTimeOffset ExpiresAt);
