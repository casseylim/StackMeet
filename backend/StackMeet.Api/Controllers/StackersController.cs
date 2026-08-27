using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/competitions/{competitionId:int}/stackers")]
public sealed class StackersController(StackMeetDbContext database, CompetitionPermissionService permissions, CompetitionParticipantReferenceService references) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StackerResponse>>> List(int competitionId, CancellationToken ct)
    {
        var access = await Access(competitionId, false, ct);
        if (access is not null) return access;
        if (!await CompetitionExists(competitionId, ct)) return NotFound();
        return Ok(await database.Stackers.AsNoTracking().Where(x => x.CompetitionId == competitionId)
            .OrderBy(x => x.StackerCode).Select(x => Map(x)).ToListAsync(ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StackerResponse>> Get(int competitionId, int id, CancellationToken ct)
    {
        var access = await Access(competitionId, false, ct);
        if (access is not null) return access;
        if (!await CompetitionExists(competitionId, ct)) return NotFound();
        var item = await database.Stackers.AsNoTracking().SingleOrDefaultAsync(x => x.CompetitionId == competitionId && x.Id == id, ct);
        return item is null ? NotFound() : Ok(Map(item));
    }

    [HttpPost]
    public async Task<ActionResult<StackerResponse>> Create(int competitionId, StackerRequest request, CancellationToken ct)
    {
        var access = await Access(competitionId, true, ct);
        if (access is not null) return access;
        var validation = Validate(request); if (validation is not null) return BadRequest(new { error = validation });
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
        var access = await Access(competitionId, true, ct);
        if (access is not null) return access;
        var validation = Validate(request); if (validation is not null) return BadRequest(new { error = validation });
        if (!await CompetitionExists(competitionId, ct)) return NotFound();
        var value = Normalize(request);
        var item = await database.Stackers.SingleOrDefaultAsync(x => x.CompetitionId == competitionId && x.Id == id, ct);
        if (item is null) return NotFound();
        if (!string.Equals(value.StackerCode, item.StackerCode, StringComparison.Ordinal)) return Conflict(new { error = "StackerCode cannot be changed after participant creation." });
        if (await database.Stackers.AnyAsync(x => x.CompetitionId == competitionId && x.Id != id && x.StackerCode == value.StackerCode, ct)) return Conflict(new { error = "StackerCode already exists for this competition." });
        item.StackerCode = value.StackerCode; item.WssaId = value.WssaId; item.FirstName = value.FirstName; item.LastName = value.LastName; item.Gender = value.Gender; item.BirthDate = value.BirthDate; item.Country = value.Country; item.Club = value.Club; item.Region = value.Region; item.Email = value.Email; item.Phone = value.Phone; item.CustomDivision = value.CustomDivision; item.Paid = value.Paid!; item.CheckedIn = value.CheckedIn!; item.IsSpecialStacker = value.IsSpecialStacker; item.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int competitionId, int id, CancellationToken ct)
    {
        var access = await Access(competitionId, true, ct);
        if (access is not null) return access;
        if (!await CompetitionExists(competitionId, ct)) return NotFound();
        var item = await database.Stackers.SingleOrDefaultAsync(x => x.CompetitionId == competitionId && x.Id == id, ct);
        if (item is null) return NotFound();
        if (await database.CompetitionResults.AnyAsync(x => x.CompetitionId == competitionId && x.ParticipantCode == item.StackerCode, ct)) return Conflict(new { error = "Participant cannot be deleted while competition results or team references exist." });
        var key = await database.Competitions.Where(x => x.Id == competitionId).Select(x => x.CompetitionKey).SingleAsync(ct);
        var state = await database.CompetitionStates.AsNoTracking().SingleOrDefaultAsync(x => x.CompetitionKey == key, ct);
        if (state is not null && references.ContainsParticipant(state.JsonData, item.StackerCode)) return Conflict(new { error = "Participant cannot be deleted while competition results or team references exist." });
        database.Stackers.Remove(item);
        await database.SaveChangesAsync(ct);
        return NoContent();
    }

    async Task<ActionResult?> Access(int competitionId, bool write, CancellationToken ct)
    {
        // Preserve the existing maintenance-key recovery path and legacy competition-password
        // sessions during the account/RBAC migration. Account sessions must honor their assigned role.
        if (HttpContext.Items["StackMeetMaintenanceApiKey"] is true) return null;
        if (HttpContext.Items["StackMeetSession"] is not SessionToken session) return Unauthorized();
        if (!session.IsAccountSession) return null;
        if (session.UserId is null) return Unauthorized();

        var role = await permissions.RoleForCompetitionId(session.UserId.Value, session.IsSystemAdmin, competitionId, ct);
        if (role is null
            || (write
                ? !CompetitionPermissionService.CanManageCompetition(role)
                : !CompetitionPermissionService.CanViewCompetition(role)))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return null;
    }

    Task<bool> CompetitionExists(int competitionId, CancellationToken ct) => database.Competitions.AnyAsync(x => x.Id == competitionId, ct);
    static string? Validate(StackerRequest x)
    {
        if (string.IsNullOrWhiteSpace(x.StackerCode)) return "StackerCode is required.";
        if (string.IsNullOrWhiteSpace(x.FirstName)) return "First name is required.";
        if (string.IsNullOrWhiteSpace(x.LastName)) return "Last name is required.";
        if (string.IsNullOrWhiteSpace(x.Gender)) return "Gender is required.";
        if (string.IsNullOrWhiteSpace(x.Country)) return "Country is required.";
        var lengths = new (string Name, string? Value, int Max)[] { ("StackerCode", x.StackerCode, 50), ("WssaId", x.WssaId, 50), ("First name", x.FirstName, 100), ("Last name", x.LastName, 100), ("Gender", x.Gender, 20), ("Country", x.Country, 100), ("Club", x.Club, 200), ("Region", x.Region, 100), ("Email", x.Email, 200), ("Phone", x.Phone, 50), ("Custom division", x.CustomDivision, 100), ("Paid", x.Paid, 10), ("CheckedIn", x.CheckedIn, 10) };
        var oversized = lengths.FirstOrDefault(item => item.Value?.Length > item.Max);
        return oversized.Value is null ? null : $"{oversized.Name} must be {oversized.Max} characters or fewer.";
    }
    static StackerRequest Normalize(StackerRequest x) => x with { StackerCode = x.StackerCode.Trim(), WssaId = TrimOrNull(x.WssaId), FirstName = x.FirstName.Trim(), LastName = x.LastName.Trim(), Gender = x.Gender.Trim(), Country = x.Country.Trim(), Club = TrimOrNull(x.Club), Region = TrimOrNull(x.Region), Email = TrimOrNull(x.Email), Phone = TrimOrNull(x.Phone), CustomDivision = TrimOrNull(x.CustomDivision), Paid = string.IsNullOrWhiteSpace(x.Paid) ? "No" : x.Paid.Trim(), CheckedIn = string.IsNullOrWhiteSpace(x.CheckedIn) ? "No" : x.CheckedIn.Trim() };
    static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    static StackerResponse Map(Stacker x) => new(x.Id, x.CompetitionId, x.StackerCode, x.WssaId, x.FirstName, x.LastName, x.Gender, x.BirthDate, x.Country, x.Club, x.Region, x.Email, x.Phone, x.CustomDivision, x.Paid, x.CheckedIn, x.IsSpecialStacker, x.CreatedAt, x.UpdatedAt);
}
