using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

/// <summary>
/// Manages Phase 1 user accounts and per-competition access assignments.
/// </summary>
/// <remarks>
/// These endpoints live under /api/admin, so the existing admin-key middleware protects them
/// until full system-admin account authorization replaces it.
/// </remarks>
[ApiController]
[Route("api/admin/users")]
public sealed class AdminUsersController(
    StackMeetDbContext database,
    PasswordHashService passwords) : ControllerBase
{
    /// <summary>
    /// Lists all application accounts with their competition assignments.
    /// </summary>
    /// <remarks>
    /// Use this for the first admin UI pass; it intentionally excludes password hashes.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminUserResponse>>> List(CancellationToken ct)
    {
        var users = await database.AppUsers
            .AsNoTracking()
            .Include(item => item.CompetitionUsers)
                .ThenInclude(item => item.Competition)
            .Include(item => item.CompetitionUsers)
                .ThenInclude(item => item.Role)
            .OrderBy(item => item.Email)
            .ToListAsync(ct);

        return Ok(users.Select(MapUser).ToList());
    }

    /// <summary>
    /// Creates a manually administered email-login account.
    /// </summary>
    /// <remarks>
    /// No invitation email is sent in Phase 1; admins communicate the temporary password manually.
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<AdminUserResponse>> Create(AdminCreateUserRequest request, CancellationToken ct)
    {
        var validation = ValidateCreate(request, out var normalizedEmail);
        if (validation is not null) return BadRequest(new { error = validation });

        if (await database.AppUsers.AnyAsync(item => item.NormalizedEmail == normalizedEmail, ct))
        {
            return Conflict(new { error = "Email already exists." });
        }

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwords.Hash(request.Password),
            IsActive = true,
            IsSystemAdmin = request.IsSystemAdmin,
            EmailConfirmed = request.EmailConfirmed,
            CreatedAt = now
        };

        database.AppUsers.Add(user);
        await database.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = user.Id }, await ReadUser(user.Id, ct));
    }

    /// <summary>
    /// Returns one application account with its competition assignments.
    /// </summary>
    /// <remarks>
    /// The response is shaped for an account-management screen rather than authentication.
    /// </remarks>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminUserResponse>> Get(int id, CancellationToken ct)
    {
        var user = await ReadUser(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>
    /// Updates account metadata and active/system-admin flags.
    /// </summary>
    /// <remarks>
    /// Disabling a user preserves historical audit records and competition assignments.
    /// </remarks>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, AdminUpdateUserRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName)) return BadRequest(new { error = "Display name is required." });

        var user = await database.AppUsers.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (user is null) return NotFound();

        user.DisplayName = request.DisplayName.Trim();
        user.IsActive = request.IsActive;
        user.IsSystemAdmin = request.IsSystemAdmin;
        user.EmailConfirmed = request.EmailConfirmed;
        await database.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Replaces a user's password during manual account administration.
    /// </summary>
    /// <remarks>
    /// This endpoint is the Phase 1 substitute for forgot-password email flows.
    /// </remarks>
    [HttpPost("{id:int}/password")]
    public async Task<IActionResult> SetPassword(int id, AdminSetUserPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return BadRequest(new { error = "Password must be at least 8 characters." });
        }

        var user = await database.AppUsers.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (user is null) return NotFound();

        user.PasswordHash = passwords.Hash(request.Password);
        await database.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Assigns or updates a user's role for one competition.
    /// </summary>
    /// <remarks>
    /// This is the core Phase 1 access model: role belongs to user plus competition, not just user.
    /// </remarks>
    [HttpPost("{id:int}/competition-access")]
    public async Task<ActionResult<AdminUserResponse>> SetCompetitionAccess(int id, AdminCompetitionAccessRequest request, CancellationToken ct)
    {
        var user = await database.AppUsers.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (user is null) return NotFound();

        if (!await database.Competitions.AnyAsync(item => item.Id == request.CompetitionId, ct))
        {
            return BadRequest(new { error = "Competition does not exist." });
        }

        var role = await database.AppRoles.SingleOrDefaultAsync(item => item.Name == request.Role.Trim(), ct);
        if (role is null) return BadRequest(new { error = "Role must be SystemAdmin, CompetitionManager, DataEntry or Viewer." });

        var access = await database.CompetitionUsers
            .SingleOrDefaultAsync(item => item.UserId == id && item.CompetitionId == request.CompetitionId, ct);

        if (access is null)
        {
            access = new CompetitionUser
            {
                UserId = id,
                CompetitionId = request.CompetitionId,
                RoleId = role.Id,
                IsActive = request.IsActive,
                AssignedAt = DateTime.UtcNow
            };
            database.CompetitionUsers.Add(access);
        }
        else
        {
            access.RoleId = role.Id;
            access.IsActive = request.IsActive;
        }

        await database.SaveChangesAsync(ct);
        return Ok(await ReadUser(id, ct));
    }

    /// <summary>
    /// Validates the minimum fields needed to create a manual Phase 1 account.
    /// </summary>
    /// <remarks>
    /// The normalized email is returned for unique-index lookups and storage.
    /// </remarks>
    static string? ValidateCreate(AdminCreateUserRequest request, out string normalizedEmail)
    {
        normalizedEmail = EmailRules.Normalize(request.Email);
        if (!EmailRules.IsValid(request.Email)) return "Valid email is required.";
        if (string.IsNullOrWhiteSpace(request.DisplayName)) return "Display name is required.";
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8) return "Password must be at least 8 characters.";
        return null;
    }

    /// <summary>
    /// Reads a single user with competition and role navigation data loaded.
    /// </summary>
    /// <remarks>
    /// This keeps create/update responses consistent with the list response.
    /// </remarks>
    async Task<AdminUserResponse?> ReadUser(int id, CancellationToken ct)
    {
        return await database.AppUsers
            .AsNoTracking()
            .Include(item => item.CompetitionUsers)
                .ThenInclude(item => item.Competition)
            .Include(item => item.CompetitionUsers)
                .ThenInclude(item => item.Role)
            .Where(item => item.Id == id)
            .SingleOrDefaultAsync(ct) is { } user
                ? MapUser(user)
                : null;
    }

    /// <summary>
    /// Maps an AppUser entity to its admin-safe response shape.
    /// </summary>
    /// <remarks>
    /// The mapper intentionally omits password hashes and any future reset-token material.
    /// </remarks>
    static AdminUserResponse MapUser(AppUser user) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.IsActive,
        user.EmailConfirmed,
        user.IsSystemAdmin,
        user.CreatedAt,
        user.LastLoginAt,
        user.CompetitionUsers
            .OrderBy(access => access.Competition.CompetitionCode)
            .Select(access => new AdminCompetitionAccessResponse(
                access.Id,
                access.CompetitionId,
                access.Competition.CompetitionKey,
                access.Competition.CompetitionName,
                access.Role.Name,
                access.IsActive,
                access.AssignedAt))
            .ToList());
}
