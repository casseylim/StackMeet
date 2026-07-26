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
    PasswordHashService passwords,
    AccountTokenService tokens,
    AccountEmailService emails) : ControllerBase
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
    /// Creates an inactive account and sends an activation email.
    /// </summary>
    /// <remarks>
    /// The user sets their own password through the emailed one-time activation link.
    /// </remarks>
    [HttpPost("invite")]
    public async Task<ActionResult<AdminEmailLinkResponse>> Invite(AdminInviteUserRequest request, CancellationToken ct)
    {
        var validation = ValidateInvite(request, out var normalizedEmail);
        if (validation is not null) return BadRequest(new { error = validation });
        if (await database.AppUsers.AnyAsync(item => item.NormalizedEmail == normalizedEmail, ct))
        {
            return Conflict(new { error = "Email already exists." });
        }

        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwords.Hash(Guid.NewGuid().ToString("N")),
            IsActive = false,
            IsSystemAdmin = request.IsSystemAdmin,
            EmailConfirmed = false,
            CreatedAt = now
        };
        database.AppUsers.Add(user);
        await database.SaveChangesAsync(ct);

        foreach (var access in request.CompetitionAccess ?? [])
        {
            var result = await UpsertAccess(user.Id, access, ct);
            if (result is not null) return BadRequest(new { error = result });
        }

        var rawToken = await tokens.CreateToken(user.Id, AccountTokenService.ActivationPurpose, TimeSpan.FromDays(7), ct);
        await transaction.CommitAsync(ct);

        var link = AccountLink("activate", rawToken);
        await emails.SendActivationEmail(user.Email, user.DisplayName, link, ct);
        return Ok(new AdminEmailLinkResponse("Activation email sent.", link));
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
    /// Sends a password-reset link to an existing user.
    /// </summary>
    /// <remarks>
    /// This replaces manual password setting for normal operation once SMTP is configured.
    /// </remarks>
    [HttpPost("{id:int}/password-reset")]
    public async Task<ActionResult<AdminEmailLinkResponse>> SendPasswordReset(int id, CancellationToken ct)
    {
        var user = await database.AppUsers.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (user is null) return NotFound();

        var rawToken = await tokens.CreateToken(user.Id, AccountTokenService.PasswordResetPurpose, TimeSpan.FromHours(2), ct);
        var link = AccountLink("reset", rawToken);
        await emails.SendPasswordResetEmail(user.Email, user.DisplayName, link, ct);
        return Ok(new AdminEmailLinkResponse("Password reset email sent.", link));
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

        var accessError = await UpsertAccess(id, request, ct);
        if (accessError is not null) return BadRequest(new { error = accessError });

        await database.SaveChangesAsync(ct);
        return Ok(await ReadUser(id, ct));
    }

    /// <summary>
    /// Creates or updates one competition access assignment.
    /// </summary>
    /// <remarks>
    /// Used by both invite creation and later access-right editing.
    /// </remarks>
    async Task<string?> UpsertAccess(int userId, AdminCompetitionAccessRequest request, CancellationToken ct)
    {
        if (!await database.Competitions.AnyAsync(item => item.Id == request.CompetitionId, ct))
        {
            return "Competition does not exist.";
        }

        var role = await database.AppRoles.SingleOrDefaultAsync(item => item.Name == request.Role.Trim(), ct);
        if (role is null) return "Role must be SystemAdmin, CompetitionManager, DataEntry or Viewer.";

        var access = await database.CompetitionUsers
            .SingleOrDefaultAsync(item => item.UserId == userId && item.CompetitionId == request.CompetitionId, ct);

        if (access is null)
        {
            database.CompetitionUsers.Add(new CompetitionUser
            {
                UserId = userId,
                CompetitionId = request.CompetitionId,
                RoleId = role.Id,
                IsActive = request.IsActive,
                AssignedAt = DateTime.UtcNow
            });
        }
        else
        {
            access.RoleId = role.Id;
            access.IsActive = request.IsActive;
        }

        return null;
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

    static string? ValidateInvite(AdminInviteUserRequest request, out string normalizedEmail)
    {
        normalizedEmail = EmailRules.Normalize(request.Email);
        if (!EmailRules.IsValid(request.Email)) return "Valid email is required.";
        if (string.IsNullOrWhiteSpace(request.DisplayName)) return "Display name is required.";
        return null;
    }

    /// <summary>
    /// Builds the browser link used in activation and password-reset emails.
    /// </summary>
    /// <remarks>
    /// Local testing links point to localhost; deployed links point to the request host.
    /// </remarks>
    string AccountLink(string purpose, string rawToken)
    {
        var url = new UriBuilder(Request.Scheme, Request.Host.Host)
        {
            Path = "account.html",
            Query = $"purpose={Uri.EscapeDataString(purpose)}&token={Uri.EscapeDataString(rawToken)}"
        };
        if (Request.Host.Port is { } port) url.Port = port;
        return url.Uri.ToString();
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
