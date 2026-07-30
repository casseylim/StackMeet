using Microsoft.AspNetCore.Mvc;
using StackMeet.Api.Dtos;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

/// <summary>
/// Manages email delivery settings for account invitation and reset emails.
/// </summary>
/// <remarks>
/// These endpoints are admin-key protected by the /api/admin middleware.
/// </remarks>
[ApiController]
[Route("api/admin/email-settings")]
public sealed class AdminEmailSettingsController(
    ProtectedSettingService settings,
    AccountEmailService emails,
    AccountLinkService accountLinks,
    AuditLogService auditLogs) : ControllerBase
{
    /// <summary>
    /// Reads current email settings without returning stored secrets.
    /// </summary>
    /// <remarks>
    /// The response tells the admin UI whether protected secret storage is configured.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<AdminEmailSettingsResponse>> Get(CancellationToken ct)
    {
        var hasBrevoApiKey = await settings.HasValue("Email:BrevoApiKey", ct);
        return Ok(new AdminEmailSettingsResponse(
            await settings.Get("Email:Provider", ct) ?? (hasBrevoApiKey ? AccountEmailService.EmailProvider.BrevoApi : AccountEmailService.EmailProvider.Smtp),
            await settings.Get("Email:FromName", ct) ?? "NADITrack",
            await settings.Get("Email:FromAddress", ct) ?? "",
            await settings.Get("Email:SmtpHost", ct) ?? "",
            int.TryParse(await settings.Get("Email:SmtpPort", ct), out var port) ? port : 587,
            bool.TryParse(await settings.Get("Email:UseTls", ct), out var useTls) ? useTls : true,
            await settings.Get("Email:Username", ct) ?? "",
            await settings.HasValue("Email:Password", ct),
            hasBrevoApiKey,
            settings.CanProtect));
    }

    /// <summary>
    /// Saves email delivery settings for invitation and reset emails.
    /// </summary>
    /// <remarks>
    /// SMTP password and Brevo API key are optional on update; when supplied, they are encrypted before storage.
    /// </remarks>
    [HttpPut]
    public async Task<IActionResult> Save(AdminEmailSettingsRequest request, CancellationToken ct)
    {
        var validation = await Validate(request, ct);
        if (validation is not null) return BadRequest(new { error = validation });
        if ((!string.IsNullOrWhiteSpace(request.Password) || !string.IsNullOrWhiteSpace(request.BrevoApiKey)) && !settings.CanProtect)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Security:SettingsEncryptionKey is required before saving email secrets." });
        }

        var before = await SafeSettingsSnapshot(ct);
        await settings.Set("Email:Provider", NormalizeProvider(request.Provider), false, ct);
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
        if (!string.IsNullOrWhiteSpace(request.BrevoApiKey))
        {
            await settings.Set("Email:BrevoApiKey", request.BrevoApiKey.Trim(), true, ct);
        }

        await auditLogs.Write(
            "admin.email_settings.updated",
            "AppSetting",
            "Email",
            auditLogs.CurrentSession()?.UserId,
            null,
            before,
            await SafeSettingsSnapshot(ct) with
            {
                PasswordUpdated = !string.IsNullOrWhiteSpace(request.Password),
                BrevoApiKeyUpdated = !string.IsNullOrWhiteSpace(request.BrevoApiKey)
            },
            ct);
        return NoContent();
    }

    /// <summary>
    /// Sends a test email through the configured delivery provider.
    /// </summary>
    /// <remarks>
    /// Use this after saving Brevo settings before inviting real users.
    /// </remarks>
    [HttpPost("test")]
    public async Task<IActionResult> Test(AdminTestEmailRequest request, CancellationToken ct)
    {
        if (!EmailRules.IsValid(request.ToEmail)) return BadRequest(new { error = "Valid recipient email is required." });
        try
        {
            await emails.SendPasswordResetEmail(request.ToEmail, "NADITrack Admin", accountLinks.PasswordResetLink("test-token"), ct);
        }
        catch (Exception error)
        {
            return EmailSendFailure(error);
        }
        await auditLogs.Write(
            "admin.email_settings.test_sent",
            "AppSetting",
            "Email",
            auditLogs.CurrentSession()?.UserId,
            null,
            null,
            new { request.ToEmail },
            ct);
        return Ok(new { message = "Test email sent." });
    }

    /// <summary>
    /// Reads email settings in a form that is safe to store in audit details.
    /// </summary>
    /// <remarks>
    /// The password value is never returned; only HasPassword and PasswordUpdated are logged.
    /// </remarks>
    async Task<EmailSettingsAuditSnapshot> SafeSettingsSnapshot(CancellationToken ct) => new(
        await settings.Get("Email:Provider", ct) ?? AccountEmailService.EmailProvider.BrevoApi,
        await settings.Get("Email:FromName", ct) ?? "",
        await settings.Get("Email:FromAddress", ct) ?? "",
        await settings.Get("Email:SmtpHost", ct) ?? "",
        int.TryParse(await settings.Get("Email:SmtpPort", ct), out var port) ? port : 587,
        bool.TryParse(await settings.Get("Email:UseTls", ct), out var useTls) ? useTls : true,
        await settings.Get("Email:Username", ct) ?? "",
        await settings.HasValue("Email:Password", ct),
        await settings.HasValue("Email:BrevoApiKey", ct),
        false,
        false);

    /// <summary>
    /// Returns an admin-visible error when the test send fails.
    /// </summary>
    /// <remarks>
    /// This keeps production troubleshooting on-screen without exposing the SMTP password.
    /// </remarks>
    ObjectResult EmailSendFailure(Exception error)
    {
        return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Email could not be sent: {error.Message}" });
    }

    async Task<string?> Validate(AdminEmailSettingsRequest request, CancellationToken ct)
    {
        var provider = NormalizeProvider(request.Provider);
        if (string.IsNullOrWhiteSpace(request.FromName)) return "From name is required.";
        if (!EmailRules.IsValid(request.FromAddress)) return "Valid from email is required.";
        if (provider == AccountEmailService.EmailProvider.BrevoApi)
        {
            if (string.IsNullOrWhiteSpace(request.BrevoApiKey) && !await settings.HasValue("Email:BrevoApiKey", ct))
            {
                return "Brevo API key is required.";
            }
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.SmtpHost)) return "SMTP host is required.";
        if (request.SmtpPort is < 1 or > 65535) return "SMTP port is invalid.";
        if (string.IsNullOrWhiteSpace(request.Username)) return "SMTP username is required.";
        if (string.IsNullOrWhiteSpace(request.Password) && !await settings.HasValue("Email:Password", ct)) return "SMTP key / password is required.";
        return null;
    }

    static string NormalizeProvider(string? provider) =>
        string.Equals(provider, AccountEmailService.EmailProvider.Smtp, StringComparison.OrdinalIgnoreCase)
            ? AccountEmailService.EmailProvider.Smtp
            : AccountEmailService.EmailProvider.BrevoApi;

    sealed record EmailSettingsAuditSnapshot(
        string Provider,
        string FromName,
        string FromAddress,
        string SmtpHost,
        int SmtpPort,
        bool UseTls,
        string Username,
        bool HasPassword,
        bool HasBrevoApiKey,
        bool BrevoApiKeyUpdated,
        bool PasswordUpdated);
}
