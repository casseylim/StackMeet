namespace StackMeet.Api.Dtos;

/// <summary>
/// Read-only audit entry returned to the admin audit log page.
/// </summary>
/// <remarks>
/// JSON details are returned as compact strings so the browser can show them without knowing each action schema.
/// </remarks>
public sealed record AdminAuditLogResponse(
    long Id,
    DateTime CreatedAt,
    string Action,
    string EntityType,
    string? EntityId,
    int? UserId,
    string? UserEmail,
    int? CompetitionId,
    string? CompetitionKey,
    string? OldValueJson,
    string? NewValueJson,
    string? IpAddress);
