using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

namespace StackMeet.Api.Services;

/// <summary>
/// Writes security and administration events to the MSSQL audit table.
/// </summary>
/// <remarks>
/// Competition gameplay/result auditing should use the competition JSON state, while this service
/// focuses on account, access, email setup and competition administration events.
/// </remarks>
public sealed class AuditLogService(
    StackMeetDbContext database,
    IHttpContextAccessor httpContextAccessor,
    SessionTokenService tokens)
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Records one admin/security event with optional before and after details.
    /// </summary>
    /// <remarks>
    /// Details are JSON snapshots and must not include secrets such as passwords or SMTP keys.
    /// </remarks>
    public async Task Write(
        string action,
        string entityType,
        string? entityId = null,
        int? userId = null,
        int? competitionId = null,
        object? oldValue = null,
        object? newValue = null,
        CancellationToken ct = default)
    {
        database.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            CompetitionId = competitionId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValueJson = Serialize(oldValue),
            NewValueJson = Serialize(newValue),
            IpAddress = ClientIpAddress(),
            CreatedAt = DateTime.UtcNow
        });

        await database.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Reads the current account session, if one was supplied with the request.
    /// </summary>
    /// <remarks>
    /// Admin-key requests may not include a bearer token, so actor identity is optional.
    /// </remarks>
    public SessionToken? CurrentSession()
    {
        var context = httpContextAccessor.HttpContext;
        if (context?.Items.TryGetValue("StackMeetSession", out var value) == true && value is SessionToken session)
        {
            return session;
        }

        if (tokens.TryValidate(BearerToken(context?.Request.Headers.Authorization.FirstOrDefault()), out var bearerSession))
        {
            return bearerSession;
        }

        return null;
    }

    /// <summary>
    /// Creates a read-only query for recent audit rows.
    /// </summary>
    /// <remarks>
    /// Controllers add filtering and limits so the admin UI does not request unlimited history.
    /// </remarks>
    public IQueryable<AuditLog> Query() =>
        database.AuditLogs
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.Competition);

    static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    string? ClientIpAddress() =>
        httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    static string? BearerToken(string? authorization)
    {
        const string prefix = "Bearer ";
        return authorization?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true
            ? authorization[prefix.Length..].Trim()
            : null;
    }
}
