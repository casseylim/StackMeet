namespace StackMeet.Api.Models;

/// <summary>
/// Defines the named access levels assignable within a competition.
/// </summary>
/// <remarks>
/// The seed values are managed in StackMeetDbContext so role IDs remain stable across environments.
/// </remarks>
public sealed class AppRole
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<CompetitionUser> CompetitionUsers { get; set; } = [];
}
