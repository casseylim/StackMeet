using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/admin/competitions")]
public sealed class CompetitionAdminController(
    StackMeetDbContext database,
    PasswordHashService passwords) : ControllerBase
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

        item.CompetitionName = request.CompetitionName.Trim();
        item.Venue = request.Venue.Trim();
        item.StartDate = request.StartDate;
        item.EndDate = request.EndDate;
        item.Status = status;
        item.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
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
        item.Status = "Archived";
        item.ArchivedAt = DateTime.UtcNow;
        item.ArchivedBy = string.IsNullOrWhiteSpace(request.ArchivedBy) ? "admin" : request.ArchivedBy.Trim();
        item.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{competitionKey}/state/export")]
    public async Task<ActionResult<CompetitionJsonExportResponse>> ExportState(string competitionKey, CancellationToken ct)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        var state = await database.CompetitionStates.AsNoTracking().SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, ct);
        return state is null ? NotFound() : Ok(new CompetitionJsonExportResponse(normalizedKey, DateTime.UtcNow, state.JsonData));
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
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpPost("{competitionKey}/state/reset")]
    public async Task<IActionResult> ResetState(string competitionKey, CompetitionAdminResetStateRequest request, CancellationToken ct)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        if (request.Confirmation != $"RESET {normalizedKey}") return BadRequest(new { error = $"Confirmation must be RESET {normalizedKey}." });
        if (normalizedKey == "DEFAULT") return BadRequest(new { error = "DEFAULT state reset is blocked in Phase 1." });

        var competition = await database.Competitions.SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, ct);
        var state = await database.CompetitionStates.SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, ct);
        if (competition is null || state is null) return NotFound();

        state.JsonData = request.ResultsOnly
            ? CompetitionStateResetService.ResetResultsOnly(state.JsonData)
            : EmptyCompetitionStateFactory.Create(normalizedKey, competition.CompetitionName, competition.StartDate, competition.EndDate);
        state.UpdatedAt = DateTime.UtcNow;
        state.UpdatedBy = request.ResultsOnly ? "admin:reset-results" : "admin:reset-state";
        await database.SaveChangesAsync(ct);
        return NoContent();
    }

    async Task<IActionResult> UpdateStatus(string competitionKey, string status, DateTime? archivedAt, CancellationToken ct)
    {
        var normalizedStatus = NormalizeStatus(status);
        if (normalizedStatus is null) return BadRequest(new { error = "Status must be Draft, Active, Closed or Archived." });

        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        var item = await database.Competitions.SingleOrDefaultAsync(item => item.CompetitionKey == normalizedKey, ct);
        if (item is null) return NotFound();
        item.Status = normalizedStatus;
        item.ArchivedAt = archivedAt;
        item.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
        return NoContent();
    }

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