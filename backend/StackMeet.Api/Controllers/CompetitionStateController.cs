using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using StackMeet.Api.Data;
using StackMeet.Api.Services;
using StackMeet.Api.Hubs;
using StackMeet.Api.Models;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/state")]
public sealed class CompetitionStateController(
    StackMeetDbContext database,
    IHubContext<ResultsHub> resultsHub,
    CompetitionPermissionService permissions,
    ILogger<CompetitionStateController> logger) : ControllerBase
{
    [HttpGet("{competitionKey}")]
    public async Task<IActionResult> Get(string competitionKey, CancellationToken cancellationToken)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        if (!CompetitionKeyRules.IsValid(normalizedKey))
        {
            return BadRequest(new { error = "Competition key must be 3-50 characters: A-Z, 0-9, underscore or hyphen." });
        }

        var access = await Access(normalizedKey, false, cancellationToken);
        if (access is not null) return access;

        var state = await database.CompetitionStates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, cancellationToken);
        if (state is null) return NotFound();

        SetEtag(state.StateRevision);
        return Content(state.JsonData, "application/json");
    }

    [HttpPost("{competitionKey}")]
    public async Task<IActionResult> Save(string competitionKey, CancellationToken cancellationToken)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        if (!CompetitionKeyRules.IsValid(normalizedKey))
        {
            return BadRequest(new { error = "Competition key must be 3-50 characters: A-Z, 0-9, underscore or hyphen." });
        }

        var access = await Access(normalizedKey, true, cancellationToken);
        if (access is not null) return access;

        if (!TryExpectedRevision(Request.Headers["If-Match"].FirstOrDefault(), out var expectedRevision))
        {
            return StatusCode(StatusCodes.Status428PreconditionRequired, new
            {
                error = "Competition state revision is required. Refresh the latest data before saving."
            });
        }

        using var reader = new StreamReader(Request.Body);
        var jsonData = await reader.ReadToEndAsync(cancellationToken);
        var validationError = ValidateStateJson(jsonData);
        if (validationError is not null)
        {
            return BadRequest(new { error = validationError });
        }

        var referencedCodes = ExtractReferencedCodes(jsonData, out validationError);
        if (validationError is not null) return BadRequest(new { error = validationError });
        var competitionId = await database.Competitions.Where(item => item.CompetitionKey == normalizedKey).Select(item => (int?)item.Id).SingleOrDefaultAsync(cancellationToken);
        if (competitionId is null) return NotFound();
        foreach (var chunk in referencedCodes.Chunk(500))
        {
            var existing = await database.Stackers.AsNoTracking()
                .Where(item => item.CompetitionId == competitionId && chunk.Contains(item.StackerCode))
                .Select(item => item.StackerCode).ToListAsync(cancellationToken);
            if (existing.Count != chunk.Length)
                return BadRequest(new { error = "Competition state contains a participant reference that does not belong to this competition." });
        }

        var updatedBy = Request.Headers["X-StackMeet-Updated-By"].FirstOrDefault();
        var changedAt = DateTime.UtcNow;
        long committedRevision;

        await using (var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            var state = await database.CompetitionStates
                .FromSqlInterpolated($"SELECT * FROM [dbo].[CompetitionState] WITH (UPDLOCK, HOLDLOCK) WHERE [CompetitionKey] = {normalizedKey}")
                .SingleOrDefaultAsync(cancellationToken);

            if (state is null)
            {
                if (expectedRevision != 0)
                {
                    return StateConflict(0);
                }

                state = new CompetitionState
                {
                    CompetitionKey = normalizedKey,
                    JsonData = jsonData,
                    SchemaVersion = "0.9-online",
                    StateRevision = 1,
                    CreatedAt = changedAt,
                    UpdatedAt = changedAt,
                    UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? null : updatedBy
                };
                database.CompetitionStates.Add(state);
                committedRevision = 1;
            }
            else
            {
                if (state.StateRevision != expectedRevision)
                {
                    return StateConflict(state.StateRevision);
                }

                state.JsonData = jsonData;
                state.StateRevision++;
                state.UpdatedAt = changedAt;
                state.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? null : updatedBy;
                committedRevision = state.StateRevision;
            }

            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        SetEtag(committedRevision);
        var change = new { competitionKey = normalizedKey, revision = committedRevision, scope = "global", type = "CompetitionChanged", updatedAt = changedAt };
        try { await resultsHub.Clients.Group(ResultsHub.GroupName(normalizedKey)).SendAsync("CompetitionChanged", change, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Post-commit state notification failed for competition {CompetitionKey}.", normalizedKey); }


        return NoContent();
    }

    async Task<IActionResult?> Access(string competitionKey, bool write, CancellationToken ct)
    {
        // Preserve maintenance/recovery access and legacy competition-password sessions while
        // enforcing account RBAC. Data Entry and Viewer accounts can read legacy state but cannot
        // replace it; only Competition Manager and System Admin accounts can save configuration.
        if (HttpContext.Items["StackMeetMaintenanceApiKey"] is true) return null;
        if (HttpContext.Items["StackMeetSession"] is not SessionToken session) return Unauthorized();
        if (!session.IsAccountSession) return null;
        if (session.UserId is null) return Unauthorized();

        var role = await permissions.RoleForCompetitionKey(session.UserId.Value, session.IsSystemAdmin, competitionKey, ct);
        if (role is null
            || (write
                ? !CompetitionPermissionService.CanManageCompetition(role)
                : !CompetitionPermissionService.CanViewCompetition(role)))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return null;
    }

    IActionResult StateConflict(long currentRevision)
    {
        SetEtag(currentRevision);
        return Conflict(new
        {
            error = "Competition state changed on another computer. Refresh the latest data before saving.",
            currentRevision
        });
    }

    void SetEtag(long revision) => Response.Headers["ETag"] = Etag(revision);

    static string Etag(long revision) => $"\"{revision}\"";

    static bool TryExpectedRevision(string? ifMatch, out long revision)
    {
        revision = 0;
        if (string.IsNullOrWhiteSpace(ifMatch)) return false;
        var value = ifMatch.Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) value = value[2..].Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
        return long.TryParse(value, out revision) && revision >= 0;
    }

    static string? ValidateStateJson(string jsonData)
    {
        if (string.IsNullOrWhiteSpace(jsonData))
        {
            return "Competition state must contain a JSON object.";
        }

        try
        {
            using var document = JsonDocument.Parse(jsonData);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? null
                : "Competition state root must be a JSON object.";
        }
        catch (JsonException)
        {
            return "Competition state contains malformed JSON.";
        }
    }

    static IReadOnlySet<string> ExtractReferencedCodes(string jsonData, out string? error)
    {
        error = null;
        try { return new CompetitionParticipantReferenceService().ExtractReferencedCodes(jsonData); }
        catch (JsonException) { error = "Competition state contains malformed JSON."; return new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
    }
}
