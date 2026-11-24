using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Data;

namespace StackMeet.Api.Controllers;

/// <summary>
/// Publishes only the competition participant-code to public career-profile URL bridge needed
/// by the public results portal. Identity resolution remains server-owned: the browser never
/// guesses a permanent identity from name, WSSA ID, demographics, or historical fields.
/// </summary>
[ApiController]
[Route("api/public/competitions/{competitionId}/profile-links")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicResultsProfileLinksController(StackMeetDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string competitionId, CancellationToken ct)
    {
        var normalized = competitionId.Trim().ToUpperInvariant();
        if (normalized.Length is < 3 or > 50) return NotFound();

        var matches = await database.Competitions.AsNoTracking()
            .Where(item => item.CompetitionKey == normalized || item.CompetitionCode == normalized)
            .Select(item => new { item.Id, item.ArchivedAt, item.Status, item.IsPubliclyListed })
            .Take(2).ToListAsync(ct);

        if (matches.Count != 1) return NotFound();
        var competition = matches[0];
        // Direct-link results and permanent cross-event identity publication are separate.
        if (competition.ArchivedAt is not null
            || string.Equals(competition.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        if (!competition.IsPubliclyListed) return Ok(new { links = Array.Empty<object>() });

        var candidates = await database.StackerIdentityLinks.AsNoTracking()
            .Where(link =>
                link.Stacker.CompetitionId == competition.Id
                && link.SportStackerIdentity.IsPublicProfile)
            .OrderBy(link => link.Stacker.StackerCode)
            .Select(link => new
            {
                Participant = link.Stacker.StackerCode,
                link.SportStackerIdentity.NadiTrackId
            })
            .ToListAsync(ct);

        var links = candidates
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.Participant)
                && NadiTrackIdRules.IsValid(item.NadiTrackId))
            .Select(item => new
            {
                participant = item.Participant,
                profileUrl = $"/Stackers/{NadiTrackIdRules.Normalize(item.NadiTrackId)}"
            })
            .ToArray();

        return Ok(new { links });
    }
}
