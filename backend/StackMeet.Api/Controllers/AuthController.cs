using Microsoft.AspNetCore.Mvc;
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
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokens.SigningKey))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Login security is not configured." });
        }

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
}