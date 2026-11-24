using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Services;

namespace StackMeet.Api.Hubs;

public sealed class ResultsHub(
    StackMeetDbContext database,
    SessionTokenService tokens,
    CompetitionPermissionService permissions,
    ResultsSubscriptionRegistry subscriptions) : Hub
{
    // Anonymous push subscriptions are restricted to the public directory.
    // Direct-link public HTTP results retain their existing publication contract.
    public async Task JoinCompetition(string? competitionId)
    {
        subscriptions.Remove(Context.ConnectionId);
        var normalized = NormalizeCompetition(competitionId);
        var matches = await database.Competitions.AsNoTracking()
            .Where(item => item.CompetitionKey == normalized || item.CompetitionCode == normalized)
            .Select(item => new { item.CompetitionKey, item.IsPubliclyListed, item.ArchivedAt, item.Status })
            .Take(2).ToListAsync(Context.ConnectionAborted);
        if (matches.Count != 1 || !matches[0].IsPubliclyListed
            || matches[0].ArchivedAt is not null
            || string.Equals(matches[0].Status, "Archived", StringComparison.OrdinalIgnoreCase))
            throw new HubException("Competition subscription unavailable.");

        Subscribe(matches[0].CompetitionKey, null);
    }

    // The operator passes its bearer in the hub invocation, not in the URL.
    // Do not trust a claimed user identity or a client-supplied competition role.
    public async Task JoinOperatorCompetition(string? competitionId, string? token)
    {
        subscriptions.Remove(Context.ConnectionId);
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

        if ((session.IsAccountSession && !await SessionAuthorization.CurrentAccountQuery(database, session).AnyAsync(ct))
            || !await permissions.CanAccessCompetitionId(session, competition.Id, ct))
            throw new HubException("Competition subscription unavailable.");

        Subscribe(competition.CompetitionKey, session);
    }

    void Subscribe(string competitionKey, SessionToken? session)
    {
        var subscription = new ResultsSubscription(Context.ConnectionId, competitionKey,
            session?.ExpiresAt, session?.UserId, session?.SessionVersion, session?.IsSystemAdmin ?? false);
        Context.ConnectionAborted.ThrowIfCancellationRequested();
        subscriptions.Set(subscription);
        // Disconnect may race the final registration.
        if (Context.ConnectionAborted.IsCancellationRequested)
            subscriptions.RemoveIfCurrent(subscription);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        subscriptions.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    static string NormalizeCompetition(string? competitionId)
    {
        var normalized = CompetitionKeyRules.Normalize(competitionId ?? "");
        if (!CompetitionKeyRules.IsValid(normalized))
            throw new HubException("Competition subscription unavailable.");
        return normalized;
    }

}
