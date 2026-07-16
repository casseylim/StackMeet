namespace StackMeet.Api.Dtos;
public sealed record CompetitionRequest(string CompetitionCode, string CompetitionName, string Venue, DateOnly StartDate, DateOnly EndDate, string Status);
public sealed record CompetitionResponse(int Id, string CompetitionCode, string CompetitionName, string Venue, DateOnly StartDate, DateOnly EndDate, string Status, DateTime CreatedAt, DateTime UpdatedAt);
