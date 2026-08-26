using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthSessionsController(
    StackMeetDbContext database,
    AuditLogService auditLogs) : ControllerBase
{
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        if (HttpContext.Items["StackMeetSession"] is not SessionToken session
            || !session.IsAccountSession
            || session.UserId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Account login session required." });
        }

        var user = await database.AppUsers.SingleOrDefaultAsync(
            item => item.Id == session.UserId.Value && item.IsActive,
            ct);

        if (user is null)
        {
            return Unauthorized(new { error = "Login session is no longer valid. Sign in again." });
        }

        var previousVersion = user.SessionVersion;
        user.SessionVersion++;
        await database.SaveChangesAsync(ct);

        await auditLogs.Write(
            "auth.logout_all.account",
            "AppUser",
            user.Id.ToString(),
            user.Id,
            null,
            new { SessionVersion = previousVersion },
            new { user.SessionVersion },
            ct);

        return NoContent();
    }
}
