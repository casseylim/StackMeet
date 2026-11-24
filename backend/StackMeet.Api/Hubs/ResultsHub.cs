using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Services;

namespace StackMeet.Api.Hubs;

public sealed class ResultsHub(
    StackMeetDbContext database,
    SessionTokenService tokens,
    CompetitionPermissionService permissions) : Hub
{
    const string SubscriptionKey = "ResultsSubscription";

    // Anonymous push subscriptions are restricted to the public directory.
    // Direct-link public HTTP results retain their existing publication contract.
    public async Task JoinCompetition(string? competitionId)
    {
        await LeaveCurrentSubscription();
        var normalized = NormalizeCompetition(competitionId);
        var matches = await database.Competitions.AsNoTracking()
            .Where(item => item.CompetitionKey == normalized || item.CompetitionCode == normalized)
            .Select(item => new { item.CompetitionKey, item.IsPubliclyListed, item.ArchivedAt, item.Status })
            .Take(2).ToListAsync(Context.ConnectionAborted);
        if (matches.Count != 1 || !matches[0].IsPubliclyListed
            || matches[0].ArchivedAt is not null
            || string.Equals(matches[0].Status, "Archived", StringComparison.OrdinalIgnoreCase))
            throw new HubException("Competition subscription unavailable.");

        await Subscribe(matches[0].CompetitionKey);
    }

    // The operator passes its bearer in the hub invocation, not in the URL.
    // Do not trust a claimed user identity or a client-supplied competition role.
    public async Task JoinOperatorCompetition(string? competitionId, string? token)
    {
        await LeaveCurrentSubscription();
        var normalized = NormalizeCompetition(competitionId);
        var ct = Context.ConnectionAborted;
        if (!tokens.TryValidate(token, out var session))
            throw new HubException("Competition subscription unavailable.");

        var competition = await database.Competitions.AsNoTracking()
            .Where(item => item.CompetitionKey == normalized)
            .Select(item => new { item.Id, item.CompetitionKey, item.ArchivedAt, item.Status })
            .SingleOrDefaultAsync(ct);
        if (competition is null || competition.ArchivedAt is not null
            || string.Equals(competition.Status, "Archived", StringComparison.OrdinalIgnoreCase))
            throw new HubException("Competition subscription unavailable.");

        if (session.IsAccountSession)
        {
            var current = await database.AppUsers.AsNoTracking().AnyAsync(item =>
                item.Id == session.UserId && item.IsActive
                && item.SessionVersion == session.SessionVersion
                && item.IsSystemAdmin == session.IsSystemAdmin, ct);
            if (!current || !CompetitionPermissionService.CanViewCompetition(
                await permissions.RoleForCompetitionId(session.UserId!.Value, session.IsSystemAdmin, competition.Id, ct)))
                throw new HubException("Competition subscription unavailable.");
        }
        else if (!string.Equals(session.CompetitionId, competition.CompetitionKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new HubException("Competition subscription unavailable.");
        }

        await Subscribe(competition.CompetitionKey);
    }

    async Task LeaveCurrentSubscription()
    {
        if (Context.Items.TryGetValue(SubscriptionKey, out var value) && value is string group)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
            Context.Items.Remove(SubscriptionKey);
        }
    }

    async Task Subscribe(string competitionKey)
    {
        var group = GroupName(competitionKey);
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        Context.Items[SubscriptionKey] = group;
    }

    static string NormalizeCompetition(string? competitionId)
    {
        var normalized = CompetitionKeyRules.Normalize(competitionId ?? "");
        if (!CompetitionKeyRules.IsValid(normalized))
            throw new HubException("Competition subscription unavailable.");
        return normalized;
    }

    public static string GroupName(string competitionId) =>
        $"results:{competitionId.Trim().ToUpperInvariant()}";
}
