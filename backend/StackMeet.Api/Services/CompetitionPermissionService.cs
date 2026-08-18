using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;

namespace StackMeet.Api.Services;

/// <summary>
/// Centralizes Phase 1 competition-specific permission checks for account-based sessions.
/// </summary>
/// <remarks>
/// Controllers should ask this service for capabilities instead of checking role names inline.
/// This keeps the migration from competition-password access to per-competition user access manageable.
/// </remarks>
public sealed class CompetitionPermissionService(StackMeetDbContext database)
{
    /// <summary>
    /// Returns the assigned role name for a user in a competition key, or SystemAdmin for global admins.
    /// </summary>
    /// <remarks>
    /// Use this when the API route carries the public competition key, such as /api/state/{competitionKey}.
    /// </remarks>
    public async Task<string?> RoleForCompetitionKey(int userId, bool isSystemAdmin, string competitionKey, CancellationToken ct)
    {
        if (isSystemAdmin) return StackMeetRoles.SystemAdmin;

        return await database.CompetitionUsers
            .AsNoTracking()
            .Where(item =>
                item.IsActive
                && item.UserId == userId
                && item.User.IsActive
                && item.Competition.CompetitionKey == competitionKey)
            .Select(item => item.Role.Name)
            .SingleOrDefaultAsync(ct);
    }

    /// <summary>
    /// Returns the assigned role name for a user in a competition ID, or SystemAdmin for global admins.
    /// </summary>
    /// <remarks>
    /// Use this when SQL-native controllers route by Competition.Id.
    /// </remarks>
    public async Task<string?> RoleForCompetitionId(int userId, bool isSystemAdmin, int competitionId, CancellationToken ct)
    {
        if (isSystemAdmin) return StackMeetRoles.SystemAdmin;

        return await database.CompetitionUsers
            .AsNoTracking()
            .Where(item =>
                item.IsActive
                && item.UserId == userId
                && item.User.IsActive
                && item.CompetitionId == competitionId)
            .Select(item => item.Role.Name)
            .SingleOrDefaultAsync(ct);
    }

    /// <summary>
    /// Checks whether a role can create or administer competitions and user access.
    /// </summary>
    /// <remarks>
    /// Phase 1 keeps this strict: only global system admins can perform system-wide administration.
    /// </remarks>
    public static bool CanAdministerSystem(string? role) => role == StackMeetRoles.SystemAdmin;

    /// <summary>
    /// Checks whether a role can modify competition configuration and participant/team setup.
    /// </summary>
    /// <remarks>
    /// Competition managers can operate their assigned competitions, while data entry remains result-focused.
    /// </remarks>
    public static bool CanManageCompetition(string? role) =>
        role is StackMeetRoles.SystemAdmin or StackMeetRoles.CompetitionManager;

    public static bool CanManageCertificates(string? role) =>
        role is StackMeetRoles.SystemAdmin or StackMeetRoles.CompetitionManager;

    /// <summary>
    /// Checks whether a role can enter preliminary or finals results.
    /// </summary>
    /// <remarks>
    /// Data Entry users are allowed here, but later phases can add event locks and correction approval.
    /// </remarks>
    public static bool CanEnterResults(string? role) =>
        role is StackMeetRoles.SystemAdmin or StackMeetRoles.CompetitionManager or StackMeetRoles.DataEntry;

    /// <summary>
    /// Checks whether a role has any internal read access to a competition.
    /// </summary>
    /// <remarks>
    /// Viewer is included for later read-only internal views.
    /// </remarks>
    public static bool CanViewCompetition(string? role) =>
        role is StackMeetRoles.SystemAdmin or StackMeetRoles.CompetitionManager or StackMeetRoles.DataEntry or StackMeetRoles.Viewer;
}
