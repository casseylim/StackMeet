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
    AccountEmailService emails,
    AccountLinkService accountLinks,
    AuditLogService auditLogs) : ControllerBase
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
        await auditLogs.Write(
            "admin.user.created",
            "AppUser",
            user.Id.ToString(),
            ActorUserId(),
            null,
            null,
            new { user.Email, user.DisplayName, user.IsActive, user.IsSystemAdmin, user.EmailConfirmed },
            ct);

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
        await auditLogs.Write(
            "admin.user.invited",
            "AppUser",
            user.Id.ToString(),
            ActorUserId(),
            null,
            null,
            new
            {
                user.Email,
                user.DisplayName,
                user.IsSystemAdmin,
                competitionAccess = request.CompetitionAccess?.Select(item => new { item.CompetitionId, item.Role, item.IsActive })
            },
            ct);
        await transaction.CommitAsync(ct);

        var link = accountLinks.ActivationLink(rawToken);
        try
        {
            await emails.SendActivationEmail(user.Email, user.DisplayName, link, ct);
        }
        catch (Exception error)
        {
            return EmailSendFailure(error);
        }
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

        var before = new { user.DisplayName, user.IsActive, user.IsSystemAdmin, user.EmailConfirmed };
        user.DisplayName = request.DisplayName.Trim();
        user.IsActive = request.IsActive;
        user.IsSystemAdmin = request.IsSystemAdmin;
        user.EmailConfirmed = request.EmailConfirmed;
        await database.SaveChangesAsync(ct);
        await auditLogs.Write(
            "admin.user.updated",
            "AppUser",
            user.Id.ToString(),
            ActorUserId(),
            null,
            before,
            new { user.DisplayName, user.IsActive, user.IsSystemAdmin, user.EmailConfirmed },
            ct);
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
        await auditLogs.Write(
            "admin.user.password_set",
            "AppUser",
            user.Id.ToString(),
            ActorUserId(),
            null,
            null,
            new { user.Email },
            ct);
        return NoContent();
    }

    /// <summary>
    /// Deletes one application account and its direct access records.
    /// </summary>
    /// <remarks>
    /// Audit rows are preserved, and the endpoint refuses to remove the last global admin account.
    /// </remarks>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, AdminDeleteUserRequest request, CancellationToken ct)
    {
        var user = await database.AppUsers
            .Include(item => item.CompetitionUsers)
            .Include(item => item.Tokens)
            .SingleOrDefaultAsync(item => item.Id == id, ct);
        if (user is null) return NotFound();

        var expectedConfirmation = $"DELETE {user.Email}";
        if (!string.Equals(request.Confirmation?.Trim(), expectedConfirmation, StringComparison.Ordinal))
        {
            return BadRequest(new { error = $"Type {expectedConfirmation} to confirm deletion." });
        }

        if (user.IsSystemAdmin)
        {
            var remainingAdminCount = await database.AppUsers.CountAsync(item => item.IsSystemAdmin && item.Id != id, ct);
            if (remainingAdminCount == 0)
            {
                return BadRequest(new { error = "At least one global system admin must remain." });
            }
        }

        var assignedByRows = await database.CompetitionUsers
            .Where(item => item.AssignedByUserId == id)
            .ToListAsync(ct);
        foreach (var access in assignedByRows)
        {
            access.AssignedByUserId = null;
        }

        var before = new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.EmailConfirmed,
            user.IsSystemAdmin,
            CompetitionAccessCount = user.CompetitionUsers.Count,
            TokenCount = user.Tokens.Count
        };

        var actorUserId = ActorUserId();
        if (actorUserId == id) actorUserId = null;

        database.CompetitionUsers.RemoveRange(user.CompetitionUsers);
        database.AppUserTokens.RemoveRange(user.Tokens);
        database.AppUsers.Remove(user);
        await database.SaveChangesAsync(ct);
        await auditLogs.Write(
            "admin.user.deleted",
            "AppUser",
            id.ToString(),
            actorUserId,
            null,
            before,
            new { user.Email },
            ct);
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
        var link = accountLinks.PasswordResetLink(rawToken);
        try
        {
            await emails.SendPasswordResetEmail(user.Email, user.DisplayName, link, ct);
        }
        catch (Exception error)
        {
            return EmailSendFailure(error);
        }
        await auditLogs.Write(
            "admin.user.password_reset_requested",
            "AppUser",
            user.Id.ToString(),
            ActorUserId(),
            null,
            null,
            new { user.Email },
            ct);
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

        var before = await AccessSnapshot(id, request.CompetitionId, ct);
        var accessError = await UpsertAccess(id, request, ct);
        if (accessError is not null) return BadRequest(new { error = accessError });

        await database.SaveChangesAsync(ct);
        var after = await AccessSnapshot(id, request.CompetitionId, ct);
        await auditLogs.Write(
            "admin.user.competition_access_set",
            "CompetitionUser",
            after?.Id.ToString(),
            ActorUserId(),
            request.CompetitionId,
            before,
            after,
            ct);
        return Ok(await ReadUser(id, ct));
    }

    /// <summary>
    /// Deactivates one competition assignment for a user.
    /// </summary>
    /// <remarks>
    /// Assignments are retained for audit context but hidden from active access checks.
    /// </remarks>
    [HttpDelete("{id:int}/competition-access/{accessId:int}")]
    public async Task<ActionResult<AdminUserResponse>> RemoveCompetitionAccess(int id, int accessId, CancellationToken ct)
    {
        var access = await database.CompetitionUsers
            .Include(item => item.Competition)
            .Include(item => item.Role)
            .SingleOrDefaultAsync(item => item.Id == accessId && item.UserId == id, ct);
        if (access is null) return NotFound();

        var before = new { access.Id, access.UserId, access.CompetitionId, access.Competition.CompetitionKey, Role = access.Role.Name, access.IsActive };
        access.IsActive = false;
        await database.SaveChangesAsync(ct);
        await auditLogs.Write(
            "admin.user.competition_access_removed",
            "CompetitionUser",
            access.Id.ToString(),
            ActorUserId(),
            access.CompetitionId,
            before,
            new { access.Id, access.UserId, access.CompetitionId, access.Competition.CompetitionKey, Role = access.Role.Name, access.IsActive },
            ct);
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
    /// Reads the optional account actor from the current admin request.
    /// </summary>
    /// <remarks>
    /// Admin-key-only requests do not identify a specific user, so audit rows allow null actor ids.
    /// </remarks>
    int? ActorUserId() => auditLogs.CurrentSession()?.UserId;

    /// <summary>
    /// Returns an admin-visible error when SMTP delivery fails.
    /// </summary>
    /// <remarks>
    /// The message is intentionally limited to the exception text and does not include SMTP credentials.
    /// </remarks>
    ObjectResult EmailSendFailure(Exception error)
    {
        return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Email could not be sent: {error.Message}" });
    }

    /// <summary>
    /// Captures one competition assignment in an audit-safe shape.
    /// </summary>
    /// <remarks>
    /// This avoids logging EF navigation objects while still showing the role and active flag change.
    /// </remarks>
    async Task<AccessAuditSnapshot?> AccessSnapshot(int userId, int competitionId, CancellationToken ct)
    {
        return await database.CompetitionUsers
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.CompetitionId == competitionId)
            .Select(item => new AccessAuditSnapshot(
                item.Id,
                item.UserId,
                item.CompetitionId,
                item.Competition.CompetitionKey,
                item.Role.Name,
                item.IsActive,
                item.AssignedAt))
            .SingleOrDefaultAsync(ct);
    }

    sealed record AccessAuditSnapshot(
        int Id,
        int UserId,
        int CompetitionId,
        string CompetitionKey,
        string Role,
        bool IsActive,
        DateTime AssignedAt);

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
