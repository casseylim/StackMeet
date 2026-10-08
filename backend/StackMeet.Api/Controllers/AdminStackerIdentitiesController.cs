using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Data;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

/// <summary>Protected by the existing /api/admin system-admin/admin-key middleware.</summary>
[ApiController]
[Route("api/admin/stacker-identities")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminStackerIdentitiesController(
    StackMeetDbContext database,
    StackerIdentityBackfillService backfill,
    AuditLogService audit) : ControllerBase
{
    [HttpGet("candidates")]
    public async Task<IActionResult> Candidates(int? competitionId, int take = 100, CancellationToken ct = default)
    {
        if (competitionId is <= 0 || take is < 1 or > 500) return BadRequest();
        var report = await backfill.DiscoverAsync(competitionId, take, ct);
        // Do not serialize domain entities or private matching inputs into an API response.
        return Ok(new { report.TotalStackers, report.LinkedStackers, report.UnlinkedStackers, report.HasMore,
            Items = report.Items.Select(item => new { item.StackerId, item.CompetitionId, item.StackerCode,
                item.DisplayName, item.Status, Candidates = item.Candidates.Select(candidate => new {
                    candidate.NadiTrackId, candidate.DisplayName, candidate.Strength, candidate.Evidence }) }) });
    }

    // Bounded search includes linked registrations so operators can inspect and unlink safely.
    [HttpGet("stackers")]
    public async Task<IActionResult> Stackers(int competitionId, string? search = null, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        if (competitionId <= 0 || skip < 0 || take is < 1 or > 100 || search?.Length > 100) return BadRequest();
        var query = database.Stackers.AsNoTracking().Where(s => s.CompetitionId == competitionId);
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term)) query = query.Where(s => s.StackerCode.Contains(term)
            || (s.FirstName + " " + s.LastName).Contains(term));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(s => s.StackerCode).ThenBy(s => s.Id).Skip(skip).Take(take)
            .Select(s => new { StackerId = s.Id, s.CompetitionId, s.StackerCode,
                DisplayName = s.FirstName + " " + s.LastName,
                NadiTrackId = database.StackerIdentityLinks.Where(l => l.StackerId == s.Id)
                    .Select(l => l.SportStackerIdentity.NadiTrackId).FirstOrDefault() }).ToListAsync(ct);
        return Ok(new { Total = total, Skip = skip, Take = take, Items = items });
    }

    [HttpGet("stackers/{stackerId:int}")]
    public async Task<IActionResult> StackerReview(int stackerId, CancellationToken ct)
    {
        var stacker = await database.Stackers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == stackerId, ct);
        if (stacker is null) return NotFound();
        var link = await database.StackerIdentityLinks.AsNoTracking().Where(l => l.StackerId == stackerId)
            .Select(l => new { LinkId = l.Id, l.SportStackerIdentity.NadiTrackId,
                l.SportStackerIdentity.IsPublicProfile,
                DisplayName = l.SportStackerIdentity.FirstName + " " + l.SportStackerIdentity.LastName }).SingleOrDefaultAsync(ct);
        var competition = await database.Competitions.AsNoTracking().Where(c => c.Id == stacker.CompetitionId)
            .Select(c => new { c.CompetitionCode, c.Status }).SingleAsync(ct);
        var matches = StackerIdentityMatcher.FindMatches(new StackerIdentityMatchQuery(null, stacker.WssaId,
            stacker.FirstName, stacker.LastName, stacker.BirthDate, stacker.Country, stacker.Club, stacker.Email, stacker.Phone),
            await database.SportStackerIdentities.AsNoTracking().ToListAsync(ct));
        return Ok(new { MatchingCandidates = matches.Candidates.Select(c => new { c.Identity.NadiTrackId,
                DisplayName = c.Identity.FirstName + " " + c.Identity.LastName, c.Strength, c.Evidence }),
            StackerId = stacker.Id, stacker.CompetitionId, stacker.StackerCode,
            DisplayName = stacker.FirstName + " " + stacker.LastName, Competition = competition, Link = link,
            CreateAllowed = IdentityNameQuality.IsUsable(stacker.FirstName) && IdentityNameQuality.IsUsable(stacker.LastName),
            ProposedIdentity = new { FirstName = stacker.FirstName.Trim(), LastName = stacker.LastName.Trim(),
                stacker.Gender, stacker.BirthDate, stacker.Country, stacker.Club, stacker.Region, stacker.WssaId,
                IsPublicProfile = false },
            ContactCopyNotice = "Existing contact values, if any, are preserved from this registration and are not shown here." });
    }

    [HttpGet("profiles/{nadiTrackId}")]
    public async Task<IActionResult> Profile(string nadiTrackId, CancellationToken ct)
    {
        if (!NadiTrackIdRules.IsValid(nadiTrackId)) return NotFound();
        var id = NadiTrackIdRules.Normalize(nadiTrackId);
        var profile = await database.SportStackerIdentities.AsNoTracking().Where(item => item.NadiTrackId == id)
            .Select(item => new { item.NadiTrackId, item.IsPublicProfile,
                DisplayName = item.FirstName + " " + item.LastName,
                ProfilePath = "/Stackers/" + item.NadiTrackId,
                Links = database.StackerIdentityLinks.Where(link => link.SportStackerIdentityId == item.Id)
                    .Select(link => new { LinkId = link.Id, link.StackerId }).ToList() }).SingleOrDefaultAsync(ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost("links")]
    public Task<IActionResult> Link(IdentityLinkRequest request, CancellationToken ct) => Mutation(async () =>
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await backfill.ApplyAsync(new StackerIdentityBackfillApplyRequest(
            request.StackerId, request.ExplicitNadiTrackId, request.RequestedAction, request.SelectedNadiTrackId,
            request.CandidateConfirmed, request.CreateNewOverrideConfirmed, request.ResolutionNote,
            audit.CurrentSession()?.UserId), ct);
        if (result.Status != StackerIdentityBackfillApplyStatus.Applied)
            return Conflict(new { error = "Link was not applied. Review current candidates or the existing link.",
                reason = result.Resolution?.ReasonCode });
        var saved = result.Persistence!;
        await audit.Write("StackerIdentity.Linked", "StackerIdentityLink", saved.Link.Id.ToString(),
            audit.CurrentSession()?.UserId, saved.Link.Stacker.CompetitionId,
            newValue: new { saved.Link.StackerId, saved.Identity.NadiTrackId, saved.IdentityCreated }, ct: ct);
        await transaction.CommitAsync(ct);
        return Ok(new { LinkId = saved.Link.Id, saved.Link.StackerId, saved.Identity.NadiTrackId,
            saved.Identity.IsPublicProfile, saved.IdentityCreated, ProfilePath = "/Stackers/" + saved.Identity.NadiTrackId });
    });

    [HttpPut("profiles/{nadiTrackId}/publication")]
    public Task<IActionResult> Publication(string nadiTrackId, IdentityPublicationRequest request, CancellationToken ct) => Mutation(async () =>
    {
        if (!NadiTrackIdRules.IsValid(nadiTrackId)) return NotFound();
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var id = NadiTrackIdRules.Normalize(nadiTrackId);
        var identity = await database.SportStackerIdentities.SingleOrDefaultAsync(item => item.NadiTrackId == id, ct);
        if (identity is null) return NotFound();
        var previous = identity.IsPublicProfile;
        identity.IsPublicProfile = request.IsPublicProfile!.Value;
        identity.UpdatedAt = DateTime.UtcNow;
        await audit.Write("StackerIdentity.PublicationChanged", "SportStackerIdentity", identity.Id.ToString(),
            audit.CurrentSession()?.UserId, oldValue: new { IsPublicProfile = previous },
            newValue: new { identity.IsPublicProfile, request.Reason }, ct: ct);
        await transaction.CommitAsync(ct);
        return Ok(new { identity.NadiTrackId, identity.IsPublicProfile, ProfilePath = "/Stackers/" + identity.NadiTrackId });
    });

    [HttpPost("links/{linkId:long}/unlink")]
    public Task<IActionResult> Unlink(long linkId, IdentityUnlinkRequest request, CancellationToken ct) => Mutation(async () =>
    {
        if (!NadiTrackIdRules.IsValid(request.NadiTrackId)) return BadRequest();
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var link = await database.StackerIdentityLinks.Include(item => item.SportStackerIdentity)
            .Include(item => item.Stacker).SingleOrDefaultAsync(item => item.Id == linkId, ct);
        if (link is null) return NotFound();
        if (link.StackerId != request.StackerId || link.SportStackerIdentity.NadiTrackId != NadiTrackIdRules.Normalize(request.NadiTrackId))
            return Conflict(new { error = "The selected relationship does not match the expected stacker and identity." });
        var previous = new { LinkId = link.Id, link.StackerId, link.SportStackerIdentity.NadiTrackId };
        database.StackerIdentityLinks.Remove(link);
        await audit.Write("StackerIdentity.Unlinked", "StackerIdentityLink", link.Id.ToString(),
            audit.CurrentSession()?.UserId, link.Stacker.CompetitionId, oldValue: previous,
            newValue: new { request.Reason }, ct: ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    });

    private async Task<IActionResult> Mutation(Func<Task<IActionResult>> operation)
    {
        try { return await operation(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException) { return Conflict(new { error = "Identity state changed or the resolution is invalid. Review and retry." }); }
        catch (DbUpdateException ex) when (IsConcurrencyConflict(ex.InnerException)) { return Conflict(new { error = "A concurrent identity change won. Refresh and review before retrying." }); }
        catch (SqlException ex) when (IsConcurrencyConflict(ex)) { return Conflict(new { error = "A concurrent identity change won. Refresh and review before retrying." }); }
    }

    private static bool IsConcurrencyConflict(Exception? error) => error is SqlException sql && sql.Number is 1205 or 2601 or 2627;
}

public sealed record IdentityLinkRequest(
    [Range(1, int.MaxValue)] int StackerId,
    [Range(1, 2)] StackerIdentityResolutionAction RequestedAction,
    [MaxLength(50)] string? ExplicitNadiTrackId,
    [MaxLength(50)] string? SelectedNadiTrackId,
    bool CandidateConfirmed,
    bool CreateNewOverrideConfirmed,
    [Required, MaxLength(1000)] string ResolutionNote);

public sealed record IdentityPublicationRequest(
    [Required] bool? IsPublicProfile,
    [Required, MaxLength(1000)] string Reason);

public sealed record IdentityUnlinkRequest(
    [Range(1, int.MaxValue)] int StackerId,
    [Required, MaxLength(50)] string NadiTrackId,
    [Required, MaxLength(1000)] string Reason);
