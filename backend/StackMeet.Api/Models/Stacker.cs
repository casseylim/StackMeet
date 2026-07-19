namespace StackMeet.Api.Models;

public sealed class Stacker
{
    public int Id { get; set; }
    public int CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;
    public string StackerCode { get; set; } = string.Empty;
    public string? WssaId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? Club { get; set; }
    public string? Region { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CustomDivision { get; set; }
    public string Paid { get; set; } = string.Empty;
    public string CheckedIn { get; set; } = string.Empty;
    public bool IsSpecialStacker { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
