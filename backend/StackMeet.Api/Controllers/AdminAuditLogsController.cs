using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Dtos;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

/// <summary>
/// Provides read-only access to MSSQL admin audit logs.
/// </summary>
/// <remarks>
/// The /api/admin middleware protects this endpoint with the configured admin key.
/// </remarks>
[ApiController]
[Route("api/admin/audit-logs")]
public sealed class AdminAuditLogsController(AuditLogService auditLogs) : ControllerBase
{
    /// <summary>
    /// Lists recent audit entries with simple optional filters.
    /// </summary>
    /// <remarks>
    /// Limit is capped to keep the admin page responsive even after long production use.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminAuditLogResponse>>> List(
        string? action,
        string? entityType,
        int? userId,
        int? competitionId,
        int? limit,
        CancellationToken ct)
    {
        var cappedLimit = Math.Clamp(limit ?? 100, 1, 500);
        var query = auditLogs.Query();

        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(item => item.Action.Contains(action.Trim()));
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(item => item.EntityType == entityType.Trim());
        if (userId is not null) query = query.Where(item => item.UserId == userId);
        if (competitionId is not null) query = query.Where(item => item.CompetitionId == competitionId);

        return Ok(await query
            .OrderByDescending(item => item.CreatedAt)
            .Take(cappedLimit)
            .Select(item => Map(item))
            .ToListAsync(ct));
    }

    /// <summary>
    /// Maps the EF entity to the admin-facing read model.
    /// </summary>
    /// <remarks>
    /// Navigation properties are optional because some audit rows may intentionally have no actor.
    /// </remarks>
    static AdminAuditLogResponse Map(AuditLog item) => new(
        item.Id,
        item.CreatedAt,
        item.Action,
        item.EntityType,
        item.EntityId,
        item.UserId,
        item.User == null ? null : item.User.Email,
        item.CompetitionId,
        item.Competition == null ? null : item.Competition.CompetitionKey,
        item.OldValueJson,
        item.NewValueJson,
        item.IpAddress);
}
