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
    /// <summary>Controls whether this competition appears on the public all-competitions directory.</summary>
    public bool IsPubliclyListed { get; set; }
    /// <summary>Latest committed mutation revision for this competition's result dataset.</summary>
    public long ResultsRevision { get; set; }
    public string? PasswordHash { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<Stacker> Stackers { get; set; } = [];
    public ICollection<CompetitionUser> CompetitionUsers { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
    public ICollection<CompetitionResult> Results { get; set; } = [];
    public ICollection<CompetitionAsset> Assets { get; set; } = [];
}
