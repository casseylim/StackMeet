namespace StackMeet.Api.Dtos;
public sealed record CompetitionRequest(string CompetitionCode, string CompetitionName, string Venue, DateOnly StartDate, DateOnly EndDate, string Status, bool IsPubliclyListed = false);
public sealed record CompetitionResponse(int Id, string CompetitionCode, string CompetitionName, string Venue, DateOnly StartDate, DateOnly EndDate, string Status, bool IsPubliclyListed, DateTime CreatedAt, DateTime UpdatedAt);
