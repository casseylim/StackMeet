namespace StackMeet.Api.Services;

public sealed record SessionToken(string CompetitionId, string DisplayName, DateTimeOffset ExpiresAt);
