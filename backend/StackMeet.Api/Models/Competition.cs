namespace StackMeet.Api.Models;

public sealed class Competition
{
    public int Id { get; set; }
    public string CompetitionCode { get; set; } = string.Empty;
    public string CompetitionKey { get; set; } = string.Empty;
    public string CompetitionName { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<Stacker> Stackers { get; set; } = [];
}