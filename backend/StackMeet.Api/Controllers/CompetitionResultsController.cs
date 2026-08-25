using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
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
public sealed class CompetitionResultsController(StackMeetDbContext database, CompetitionPermissionService permissions, IHubContext<ResultsHub> resultsHub) : ControllerBase
{
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
        if (request is null || request.Upserts is null || request.Deletes is null || request.Upserts.Length + request.Deletes.Length == 0) return BadRequest(new { error = "At least one result change is required." });
        if (request.Upserts.Any(x => !Valid(x.Stage, x.Type, x.Participant, x.Event, x.Attempts)) || request.Deletes.Any(x => !Valid(x.Stage, x.Type, x.Participant, x.Event, []))) return BadRequest(new { error = "Invalid result identity or attempts." });
        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        var competition = await database.Competitions.FromSqlInterpolated($"SELECT * FROM [dbo].[Competition] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {competitionId}").SingleOrDefaultAsync(ct);
        if (competition is null) return NotFound();
        if (competition.Status is "Closed" or "Archived" || competition.ArchivedAt is not null) return Conflict(new { error = "Results cannot be changed for a closed or archived competition." });
        var keys = request.Upserts.Select(Key).Concat(request.Deletes.Select(Key)).ToArray();
        if (keys.Length != keys.Distinct(StringComparer.Ordinal).Count()) return BadRequest(new { error = "Duplicate logical result in batch." });

        var existing = await database.CompetitionResults.Where(x => x.CompetitionId == competitionId).ToListAsync(ct);
        var touched = new HashSet<CompetitionResult>();
        var deletedCount = 0;
        var actorUserId = (HttpContext.Items["StackMeetSession"] as SessionToken)?.UserId;

        foreach (var item in request.Upserts)
        {
            var row = existing.SingleOrDefault(x => Key(x.Stage, x.ParticipantType, x.ParticipantCode, x.EventCode) == Key(item));
            if (row is not null && item.ExpectedRevision is not null && row.Revision != item.ExpectedRevision) return Conflict(new { error = "This result was changed by another user.", result = Map(row) });
            var now = DateTime.UtcNow;
            if (row is null)
            {
                row = new CompetitionResult
                {
                    CompetitionId = competitionId,
                    Stage = item.Stage.Trim(),
                    ParticipantType = item.Type.Trim(),
                    ParticipantCode = item.Participant.Trim(),
                    EventCode = item.Event.Trim(),
                    CreatedAt = now
                };
                database.CompetitionResults.Add(row);
                existing.Add(row);
            }

            row.AttemptsJson = JsonSerializer.Serialize(item.Attempts);
            row.Penalty = item.Penalty;
            row.UpdatedAt = now;
            row.UpdatedByUserId = actorUserId;
            touched.Add(row);
        }

        foreach (var item in request.Deletes)
        {
            var row = existing.SingleOrDefault(x => Key(x.Stage, x.ParticipantType, x.ParticipantCode, x.EventCode) == Key(item));
            if (row is null) continue;
            if (item.ExpectedRevision is not null && row.Revision != item.ExpectedRevision) return Conflict(new { error = "This result was changed by another user.", result = Map(row) });
            database.CompetitionResults.Remove(row);
            deletedCount++;
        }

        if (touched.Count == 0 && deletedCount == 0)
        {
            await transaction.CommitAsync(ct);
            return await List(competitionId, ct);
        }

        competition.ResultsRevision++;
        foreach (var row in touched)
        {
            row.Revision = competition.ResultsRevision;
        }

        await database.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await resultsHub.Clients.Group(ResultsHub.GroupName(competition.CompetitionKey)).SendAsync("ResultsChanged", new { competitionId, competitionKey = competition.CompetitionKey, revision = competition.ResultsRevision, scope = "results", type = "ResultsChanged" }, ct);
        return await List(competitionId, ct);
    }

    async Task<ActionResult?> Access(int competitionId, bool write, CancellationToken ct)
    {
        if (HttpContext.Items["StackMeetSession"] is not SessionToken session || session.UserId is null) return Unauthorized();
        var role = await permissions.RoleForCompetitionId(session.UserId.Value, session.IsSystemAdmin, competitionId, ct);
        if (role is null || (write ? !CompetitionPermissionService.CanEnterResults(role) : !CompetitionPermissionService.CanViewCompetition(role))) return Forbid();
        return null;
    }

    static bool Valid(string stage, string type, string participant, string ev, decimal[] attempts) => !string.IsNullOrWhiteSpace(stage) && !string.IsNullOrWhiteSpace(type) && !string.IsNullOrWhiteSpace(participant) && !string.IsNullOrWhiteSpace(ev) && attempts.All(x => x >= 0 && x <= 86400);
    static string Key(ResultUpsertRequest x) => Key(x.Stage, x.Type, x.Participant, x.Event);
    static string Key(ResultDeleteRequest x) => Key(x.Stage, x.Type, x.Participant, x.Event);
    static string Key(string a, string b, string c, string d) => string.Join("\u001f", a.Trim().ToUpperInvariant(), b.Trim().ToUpperInvariant(), c.Trim().ToUpperInvariant(), d.Trim().ToUpperInvariant());
    static CompetitionResultResponse Map(CompetitionResult x) => new(x.PublicId, x.Stage, x.ParticipantType, x.ParticipantCode, x.EventCode, JsonSerializer.Deserialize<decimal[]>(x.AttemptsJson) ?? [], x.Penalty, x.Revision, x.UpdatedAt);
}
