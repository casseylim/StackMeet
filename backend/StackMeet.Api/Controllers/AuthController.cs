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
    SessionTokenService tokens) : ControllerBase
{
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
    public IActionResult Logout() => NoContent();

    /// <summary>
    /// Authenticates an AppUser by normalized email and password hash.
    /// </summary>
    /// <remarks>
    /// The account must be active; email confirmation is stored but not enforced until Phase 2.
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
            return Unauthorized(new { error = "Invalid email or password." });
        }

        user.LastLoginAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);

        var tokenValue = tokens.CreateForUser(user.Id, user.Email, user.DisplayName, user.IsSystemAdmin);
        return Ok(new LoginResponse(
            tokenValue.ToString(),
            null,
            user.DisplayName,
            tokenValue.Session.ExpiresAt,
            user.Id,
            user.Email,
            user.IsSystemAdmin,
            await CompetitionAccessFor(user.Id, cancellationToken)));
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
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CompetitionKey == competitionKey, cancellationToken);

        if (competition is null || competition.ArchivedAt is not null || competition.Status == "Archived")
        {
            return Unauthorized(new { error = "Invalid competition ID or password." });
        }

        if (!passwords.Verify(request.Password!, competition.PasswordHash))
        {
            return Unauthorized(new { error = "Invalid competition ID or password." });
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "StackMeet User" : request.DisplayName.Trim();
        var tokenValue = tokens.Create(competitionKey, displayName);
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
}
