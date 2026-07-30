using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    StackMeetDbContext database,
    PasswordHashService passwords,
    SessionTokenService tokens,
    AccountTokenService accountTokens,
    AccountEmailService emails,
    AccountLinkService accountLinks,
    ProtectedSettingService settings,
    AuditLogService auditLogs) : ControllerBase
{
    static readonly TimeSpan PasswordResetRequestCooldown = TimeSpan.FromMinutes(5);
    static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromMinutes(60);
    const int PasswordResetHourlyLimit = 5;
    const string RequireEmailConfirmedSettingKey = "Auth:RequireEmailConfirmed";

    /// <summary>
    /// Authenticates either a Phase 1 email account or the legacy competition-password flow.
    /// </summary>
    /// <remarks>
    /// Email login is preferred when Email is supplied; CompetitionId login remains for migration safety.
    /// </remarks>
    [HttpPost("login")]
    [EnableRateLimiting("Login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokens.SigningKey))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Login security is not configured." });
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            return await LoginAccount(request, cancellationToken);
        }

        return await LoginCompetition(request, cancellationToken);
    }

    /// <summary>
    /// Returns the current account session and assigned competition roles.
    /// </summary>
    /// <remarks>
    /// Legacy competition sessions return a minimal identity so the old frontend can continue to work.
    /// </remarks>
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        var bearerToken = BearerToken(Request.Headers.Authorization.FirstOrDefault());
        if (!tokens.TryValidate(bearerToken, out var session))
        {
            return Unauthorized(new { error = "Valid login session required." });
        }

        if (!session.IsAccountSession)
        {
            return Ok(new CurrentUserResponse(null, null, session.DisplayName, false, session.ExpiresAt, []));
        }

        var user = await database.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == session.UserId && item.IsActive, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new { error = "Account is no longer active." });
        }

        return Ok(new CurrentUserResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.IsSystemAdmin,
            session.ExpiresAt,
            await CompetitionAccessFor(user.Id, cancellationToken)));
    }

    /// <summary>
    /// Provides a stable logout endpoint for the browser client.
    /// </summary>
    /// <remarks>
    /// Phase 1 bearer tokens are stateless, so logout is client-side token disposal until cookie auth lands.
    /// </remarks>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var bearerToken = BearerToken(Request.Headers.Authorization.FirstOrDefault());
        if (tokens.TryValidate(bearerToken, out var session))
        {
            await auditLogs.Write(
                session.IsAccountSession ? "auth.logout.account" : "auth.logout.competition",
                session.IsAccountSession ? "AppUser" : "Competition",
                session.IsAccountSession ? session.UserId?.ToString() : session.CompetitionId,
                session.UserId,
                null,
                null,
                new { session.Email, session.DisplayName, session.CompetitionId },
                cancellationToken);
        }

        return NoContent();
    }

    /// <summary>
    /// Sends a password-reset link for an active account when the email exists.
    /// </summary>
    /// <remarks>
    /// The response is intentionally generic to avoid exposing whether an email is registered.
    /// </remarks>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("Login")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!EmailRules.IsValid(request.Email))
        {
            return BadRequest(new { error = "Valid email address is required." });
        }

        var normalizedEmail = EmailRules.Normalize(request.Email);
        var user = await database.AppUsers.SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail && item.IsActive, cancellationToken);
        if (user is not null)
        {
            if (await HasRecentPasswordResetToken(user.Id, cancellationToken))
            {
                await auditLogs.Write(
                    "auth.password_reset.request_throttled",
                    "AppUser",
                    user.Id.ToString(),
                    user.Id,
                    null,
                    null,
                    new { user.Email, WindowMinutes = PasswordResetRequestCooldown.TotalMinutes },
                    cancellationToken);
                return Ok(new { message = "If this email is registered, a password reset link has been sent." });
            }
            if (await PasswordResetRequestCount(user.Id, TimeSpan.FromHours(1), cancellationToken) >= PasswordResetHourlyLimit)
            {
                await auditLogs.Write(
                    "auth.password_reset.request_hourly_limit",
                    "AppUser",
                    user.Id.ToString(),
                    user.Id,
                    null,
                    null,
                    new { user.Email, Limit = PasswordResetHourlyLimit, WindowMinutes = 60 },
                    cancellationToken);
                return Ok(new { message = "If this email is registered, a password reset link has been sent." });
            }

            var rawToken = await accountTokens.CreateToken(user.Id, AccountTokenService.PasswordResetPurpose, PasswordResetTokenLifetime, cancellationToken);
            var link = accountLinks.PasswordResetLink(rawToken);
            try
            {
                await emails.SendPasswordResetEmail(user.Email, user.DisplayName, link, cancellationToken);
            }
            catch (Exception error)
            {
                await auditLogs.Write(
                    "auth.password_reset.request_failed",
                    "AppUser",
                    user.Id.ToString(),
                    user.Id,
                    null,
                    null,
                    new { user.Email, Error = error.Message },
                    cancellationToken);
                return StatusCode(StatusCodes.Status502BadGateway, new { error = "Reset email could not be sent. Contact your system admin." });
            }
        }

        await auditLogs.Write(
            "auth.password_reset.requested",
            "AppUser",
            user?.Id.ToString() ?? normalizedEmail,
            user?.Id,
            null,
            null,
            new { email = normalizedEmail, Sent = user is not null, ExpiresInMinutes = user is not null ? (double?)PasswordResetTokenLifetime.TotalMinutes : null },
            cancellationToken);
        return Ok(new { message = "If this email is registered, a password reset link has been sent." });
    }

    /// <summary>
    /// Checks whether an account recently requested a password reset.
    /// </summary>
    /// <remarks>
    /// Returning the same public message avoids revealing whether the email exists while preventing repeated reset emails.
    /// </remarks>
    async Task<bool> HasRecentPasswordResetToken(int userId, CancellationToken cancellationToken)
    {
        return await PasswordResetRequestCount(userId, PasswordResetRequestCooldown, cancellationToken) > 0;
    }

    /// <summary>
    /// Counts recent password-reset token requests for per-account throttling.
    /// </summary>
    /// <remarks>
    /// Counting token rows avoids a new table while still enforcing cooldown and hourly caps.
    /// </remarks>
    async Task<int> PasswordResetRequestCount(int userId, TimeSpan window, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.Subtract(window);
        return await database.AppUserTokens.CountAsync(item =>
            item.UserId == userId
            && item.Purpose == AccountTokenService.PasswordResetPurpose
            && item.CreatedAt >= since,
            cancellationToken);
    }

    /// <summary>
    /// Activates an invited account and sets its first password.
    /// </summary>
    /// <remarks>
    /// The activation token is single-use and marks the account active/email-confirmed.
    /// </remarks>
    [HttpPost("activate")]
    public async Task<IActionResult> Activate(ActivateAccountRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateNewPassword(request.Password);
        if (validation is not null) return BadRequest(new { error = validation });

        var userId = await accountTokens.ConsumeToken(request.Token, AccountTokenService.ActivationPurpose, cancellationToken);
        if (userId is null) return BadRequest(new { error = "Activation link is invalid or expired." });

        var user = await database.AppUsers.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return BadRequest(new { error = "Account no longer exists." });

        if (!string.IsNullOrWhiteSpace(request.DisplayName)) user.DisplayName = request.DisplayName.Trim();
        user.PasswordHash = passwords.Hash(request.Password);
        user.IsActive = true;
        user.EmailConfirmed = true;
        await database.SaveChangesAsync(cancellationToken);
        await auditLogs.Write(
            "auth.account.activated",
            "AppUser",
            user.Id.ToString(),
            user.Id,
            null,
            null,
            new { user.Email, user.DisplayName },
            cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Resets an existing account password from a one-time email link.
    /// </summary>
    /// <remarks>
    /// Reset tokens do not change competition assignments or system-admin status.
    /// </remarks>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateNewPassword(request.Password);
        if (validation is not null) return BadRequest(new { error = validation });

        var tokenResult = await accountTokens.ConsumeTokenWithResult(request.Token, AccountTokenService.PasswordResetPurpose, cancellationToken);
        if (tokenResult.UserId is null)
        {
            await auditLogs.Write(
                tokenResult.Failure == AccountTokenService.ConsumeTokenFailure.Expired ? "auth.password_reset.expired" : "auth.password_reset.invalid",
                "AppUserToken",
                null,
                null,
                null,
                null,
                new { Reason = tokenResult.Failure?.ToString() ?? "Unknown" },
                cancellationToken);
            var error = tokenResult.Failure == AccountTokenService.ConsumeTokenFailure.Expired
                ? "This password reset link has expired. Please request a new one."
                : "This password reset link is invalid or has already been used. Please request a new one.";
            return BadRequest(new { error });
        }

        var user = await database.AppUsers.SingleOrDefaultAsync(item => item.Id == tokenResult.UserId, cancellationToken);
        if (user is null) return BadRequest(new { error = "Account no longer exists." });

        user.PasswordHash = passwords.Hash(request.Password);
        user.IsActive = true;
        await database.SaveChangesAsync(cancellationToken);
        await auditLogs.Write(
            "auth.password_reset.completed",
            "AppUser",
            user.Id.ToString(),
            user.Id,
            null,
            null,
            new { user.Email },
            cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Authenticates an AppUser by normalized email and password hash.
    /// </summary>
    /// <remarks>
    /// The account must be active, and the optional Email Confirmed requirement can be enabled by a Global System Admin.
    /// </remarks>
    async Task<ActionResult<LoginResponse>> LoginAccount(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!EmailRules.IsValid(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        var normalizedEmail = EmailRules.Normalize(request.Email);
        var user = await database.AppUsers.SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive || !passwords.Verify(request.Password!, user.PasswordHash))
        {
            await auditLogs.Write(
                "auth.login.failed",
                "AppUser",
                normalizedEmail,
                user?.Id,
                null,
                null,
                new { email = normalizedEmail, reason = user is null ? "not_found" : "invalid_or_inactive" },
                cancellationToken);
            return Unauthorized(new { error = "Invalid email or password." });
        }

        if (await IsEmailConfirmationRequired(cancellationToken) && !user.EmailConfirmed)
        {
            await auditLogs.Write(
                "auth.login.email_unconfirmed",
                "AppUser",
                user.Id.ToString(),
                user.Id,
                null,
                null,
                new { user.Email },
                cancellationToken);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Email confirmation is required before login." });
        }

        user.LastLoginAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);

        var tokenValue = tokens.CreateForUser(user.Id, user.Email, user.DisplayName, user.IsSystemAdmin);
        var access = await CompetitionAccessFor(user.Id, cancellationToken);
        await auditLogs.Write(
            "auth.login.success",
            "AppUser",
            user.Id.ToString(),
            user.Id,
            null,
            null,
            new
            {
                user.Email,
                user.DisplayName,
                user.IsSystemAdmin,
                competitions = access.Select(item => new { item.CompetitionId, item.CompetitionKey, item.Role })
            },
            cancellationToken);

        return Ok(new LoginResponse(
            tokenValue.ToString(),
            null,
            user.DisplayName,
            tokenValue.Session.ExpiresAt,
            user.Id,
            user.Email,
            user.IsSystemAdmin,
            access));
    }

    /// <summary>
    /// Authenticates the legacy competition-key password flow.
    /// </summary>
    /// <remarks>
    /// Keeping this path avoids breaking the current frontend while account login is introduced.
    /// </remarks>
    async Task<ActionResult<LoginResponse>> LoginCompetition(LoginRequest request, CancellationToken cancellationToken)
    {
        var competitionKey = CompetitionKeyRules.Normalize(request.CompetitionId ?? "");
        if (!CompetitionKeyRules.IsValid(competitionKey) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(new { error = "Invalid competition ID or password." });
        }

        var competition = await database.Competitions
            .SingleOrDefaultAsync(item => item.CompetitionKey == competitionKey, cancellationToken);

        if (competition is null || competition.ArchivedAt is not null || competition.Status == "Archived")
        {
            await auditLogs.Write(
                "auth.login.competition_failed",
                "Competition",
                competitionKey,
                null,
                competition?.Id,
                null,
                new { competitionKey, reason = competition is null ? "not_found" : "archived" },
                cancellationToken);
            return Unauthorized(new { error = "Invalid competition ID or password." });
        }

        if (!passwords.Verify(request.Password!, competition.PasswordHash))
        {
            await auditLogs.Write(
                "auth.login.competition_failed",
                "Competition",
                competitionKey,
                null,
                competition.Id,
                null,
                new { competitionKey, reason = "invalid_password" },
                cancellationToken);
            return Unauthorized(new { error = "Invalid competition ID or password." });
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "StackMeet User" : request.DisplayName.Trim();
        var tokenValue = tokens.Create(competitionKey, displayName);
        await auditLogs.Write(
            "auth.login.competition_success",
            "Competition",
            competitionKey,
            null,
            competition.Id,
            null,
            new { competitionKey, displayName },
            cancellationToken);
        return Ok(new LoginResponse(tokenValue.ToString(), competitionKey, displayName, tokenValue.Session.ExpiresAt));
    }

    /// <summary>
    /// Loads active competition assignments for login and current-user responses.
    /// </summary>
    /// <remarks>
    /// Archived competitions are hidden from the account switcher payload.
    /// </remarks>
    async Task<IReadOnlyCollection<CompetitionAccessResponse>> CompetitionAccessFor(int userId, CancellationToken cancellationToken)
    {
        return await database.CompetitionUsers
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.IsActive && item.Competition.Status != "Archived" && item.Competition.ArchivedAt == null)
            .OrderBy(item => item.Competition.CompetitionCode)
            .Select(item => new CompetitionAccessResponse(
                item.CompetitionId,
                item.Competition.CompetitionKey,
                item.Competition.CompetitionName,
                item.Role.Name))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Reads the global email-confirmation login rule.
    /// </summary>
    /// <remarks>
    /// The protected setting defaults to false to avoid locking out existing users during rollout.
    /// </remarks>
    async Task<bool> IsEmailConfirmationRequired(CancellationToken cancellationToken)
    {
        var stored = await settings.Get(RequireEmailConfirmedSettingKey, cancellationToken);
        return string.IsNullOrWhiteSpace(stored) || (bool.TryParse(stored, out var required) && required);
    }

    /// <summary>
    /// Extracts a bearer token from the Authorization header.
    /// </summary>
    /// <remarks>
    /// This mirrors the middleware parser so /api/auth/me can validate sessions directly.
    /// </remarks>
    static string? BearerToken(string? authorization)
    {
        const string prefix = "Bearer ";
        return authorization?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true
            ? authorization[prefix.Length..].Trim()
            : null;
    }

    static string? ValidateNewPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8) return "Password must be at least 8 characters.";
        return null;
    }
}
