using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

namespace StackMeet.Api.Services;

/// <summary>Shared account-currentness and competition read-scope rules.</summary>
/// <remarks>Only call with a cryptographically validated session or the established HTTP authority.</remarks>
public static class SessionAuthorization
{
    public static IQueryable<AppUser> CurrentAccountQuery(StackMeetDbContext database, SessionToken session)
    {
        var users = database.AppUsers.AsNoTracking();
        if (!session.IsAccountSession || session.UserId is null || session.SessionVersion is null)
            return users.Where(_ => false);

        return users.Where(item =>
            item.Id == session.UserId.Value
            && item.IsActive
            && item.SessionVersion == session.SessionVersion.Value
            && item.IsSystemAdmin == session.IsSystemAdmin);
    }

    public static IQueryable<Competition> ScopeCompetitions(IQueryable<Competition> query, HttpContext context)
    {
        // Maintenance authority is established only by the API key gate, never a request header alone.
        if (context.Items["StackMeetMaintenanceApiKey"] is true) return query;
        if (context.Items["StackMeetSession"] is not SessionToken session) return query.Where(_ => false);
        if (!session.IsAccountSession)
            return query.Where(item => item.CompetitionKey == session.CompetitionId);
        if (session.IsSystemAdmin) return query;

        return query.Where(item => item.CompetitionUsers.Any(access =>
            access.IsActive && access.UserId == session.UserId && access.User.IsActive
            && StackMeetRoles.All.Contains(access.Role.Name)));
    }
}
