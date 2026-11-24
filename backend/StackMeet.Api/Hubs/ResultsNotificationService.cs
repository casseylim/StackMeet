using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Services;

namespace StackMeet.Api.Hubs;

/// <summary>Reauthorizes subscribers against committed data before sending change metadata.</summary>
public sealed class ResultsNotificationService(
    StackMeetDbContext database, ResultsSubscriptionRegistry subscriptions, IHubContext<ResultsHub> hub)
{
    public async Task SendAsync(string competitionKey, string eventName, object change, CancellationToken ct)
    {
        var candidates = subscriptions.ForCompetition(competitionKey);
        if (candidates.Length == 0) return;

        // If any database query fails, propagate to the caller's post-commit logging guard.
        // Do not send to an unchecked group or use cached authorization as a fallback.
        var competition = await database.Competitions.AsNoTracking()
            .Where(item => item.CompetitionKey == competitionKey)
            .Select(item => new { item.Id, item.IsPubliclyListed, item.ArchivedAt, item.Status })
            .SingleOrDefaultAsync(ct);
        if (competition is null || competition.ArchivedAt is not null
            || string.Equals(competition.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var candidate in candidates) subscriptions.RemoveIfCurrent(candidate);
            return;
        }

        var accounts = new Dictionary<int, (long Version, bool Admin)>();
        var readers = new HashSet<int>();
        // Bounded chunks keep query parameters manageable even with many connected users.
        var userIds = candidates.Where(item => item.UserId.HasValue)
            .Select(item => item.UserId!.Value).Distinct().ToArray();
        foreach (var chunk in userIds.Chunk(500))
        {
            var active = await database.AppUsers.AsNoTracking()
                .Where(item => chunk.Contains(item.Id) && item.IsActive)
                .Select(item => new { item.Id, item.SessionVersion, item.IsSystemAdmin })
                .ToListAsync(ct);
            foreach (var user in active) accounts[user.Id] = (user.SessionVersion, user.IsSystemAdmin);

            var assignments = await database.CompetitionUsers.AsNoTracking()
                .Where(item => chunk.Contains(item.UserId) && item.CompetitionId == competition.Id && item.IsActive)
                .Select(item => new { item.UserId, Role = item.Role.Name }).ToListAsync(ct);
            foreach (var assignment in assignments)
                if (CompetitionPermissionService.CanViewCompetition(assignment.Role)) readers.Add(assignment.UserId);
        }

        var now = DateTimeOffset.UtcNow;
        var authorized = new List<ResultsSubscription>();
        foreach (var candidate in candidates)
        {
            var allowed = candidate.IsPublic
                ? competition.IsPubliclyListed
                : candidate.ExpiresAt > now &&
                  (candidate.UserId is not int userId ||
                   (accounts.TryGetValue(userId, out var account)
                    && account.Version == candidate.SessionVersion
                    && account.Admin == candidate.IsSystemAdmin
                    && (account.Admin || readers.Contains(userId))));
            if (allowed) authorized.Add(candidate);
            else subscriptions.RemoveIfCurrent(candidate);
        }
        var recipients = authorized.Where(subscriptions.IsCurrent).Select(item => item.ConnectionId).ToArray();
        if (recipients.Length > 0)
            await hub.Clients.Clients(recipients).SendAsync(eventName, change, ct);
    }
}
