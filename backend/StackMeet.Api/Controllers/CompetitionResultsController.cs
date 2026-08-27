using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;
using StackMeet.Api.Models;
using StackMeet.Api.Services;
using StackMeet.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/competitions/{competitionId:int}/results")]
public sealed class CompetitionResultsController(StackMeetDbContext database, CompetitionPermissionService permissions, IHubContext<ResultsHub> resultsHub, ILogger<CompetitionResultsController> logger) : ControllerBase
{
    const int CandidateParticipantChunkSize = 300;

    [HttpGet]
    public async Task<ActionResult<CompetitionResultsResponse>> List(int competitionId, CancellationToken ct)
    {
        var access = await Access(competitionId, false, ct);
        if (access is not null) return access;
        var competition = await database.Competitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == competitionId, ct);
        if (competition is null) return NotFound();
        var rows = await database.CompetitionResults.AsNoTracking().Where(x => x.CompetitionId == competitionId).OrderBy(x => x.Id).ToListAsync(ct);
        return Ok(new CompetitionResultsResponse(competition.ResultsRevision, rows.Select(Map).ToList()));
    }

    [HttpPost("batch")]
    public async Task<ActionResult<CompetitionResultsResponse>> Batch(int competitionId, ResultBatchRequest request, CancellationToken ct)
    {
        var access = await Access(competitionId, true, ct);
        if (access is not null) return access;
        if (!CompetitionResultValidator.TryNormalize(request, out var normalizedRequest, out var validationError)) return BadRequest(new { error = validationError });
        request = normalizedRequest;

        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        var competition = await database.Competitions.FromSqlInterpolated($"SELECT * FROM [dbo].[Competition] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {competitionId}").SingleOrDefaultAsync(ct);
        if (competition is null) return NotFound();
        if (competition.Status is "Closed" or "Archived" || competition.ArchivedAt is not null) return Conflict(new { error = "Results cannot be changed for a closed or archived competition." });

        var participantError = await CompetitionResultValidator.ValidateUpsertParticipants(database, competition, request.Upserts, ct);
        if (participantError is not null) return BadRequest(new { error = participantError });

        var keys = request.Upserts.Select(Key).Concat(request.Deletes.Select(Key)).ToArray();
        if (keys.Length != keys.Distinct(StringComparer.Ordinal).Count()) return BadRequest(new { error = "Duplicate logical result in batch." });

        var participantCodes = request.Upserts
            .Select(x => x.Participant.Trim())
            .Concat(request.Deletes.Select(x => x.Participant.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingByKey = new Dictionary<string, CompetitionResult>(StringComparer.Ordinal);
        foreach (var participantChunk in participantCodes.Chunk(CandidateParticipantChunkSize))
        {
            var candidates = await database.CompetitionResults
                .Where(x => x.CompetitionId == competitionId && participantChunk.Contains(x.ParticipantCode))
                .ToListAsync(ct);
            foreach (var row in candidates)
            {
                existingByKey.Add(Key(row.Stage, row.ParticipantType, row.ParticipantCode, row.EventCode), row);
            }
        }

        var touched = new HashSet<CompetitionResult>();
        var deletedCount = 0;
        var actorUserId = (HttpContext.Items["StackMeetSession"] as SessionToken)?.UserId;

        foreach (var item in request.Upserts)
        {
            existingByKey.TryGetValue(Key(item), out var row);
            if (row is not null && item.ExpectedRevision is not null && row.Revision != item.ExpectedRevision) return Conflict(new { error = "This result was changed by another user.", result = Map(row) });
            var now = DateTime.UtcNow;
            if (row is null)
            {
                row = new CompetitionResult
                {
                    CompetitionId = competitionId,
                    Stage = item.Stage,
                    ParticipantType = item.Type,
                    ParticipantCode = item.Participant,
                    EventCode = item.Event,
                    CreatedAt = now
                };
                database.CompetitionResults.Add(row);
                existingByKey.Add(Key(item), row);
            }

            row.AttemptsJson = JsonSerializer.Serialize(item.Attempts);
            row.Penalty = item.Penalty;
            row.UpdatedAt = now;
            row.UpdatedByUserId = actorUserId;
            touched.Add(row);
        }

        foreach (var item in request.Deletes)
        {
            if (!existingByKey.TryGetValue(Key(item), out var row)) continue;
            if (item.ExpectedRevision is not null && row.Revision != item.ExpectedRevision) return Conflict(new { error = "This result was changed by another user.", result = Map(row) });
            database.CompetitionResults.Remove(row);
            deletedCount++;
        }

        if (touched.Count == 0 && deletedCount == 0)
        {
            await transaction.CommitAsync(ct);
            return Ok(new CompetitionResultsResponse(competition.ResultsRevision, []));
        }

        competition.ResultsRevision++;
        foreach (var row in touched)
        {
            row.Revision = competition.ResultsRevision;
        }

        try
        {
            await database.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            await transaction.RollbackAsync(ct);
            return Conflict(new { error = "A result with this identity already exists. Refresh the latest results and retry." });
        }

        await transaction.CommitAsync(ct);
        try { await resultsHub.Clients.Group(ResultsHub.GroupName(competition.CompetitionKey)).SendAsync("ResultsChanged", new { competitionId, competitionKey = competition.CompetitionKey, revision = competition.ResultsRevision, scope = "results", type = "ResultsChanged" }, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Post-commit results notification failed for competition {CompetitionId}.", competitionId); }
        // Batch responses are deltas; the full GET remains the authoritative resync path.
        return Ok(new CompetitionResultsResponse(competition.ResultsRevision, touched.Select(Map).ToList()));
    }

    async Task<ActionResult?> Access(int competitionId, bool write, CancellationToken ct)
    {
        if (HttpContext.Items["StackMeetSession"] is not SessionToken session || session.UserId is null) return Unauthorized();
        var role = await permissions.RoleForCompetitionId(session.UserId.Value, session.IsSystemAdmin, competitionId, ct);
        if (role is null || (write ? !CompetitionPermissionService.CanEnterResults(role) : !CompetitionPermissionService.CanViewCompetition(role))) return StatusCode(StatusCodes.Status403Forbidden);
        return null;
    }

    static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && sql.Number is 2601 or 2627;

    static string Key(ResultUpsertRequest x) => Key(x.Stage, x.Type, x.Participant, x.Event);
    static string Key(ResultDeleteRequest x) => Key(x.Stage, x.Type, x.Participant, x.Event);
    static string Key(string a, string b, string c, string d) => string.Join("\u001f", a.Trim().ToUpperInvariant(), b.Trim().ToUpperInvariant(), c.Trim().ToUpperInvariant(), d.Trim().ToUpperInvariant());
    static CompetitionResultResponse Map(CompetitionResult x) => new(x.PublicId, x.Stage, x.ParticipantType, x.ParticipantCode, x.EventCode, JsonSerializer.Deserialize<decimal[]>(x.AttemptsJson) ?? [], x.Penalty, x.Revision, x.UpdatedAt);
}
