namespace StackMeet.Api.Models;

public sealed class CompetitionState
{
    public int Id { get; set; }
    public string CompetitionKey { get; set; } = string.Empty;
    public string JsonData { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = "0.9-online";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}