using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;
using StackMeet.Api.Hubs;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/admin/competitions")]
public sealed class CompetitionAdminController(
    StackMeetDbContext database,
    PasswordHashService passwords,
    AuditLogService auditLogs,
    IHubContext<ResultsHub> resultsHub,
    ILogger<CompetitionAdminController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CompetitionAdminSummaryResponse>>> List(CancellationToken ct)
    {
        return Ok(await Query().ToListAsync(ct));
    }

    [HttpGet("{competitionKey}")]
    public async Task<ActionResult<CompetitionAdminSummaryResponse>> Get(string competitionKey, CancellationToken ct)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        var item = await Query(normalizedKey).SingleOrDefaultAsync(ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<CompetitionAdminSummaryResponse>> Create(CompetitionAdminUpsertRequest request, CancellationToken ct)
    {
        var validation = ValidateRequest(request, requirePassword: true, out var key, out var code);
        if (validation is BadRequestObjectResult badRequest) return BadRequest(badRequest.Value);

        if (await database.Competitions.AnyAsync(item => item.CompetitionKey == key, ct))
        {
            return Conflict(new { error = "CompetitionKey already exists." });
        }

        if (await database.Competitions.AnyAsync(item => item.CompetitionCode == code, ct))
        {
            return Conflict(new { error = "CompetitionCode already exists." });
        }

        var stateExists = await database.CompetitionStates.AnyAsync(item => item.CompetitionKey == key, ct);

        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var competition = new Competition
        {
            CompetitionCode = code,
            CompetitionKey = key,
            CompetitionName = request.CompetitionName.Trim(),
            Venue = request.Venue.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = NormalizeStatus(request.Status)!,
            IsPubliclyListed = request.IsPubliclyListed,
            PasswordHash = passwords.Hash(request.Password!),
            CreatedAt = now,
            UpdatedAt = now
        };
        database.Competitions.Add(competition);
        if (!stateExists)
        {
            database.CompetitionStates.Add(new CompetitionState
            {
                CompetitionKey = key,
                JsonData = EmptyCompetitionStateFactory.Create(key, competition.CompetitionName, competition.StartDate, competition.EndDate),
                SchemaVersion = "0.9-online",
                CreatedAt = now,
                UpdatedAt = now,
                UpdatedBy = "admin:create"
            });
        }
        await database.SaveChangesAsync(ct);
        await auditLogs.Write(
            "admin.competition.created",
            "Competition",
            competition.CompetitionKey,
            ActorUserId(),
            competition.Id,
            null,
            CompetitionSnapshot(competition),
            ct);
        await transaction.CommitAsync(ct);

        var created = await Query(key).SingleAsync(ct);
        return CreatedAtAction(nameof(Get), new { competitionKey = key }, created);
    }

    [HttpPut("{competitionKey}")]
    public async Task<IActionResult> Update(string competitionKey, CompetitionAdminUpsertRequest request, CancellationToken ct)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        var item = await database.Competitions.SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, ct);
        if (item is null) return NotFound();
        if (!BasicFieldsAreValid(request)) return BadRequest(new { error = "Competition name, venue and valid dates are required." });
        var status = NormalizeStatus(request.Status);
        if (status is null) return BadRequest(new { error = "Status must be Draft, Active, Closed or Archived." });

        var before = CompetitionSnapshot(item);
        item.CompetitionName = request.CompetitionName.Trim();
        item.Venue = request.Venue.Trim();
        item.StartDate = request.StartDate;
        item.EndDate = request.EndDate;
        item.Status = status;
        item.IsPubliclyListed = request.IsPubliclyListed;
        item.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
        await auditLogs.Write(
            "admin.competition.updated",
            "Competition",
            item.CompetitionKey,
            ActorUserId(),
            item.Id,
            before,
            CompetitionSnapshot(item),
            ct);
        return NoContent();
    }

    [HttpPost("{competitionKey}/password")]
    public async Task<IActionResult> SetPassword(string competitionKey, CompetitionAdminPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8) return BadRequest(new { error = "Password must be at least 8 characters." });
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        var item = await database.Competitions.SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, ct);
        if (item is null) return NotFound();
        item.PasswordHash = passwords.Hash(request.Password);
        item.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
        await auditLogs.Write(
            "admin.competition.password_set",
            "Competition",
            item.CompetitionKey,
            ActorUserId(),
            item.Id,
            null,
            new { item.CompetitionKey },
            ct);
        return NoContent();
    }

    [HttpPost("{competitionKey}/status")]
    public Task<IActionResult> SetStatus(string competitionKey, CompetitionAdminStatusRequest request, CancellationToken ct) => UpdateStatus(competitionKey, request.Status, null, ct);

    [HttpPost("{competitionKey}/archive")]
    public async Task<IActionResult> Archive(string competitionKey, CompetitionAdminArchiveRequest request, CancellationToken ct)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        var item = await database.Competitions.SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, ct);
        if (item is null) return NotFound();
        var before = CompetitionSnapshot(item);
        item.Status = "Archived";
        item.ArchivedAt = DateTime.UtcNow;
        item.ArchivedBy = string.IsNullOrWhiteSpace(request.ArchivedBy) ? "admin" : request.ArchivedBy.Trim();
        item.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
        await auditLogs.Write(
            "admin.competition.archived",
            "Competition",
            item.CompetitionKey,
            ActorUserId(),
            item.Id,
            before,
            CompetitionSnapshot(item),
            ct);
        return NoContent();
    }

    [HttpGet("{competitionKey}/state/export")]
    public async Task<ActionResult<CompetitionJsonExportResponse>> ExportState(string competitionKey, CancellationToken ct)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        var state = await database.CompetitionStates.AsNoTracking().SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, ct);
        if (state is not null)
        {
            SetEtag(state.StateRevision);
            var competitionId = await CompetitionIdForKey(normalizedKey, ct);
            await auditLogs.Write(
                "admin.competition.state_exported",
                "CompetitionState",
                normalizedKey,
                ActorUserId(),
                competitionId,
                null,
                new { competitionKey = normalizedKey },
                ct);
        }
        return state is null ? NotFound() : Ok(new CompetitionJsonExportResponse(normalizedKey, DateTime.UtcNow, state.JsonData));
    }

    [HttpPost("{competitionKey}/state/import")]
    public async Task<IActionResult> ImportState(string competitionKey, CancellationToken ct)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        if (!CompetitionKeyRules.IsValid(normalizedKey))
        {
            return BadRequest(new { error = "Competition key must be 3-50 characters: A-Z, 0-9, underscore or hyphen." });
        }

        if (!TryExpectedRevision(Request.Headers["If-Match"].FirstOrDefault(), out var expectedRevision))
        {
            return StatusCode(StatusCodes.Status428PreconditionRequired, new
            {
                error = "Competition state revision is required. Refresh the latest data before importing."
            });
        }

        using var reader = new StreamReader(Request.Body);
        var jsonData = await reader.ReadToEndAsync(ct);
        var validationError = ValidateStateJson(jsonData);
        if (validationError is not null)
        {
            return BadRequest(new { error = validationError });
        }

        var competitionId = await CompetitionIdForKey(normalizedKey, ct);
        if (competitionId is null) return NotFound();

        var changedAt = DateTime.UtcNow;
        long committedRevision;
        await using (var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var state = await database.CompetitionStates
                .FromSqlInterpolated($"SELECT * FROM [dbo].[CompetitionState] WITH (UPDLOCK, HOLDLOCK) WHERE [CompetitionKey] = {normalizedKey}")
                .SingleOrDefaultAsync(ct);
            if (state is null) return NotFound();
            if (state.StateRevision != expectedRevision) return StateConflict(state.StateRevision);

            var before = new { state.CompetitionKey, state.StateRevision, state.UpdatedAt, state.UpdatedBy };
            state.JsonData = jsonData;
            state.StateRevision++;
            state.UpdatedAt = changedAt;
            state.UpdatedBy = "admin:xml-import";
            committedRevision = state.StateRevision;
            await database.SaveChangesAsync(ct);
            await auditLogs.Write(
                "admin.competition.state_imported",
                "CompetitionState",
                normalizedKey,
                ActorUserId(),
                competitionId,
                before,
                new { state.CompetitionKey, state.StateRevision, state.UpdatedAt, state.UpdatedBy },
                ct);
            await transaction.CommitAsync(ct);
        }

        SetEtag(committedRevision);
        var change = new { competitionKey = normalizedKey, revision = committedRevision, scope = "global", type = "CompetitionChanged", updatedAt = changedAt };
        try { await resultsHub.Clients.Group(ResultsHub.GroupName(normalizedKey)).SendAsync("CompetitionChanged", change, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Post-commit admin notification failed for competition {CompetitionKey}.", normalizedKey); }
        return NoContent();
    }

    [HttpPost("{competitionKey}/state/initialize")]
    public async Task<IActionResult> InitializeState(string competitionKey, CancellationToken ct)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        var competition = await database.Competitions.SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, ct);
        if (competition is null) return NotFound();
        if (await database.CompetitionStates.AnyAsync(item => item.CompetitionKey == normalizedKey, ct)) return Conflict(new { error = "CompetitionState already exists." });
        var now = DateTime.UtcNow;
        database.CompetitionStates.Add(new CompetitionState
        {
            CompetitionKey = normalizedKey,
            JsonData = EmptyCompetitionStateFactory.Create(normalizedKey, competition.CompetitionName, competition.StartDate, competition.EndDate),
            SchemaVersion = "0.9-online",
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedBy = "admin:initialize"
        });
        await database.SaveChangesAsync(ct);
        await auditLogs.Write(
            "admin.competition.state_initialized",
            "CompetitionState",
            normalizedKey,
            ActorUserId(),
            competition.Id,
            null,
            new { competitionKey = normalizedKey },
            ct);
        return NoContent();
    }

    [HttpPost("{competitionKey}/delete")]
    public async Task<IActionResult> Delete(string competitionKey, CompetitionAdminDeleteRequest request, CancellationToken ct)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        if (request.Confirmation != $"DELETE {normalizedKey}") return BadRequest(new { error = $"Confirmation must be DELETE {normalizedKey}." });
        if (normalizedKey == "DEFAULT") return BadRequest(new { error = "DEFAULT competition deletion is blocked." });

        var competition = await database.Competitions.SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, ct);
        if (competition is null) return NotFound();
        if (await database.Stackers.AnyAsync(item => item.CompetitionId == competition.Id, ct))
        {
            return Conflict(new { error = "Competition cannot be deleted while it has stackers." });
        }

        var state = await database.CompetitionStates.SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, ct);
        if (state is not null && StateContainsCompetitionData(state.JsonData))
        {
            return Conflict(new { error = "Competition cannot be deleted while its state contains participants, teams or results." });
        }

        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        if (state is not null) database.CompetitionStates.Remove(state);
        database.Competitions.Remove(competition);
        await database.SaveChangesAsync(ct);
        await auditLogs.Write(
            "admin.competition.deleted",
            "Competition",
            normalizedKey,
            ActorUserId(),
            null,
            CompetitionSnapshot(competition),
            null,
            ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpPost("{competitionKey}/state/reset")]
    public async Task<IActionResult> ResetState(string competitionKey, CompetitionAdminResetStateRequest request, CancellationToken ct)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        if (request.Confirmation != $"RESET {normalizedKey}") return BadRequest(new { error = $"Confirmation must be RESET {normalizedKey}." });
        if (normalizedKey == "DEFAULT") return BadRequest(new { error = "DEFAULT state reset is blocked in Phase 1." });

        var competition = await database.Competitions.AsNoTracking().SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, ct);
        if (competition is null) return NotFound();

        var changedAt = DateTime.UtcNow;
        long committedRevision;
        await using (var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var state = await database.CompetitionStates
                .FromSqlInterpolated($"SELECT * FROM [dbo].[CompetitionState] WITH (UPDLOCK, HOLDLOCK) WHERE [CompetitionKey] = {normalizedKey}")
                .SingleOrDefaultAsync(ct);
            if (state is null) return NotFound();

            var before = new { state.CompetitionKey, state.SchemaVersion, state.StateRevision, state.UpdatedAt, state.UpdatedBy, request.ResultsOnly };
            state.JsonData = request.ResultsOnly
                ? CompetitionStateResetService.ResetResultsOnly(state.JsonData)
                : EmptyCompetitionStateFactory.Create(normalizedKey, competition.CompetitionName, competition.StartDate, competition.EndDate);
            state.StateRevision++;
            state.UpdatedAt = changedAt;
            state.UpdatedBy = request.ResultsOnly ? "admin:reset-results" : "admin:reset-state";
            committedRevision = state.StateRevision;
            await database.SaveChangesAsync(ct);
            await auditLogs.Write(
                request.ResultsOnly ? "admin.competition.results_reset" : "admin.competition.state_reset",
                "CompetitionState",
                normalizedKey,
                ActorUserId(),
                competition.Id,
                before,
                new { state.CompetitionKey, state.SchemaVersion, state.StateRevision, state.UpdatedAt, state.UpdatedBy, request.ResultsOnly },
                ct);
            await transaction.CommitAsync(ct);
        }

        SetEtag(committedRevision);
        var change = new { competitionKey = normalizedKey, revision = committedRevision, scope = "global", type = "CompetitionChanged", updatedAt = changedAt };
        try { await resultsHub.Clients.Group(ResultsHub.GroupName(normalizedKey)).SendAsync("CompetitionChanged", change, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Post-commit admin notification failed for competition {CompetitionKey}.", normalizedKey); }
        return NoContent();
    }

    async Task<IActionResult> UpdateStatus(string competitionKey, string status, DateTime? archivedAt, CancellationToken ct)
    {
        var normalizedStatus = NormalizeStatus(status);
        if (normalizedStatus is null) return BadRequest(new { error = "Status must be Draft, Active, Closed or Archived." });

        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        var item = await database.Competitions.SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, ct);
        if (item is null) return NotFound();
        var before = CompetitionSnapshot(item);
        item.Status = normalizedStatus;
        item.ArchivedAt = archivedAt;
        item.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
        await auditLogs.Write(
            "admin.competition.status_changed",
            "Competition",
            item.CompetitionKey,
            ActorUserId(),
            item.Id,
            before,
            CompetitionSnapshot(item),
            ct);
        return NoContent();
    }

    IActionResult StateConflict(long currentRevision)
    {
        SetEtag(currentRevision);
        return Conflict(new
        {
            error = "Competition state changed on another computer. Refresh the latest data before importing.",
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

    /// <summary>
    /// Reads the optional account actor from the current admin request.
    /// </summary>
    /// <remarks>
    /// Admin-key-only operations are still audited, but their actor id remains null.
    /// </remarks>
    int? ActorUserId() => auditLogs.CurrentSession()?.UserId;

    /// <summary>
    /// Captures competition admin fields without password hash material.
    /// </summary>
    /// <remarks>
    /// Password changes are logged as events only; the hash and raw password are never included.
    /// </remarks>
    static object CompetitionSnapshot(Competition item) => new
    {
        item.Id,
        item.CompetitionCode,
        item.CompetitionKey,
        item.CompetitionName,
        item.Venue,
        item.StartDate,
        item.EndDate,
            item.Status,
            item.IsPubliclyListed,
        item.ArchivedAt,
        item.ArchivedBy,
        item.CreatedAt,
        item.UpdatedAt
    };

    /// <summary>
    /// Resolves a competition id for state-only admin actions.
    /// </summary>
    /// <remarks>
    /// Some state rows can exist independently, so this returns null when no competition row exists.
    /// </remarks>
    async Task<int?> CompetitionIdForKey(string competitionKey, CancellationToken ct) =>
        await database.Competitions
            .AsNoTracking()
            .Where(item => item.CompetitionKey == competitionKey)
            .Select(item => (int?)item.Id)
            .SingleOrDefaultAsync(ct);

    IQueryable<CompetitionAdminSummaryResponse> Query(string? competitionKey = null)
    {
        var competitions = database.Competitions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(competitionKey))
        {
            competitions = competitions.Where(item => item.CompetitionKey == competitionKey);
        }

        return
        from competition in competitions
        join state in database.CompetitionStates.AsNoTracking()
            on competition.CompetitionKey equals state.CompetitionKey into states
        from state in states.DefaultIfEmpty()
        orderby competition.CompetitionKey
        select new CompetitionAdminSummaryResponse(
            competition.Id,
            competition.CompetitionCode,
            competition.CompetitionKey,
            competition.CompetitionName,
            competition.Venue,
            competition.StartDate,
            competition.EndDate,
            competition.Status,
            competition.IsPubliclyListed,
            competition.PasswordHash != null,
            state != null,
            state == null ? null : state.CreatedAt,
            state == null ? null : state.UpdatedAt,
            competition.ArchivedAt,
            competition.ArchivedBy,
            competition.CreatedAt,
            competition.UpdatedAt);
    }

    static IActionResult? ValidateRequest(CompetitionAdminUpsertRequest request, bool requirePassword, out string key, out string code)
    {
        key = CompetitionKeyRules.Normalize(string.IsNullOrWhiteSpace(request.CompetitionKey) ? request.CompetitionCode ?? "" : request.CompetitionKey);
        code = string.IsNullOrWhiteSpace(request.CompetitionCode) ? key : request.CompetitionCode.Trim().ToUpperInvariant();
        if (!CompetitionKeyRules.IsValid(key)) return new BadRequestObjectResult(new { error = "CompetitionKey must be 3-50 characters: A-Z, 0-9, underscore or hyphen." });
        if (!CompetitionKeyRules.IsValid(code)) return new BadRequestObjectResult(new { error = "CompetitionCode must be 3-50 characters: A-Z, 0-9, underscore or hyphen." });
        if (!BasicFieldsAreValid(request)) return new BadRequestObjectResult(new { error = "Competition name, venue and valid dates are required." });
        if (NormalizeStatus(request.Status) is null) return new BadRequestObjectResult(new { error = "Status must be Draft, Active, Closed or Archived." });
        if (requirePassword && (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)) return new BadRequestObjectResult(new { error = "Password must be at least 8 characters." });
        return null;
    }

    static bool BasicFieldsAreValid(CompetitionAdminUpsertRequest request) =>
        !string.IsNullOrWhiteSpace(request.CompetitionName)
        && !string.IsNullOrWhiteSpace(request.Venue)
        && request.EndDate >= request.StartDate;

    static bool StateContainsCompetitionData(string jsonData)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonData);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return true;
            foreach (var propertyName in new[] { "stackers", "doubles", "relays", "results", "finalQualificationSnapshots" })
            {
                if (document.RootElement.TryGetProperty(propertyName, out var value)
                    && value.ValueKind == JsonValueKind.Array
                    && value.GetArrayLength() > 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    static string? NormalizeStatus(string? status) => status?.Trim().ToUpperInvariant() switch
    {
        "ACTIVE" => "Active",
        "CLOSED" => "Closed",
        "ARCHIVED" => "Archived",
        "DRAFT" => "Draft",
        _ => null
    };
}
