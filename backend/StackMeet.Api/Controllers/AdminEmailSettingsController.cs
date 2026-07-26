using Microsoft.AspNetCore.Mvc;
using StackMeet.Api.Dtos;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

/// <summary>
/// Manages SMTP settings for account invitation and reset emails.
/// </summary>
/// <remarks>
/// These endpoints are admin-key protected by the /api/admin middleware.
/// </remarks>
[ApiController]
[Route("api/admin/email-settings")]
public sealed class AdminEmailSettingsController(
    ProtectedSettingService settings,
    AccountEmailService emails) : ControllerBase
{
    /// <summary>
    /// Reads current SMTP settings without returning the password.
    /// </summary>
    /// <remarks>
    /// The response tells the admin UI whether protected secret storage is configured.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<AdminEmailSettingsResponse>> Get(CancellationToken ct)
    {
        return Ok(new AdminEmailSettingsResponse(
            await settings.Get("Email:FromName", ct) ?? "StackMeet",
            await settings.Get("Email:FromAddress", ct) ?? "",
            await settings.Get("Email:SmtpHost", ct) ?? "",
            int.TryParse(await settings.Get("Email:SmtpPort", ct), out var port) ? port : 587,
            bool.TryParse(await settings.Get("Email:UseTls", ct), out var useTls) ? useTls : true,
            await settings.Get("Email:Username", ct) ?? "",
            !string.IsNullOrWhiteSpace(await settings.Get("Email:Password", ct)),
            settings.CanProtect));
    }

    /// <summary>
    /// Saves SMTP settings for invitation and reset emails.
    /// </summary>
    /// <remarks>
    /// Password is optional on update; when supplied, it is encrypted before storage.
    /// </remarks>
    [HttpPut]
    public async Task<IActionResult> Save(AdminEmailSettingsRequest request, CancellationToken ct)
    {
        var validation = Validate(request);
        if (validation is not null) return BadRequest(new { error = validation });
        if (!string.IsNullOrWhiteSpace(request.Password) && !settings.CanProtect)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Security:SettingsEncryptionKey is required before saving SMTP password." });
        }

        await settings.Set("Email:FromName", request.FromName.Trim(), false, ct);
        await settings.Set("Email:FromAddress", request.FromAddress.Trim(), false, ct);
        await settings.Set("Email:SmtpHost", request.SmtpHost.Trim(), false, ct);
        await settings.Set("Email:SmtpPort", request.SmtpPort.ToString(), false, ct);
        await settings.Set("Email:UseTls", request.UseTls.ToString(), false, ct);
        await settings.Set("Email:Username", request.Username.Trim(), false, ct);
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            await settings.Set("Email:Password", request.Password, true, ct);
        }

        return NoContent();
    }

    /// <summary>
    /// Sends a test email through the configured SMTP settings.
    /// </summary>
    /// <remarks>
    /// Use this after saving Brevo settings before inviting real users.
    /// </remarks>
    [HttpPost("test")]
    public async Task<IActionResult> Test(AdminTestEmailRequest request, CancellationToken ct)
    {
        if (!EmailRules.IsValid(request.ToEmail)) return BadRequest(new { error = "Valid recipient email is required." });
        await emails.SendPasswordResetEmail(request.ToEmail, "StackMeet Admin", $"{Request.Scheme}://{Request.Host}/account.html", ct);
        return Ok(new { message = "Test email sent." });
    }

    static string? Validate(AdminEmailSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FromName)) return "From name is required.";
        if (!EmailRules.IsValid(request.FromAddress)) return "Valid from email is required.";
        if (string.IsNullOrWhiteSpace(request.SmtpHost)) return "SMTP host is required.";
        if (request.SmtpPort is < 1 or > 65535) return "SMTP port is invalid.";
        if (string.IsNullOrWhiteSpace(request.Username)) return "SMTP username is required.";
        return null;
    }
}
