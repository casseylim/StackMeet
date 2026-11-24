using System.Collections.Concurrent;

namespace StackMeet.Api.Hubs;

// Keep only the authorization facts required for a later send. Never retain the bearer.
public sealed class ResultsSubscription(
    string connectionId, string competitionKey, DateTimeOffset? expiresAt,
    int? userId, long? sessionVersion, bool isSystemAdmin)
{
    public string ConnectionId { get; } = connectionId;
    public string CompetitionKey { get; } = competitionKey;
    public DateTimeOffset? ExpiresAt { get; } = expiresAt;
    public int? UserId { get; } = userId;
    public long? SessionVersion { get; } = sessionVersion;
    public bool IsSystemAdmin { get; } = isSystemAdmin;
    public bool IsPublic => ExpiresAt is null;
}

public sealed class ResultsSubscriptionRegistry
{
    readonly ConcurrentDictionary<string, ResultsSubscription> entries = new(StringComparer.Ordinal);

    public void Set(ResultsSubscription subscription) => entries[subscription.ConnectionId] = subscription;
    public void Remove(string connectionId) => entries.TryRemove(connectionId, out _);

    public bool IsCurrent(ResultsSubscription subscription) =>
        entries.TryGetValue(subscription.ConnectionId, out var current) && ReferenceEquals(current, subscription);

    public void RemoveIfCurrent(ResultsSubscription subscription) =>
        ((ICollection<KeyValuePair<string, ResultsSubscription>>)entries)
            .Remove(new(subscription.ConnectionId, subscription));

    public ResultsSubscription[] ForCompetition(string competitionKey) => entries.Values
        .Where(item => string.Equals(item.CompetitionKey, competitionKey, StringComparison.OrdinalIgnoreCase)).ToArray();
}
