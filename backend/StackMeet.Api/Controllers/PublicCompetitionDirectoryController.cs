using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;

namespace StackMeet.Api.Controllers;

/// <summary>Provides the opt-in public directory of completed and active competitions.</summary>
/// <remarks>Only competitions explicitly marked for public listing are returned.</remarks>
[ApiController]
[Route("api/public/competitions")]
public sealed class PublicCompetitionDirectoryController(StackMeetDbContext database) : ControllerBase
{
    /// <summary>Returns publicly listed competitions in descending event-date order.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await database.Competitions.AsNoTracking()
            .Where(item => item.IsPubliclyListed && item.ArchivedAt == null && item.Status != "Archived")
            .OrderByDescending(item => item.StartDate)
            .ThenBy(item => item.CompetitionName)
            .Select(item => new
            {
                id = item.CompetitionCode,
                name = item.CompetitionName,
                venue = item.Venue,
                startDate = item.StartDate,
                endDate = item.EndDate,
                status = item.Status
            })
            .ToListAsync(ct);

        return Ok(rows);
    }
}
