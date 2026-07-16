using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;
using StackMeet.Api.Models;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/competitions/{competitionId:int}/stackers")]
public sealed class StackersController(StackMeetDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StackerResponse>>> List(int competitionId, CancellationToken ct)
    {
        if (!await CompetitionExists(competitionId, ct)) return NotFound();
        return Ok(await database.Stackers.AsNoTracking().Where(x => x.CompetitionId == competitionId)
            .OrderBy(x => x.StackerCode).Select(x => Map(x)).ToListAsync(ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StackerResponse>> Get(int competitionId, int id, CancellationToken ct)
    {
        if (!await CompetitionExists(competitionId, ct)) return NotFound();
        var item = await database.Stackers.AsNoTracking().SingleOrDefaultAsync(x => x.CompetitionId == competitionId && x.Id == id, ct);
        return item is null ? NotFound() : Ok(Map(item));
    }

    [HttpPost]
    public async Task<ActionResult<StackerResponse>> Create(int competitionId, StackerRequest request, CancellationToken ct)
    {
        if (!Valid(request)) return BadRequest();
        if (!await CompetitionExists(competitionId, ct)) return NotFound();
        var value = Normalize(request);
        if (await database.Stackers.AnyAsync(x => x.CompetitionId == competitionId && x.StackerCode == value.StackerCode, ct)) return Conflict(new { error = "StackerCode already exists for this competition." });
        var now = DateTime.UtcNow;
        var item = new Stacker { CompetitionId = competitionId, StackerCode = value.StackerCode, WssaId = value.WssaId, FirstName = value.FirstName, LastName = value.LastName, Gender = value.Gender, BirthDate = value.BirthDate, Country = value.Country, Club = value.Club, Region = value.Region, Email = value.Email, Phone = value.Phone, CustomDivision = value.CustomDivision, Paid = value.Paid!, CheckedIn = value.CheckedIn!, IsSpecialStacker = value.IsSpecialStacker, CreatedAt = now, UpdatedAt = now };
        database.Stackers.Add(item);
        await database.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { competitionId, id = item.Id }, Map(item));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int competitionId, int id, StackerRequest request, CancellationToken ct)
    {
        if (!Valid(request)) return BadRequest();
        if (!await CompetitionExists(competitionId, ct)) return NotFound();
        var value = Normalize(request);
        var item = await database.Stackers.SingleOrDefaultAsync(x => x.CompetitionId == competitionId && x.Id == id, ct);
        if (item is null) return NotFound();
        if (await database.Stackers.AnyAsync(x => x.CompetitionId == competitionId && x.Id != id && x.StackerCode == value.StackerCode, ct)) return Conflict(new { error = "StackerCode already exists for this competition." });
        item.StackerCode = value.StackerCode; item.WssaId = value.WssaId; item.FirstName = value.FirstName; item.LastName = value.LastName; item.Gender = value.Gender; item.BirthDate = value.BirthDate; item.Country = value.Country; item.Club = value.Club; item.Region = value.Region; item.Email = value.Email; item.Phone = value.Phone; item.CustomDivision = value.CustomDivision; item.Paid = value.Paid!; item.CheckedIn = value.CheckedIn!; item.IsSpecialStacker = value.IsSpecialStacker; item.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int competitionId, int id, CancellationToken ct)
    {
        if (!await CompetitionExists(competitionId, ct)) return NotFound();
        var item = await database.Stackers.SingleOrDefaultAsync(x => x.CompetitionId == competitionId && x.Id == id, ct);
        if (item is null) return NotFound();
        database.Stackers.Remove(item);
        await database.SaveChangesAsync(ct);
        return NoContent();
    }

    Task<bool> CompetitionExists(int competitionId, CancellationToken ct) => database.Competitions.AnyAsync(x => x.Id == competitionId, ct);
    static bool Valid(StackerRequest x) => !string.IsNullOrWhiteSpace(x.StackerCode) && !string.IsNullOrWhiteSpace(x.FirstName) && !string.IsNullOrWhiteSpace(x.LastName) && !string.IsNullOrWhiteSpace(x.Gender) && !string.IsNullOrWhiteSpace(x.Country);
    static StackerRequest Normalize(StackerRequest x) => x with { StackerCode = x.StackerCode.Trim(), WssaId = TrimOrNull(x.WssaId), FirstName = x.FirstName.Trim(), LastName = x.LastName.Trim(), Gender = x.Gender.Trim(), Country = x.Country.Trim(), Club = TrimOrNull(x.Club), Region = TrimOrNull(x.Region), Email = TrimOrNull(x.Email), Phone = TrimOrNull(x.Phone), CustomDivision = TrimOrNull(x.CustomDivision), Paid = string.IsNullOrWhiteSpace(x.Paid) ? "No" : x.Paid.Trim(), CheckedIn = string.IsNullOrWhiteSpace(x.CheckedIn) ? "No" : x.CheckedIn.Trim() };
    static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    static StackerResponse Map(Stacker x) => new(x.Id, x.CompetitionId, x.StackerCode, x.WssaId, x.FirstName, x.LastName, x.Gender, x.BirthDate, x.Country, x.Club, x.Region, x.Email, x.Phone, x.CustomDivision, x.Paid, x.CheckedIn, x.IsSpecialStacker, x.CreatedAt, x.UpdatedAt);
}
