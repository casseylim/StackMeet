namespace StackMeet.Api.Services;

/// <summary>
/// Contains the canonical role names used by account access and permission checks.
/// </summary>
/// <remarks>
/// Keep these values aligned with AppRole seed data and any frontend role selector.
/// </remarks>
public static class StackMeetRoles
{
    public const string SystemAdmin = "SystemAdmin";
    public const string CompetitionManager = "CompetitionManager";
    public const string DataEntry = "DataEntry";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [SystemAdmin, CompetitionManager, DataEntry, Viewer];
}
