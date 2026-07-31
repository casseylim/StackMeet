using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/competitions")]
public sealed class CompetitionsController(StackMeetDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CompetitionResponse>>> List(CancellationToken ct)
    {
        var query = database.Competitions.AsNoTracking();
        if (HttpContext.Items["StackMeetSession"] is SessionToken session)
        {
            if (session.IsAccountSession && !session.IsSystemAdmin)
            {
                query = query.Where(item => item.CompetitionUsers.Any(access => access.IsActive && access.UserId == session.UserId));
            }
            else if (!session.IsAccountSession)
            {
                query = query.Where(item => item.CompetitionKey == session.CompetitionId);
            }
        }

        return Ok(await query.OrderBy(x => x.CompetitionCode).Select(x => Map(x)).ToListAsync(ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CompetitionResponse>> Get(int id, CancellationToken ct)
    {
        var query = database.Competitions.AsNoTracking().Where(x => x.Id == id);
        if (HttpContext.Items["StackMeetSession"] is SessionToken session)
        {
            query = query.Where(item => item.CompetitionKey == session.CompetitionId);
        }

        var item = await query.SingleOrDefaultAsync(ct);
        return item is null ? NotFound() : Ok(Map(item));
    }

    [HttpPost]
    public async Task<ActionResult<CompetitionResponse>> Create(CompetitionRequest request, CancellationToken ct)
    {
        if (!IsMaintenanceRequest()) return Forbid();
        if (!Valid(request)) return BadRequest();
        var value = Normalize(request);
        if (await database.Competitions.AnyAsync(x => x.CompetitionCode == value.CompetitionCode || x.CompetitionKey == value.CompetitionCode, ct)) return Conflict(new { error = "CompetitionCode already exists." });
        var now=DateTime.UtcNow;
        var item=new Competition { CompetitionCode=value.CompetitionCode, CompetitionKey=value.CompetitionCode, CompetitionName=value.CompetitionName, Venue=value.Venue, StartDate=value.StartDate, EndDate=value.EndDate, Status=value.Status, IsPubliclyListed=value.IsPubliclyListed, CreatedAt=now, UpdatedAt=now };
        database.Competitions.Add(item);
        await database.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { item.Id }, Map(item));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CompetitionRequest request, CancellationToken ct)
    {
        if (!IsMaintenanceRequest()) return Forbid();
        if (!Valid(request)) return BadRequest();
        var value = Normalize(request);
        var item=await database.Competitions.SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(item is null)return NotFound();
        if(await database.Competitions.AnyAsync(x=>x.Id!=id&&(x.CompetitionCode==value.CompetitionCode || x.CompetitionKey == value.CompetitionCode),ct))return Conflict(new { error="CompetitionCode already exists."});
        item.CompetitionCode=value.CompetitionCode;
        item.CompetitionKey=value.CompetitionCode;
        item.CompetitionName=value.CompetitionName;
        item.Venue=value.Venue;
        item.StartDate=value.StartDate;
        item.EndDate=value.EndDate;
        item.Status=value.Status;
        item.IsPubliclyListed=value.IsPubliclyListed;
        item.UpdatedAt=DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!IsMaintenanceRequest()) return Forbid();
        var item=await database.Competitions.SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(item is null)return NotFound();
        if (await database.Stackers.AnyAsync(x => x.CompetitionId == id, ct)) return Conflict(new { error = "Competition cannot be deleted while it has stackers." });
        database.Competitions.Remove(item);
        await database.SaveChangesAsync(ct);
        return NoContent();
    }

    bool IsMaintenanceRequest() => HttpContext.Items["StackMeetMaintenanceApiKey"] is true;
    static bool Valid(CompetitionRequest x)=>!string.IsNullOrWhiteSpace(x.CompetitionCode)&&!string.IsNullOrWhiteSpace(x.CompetitionName)&&!string.IsNullOrWhiteSpace(x.Venue)&&x.EndDate>=x.StartDate && CompetitionKeyRules.IsValid(CompetitionKeyRules.Normalize(x.CompetitionCode)) && NormalizeStatus(x.Status) is not null;
    static CompetitionRequest Normalize(CompetitionRequest x) => x with { CompetitionCode = CompetitionKeyRules.Normalize(x.CompetitionCode), CompetitionName = x.CompetitionName.Trim(), Venue = x.Venue.Trim(), Status = NormalizeStatus(x.Status)! };
    static string? NormalizeStatus(string? status) => status?.Trim().ToUpperInvariant() switch { "ACTIVE" => "Active", "CLOSED" => "Closed", "ARCHIVED" => "Archived", "DRAFT" => "Draft", _ => null };
    static CompetitionResponse Map(Competition x)=>new(x.Id,x.CompetitionCode,x.CompetitionName,x.Venue,x.StartDate,x.EndDate,x.Status,x.IsPubliclyListed,x.CreatedAt,x.UpdatedAt);
}
