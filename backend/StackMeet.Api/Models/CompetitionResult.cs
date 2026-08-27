namespace StackMeet.Api.Models;

public sealed class CompetitionResult
{
    public long Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public int CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;
    public string Stage { get; set; } = string.Empty;
    public string ParticipantType { get; set; } = string.Empty;
    public string ParticipantCode { get; set; } = string.Empty;
    public string EventCode { get; set; } = string.Empty;
    public string AttemptsJson { get; set; } = "[]";
    public decimal Penalty { get; set; }
    /// <summary>Competition.ResultsRevision value at which this row was last changed; it is not an independent counter.</summary>
    public long Revision { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
