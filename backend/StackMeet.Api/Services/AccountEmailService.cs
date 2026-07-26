using System.Net;
using System.Net.Mail;

namespace StackMeet.Api.Services;

/// <summary>
/// Sends account activation and password-reset emails through configured SMTP.
/// </summary>
/// <remarks>
/// Brevo settings should be supplied by user secrets, environment variables or hosting configuration,
/// never committed into appsettings.json.
/// </remarks>
public sealed class AccountEmailService(IConfiguration configuration, ProtectedSettingService protectedSettings)
{
    /// <summary>
    /// Sends an activation email containing a password setup link.
    /// </summary>
    /// <remarks>
    /// The link should point to /account.html with purpose=activate and the raw one-time token.
    /// </remarks>
    public Task SendActivationEmail(string toEmail, string displayName, string activationLink, CancellationToken ct)
    {
        var subject = "Activate your StackMeet account";
        var body = $"""
        Hello {displayName},

        Your StackMeet account has been created.

        Set your password here:
        {activationLink}

        This link is temporary. If it expires, ask a StackMeet system admin to send a new activation or reset link.
        """;
        return Send(toEmail, subject, body, ct);
    }

    /// <summary>
    /// Sends a password-reset email containing a reset link.
    /// </summary>
    /// <remarks>
    /// The link should point to /account.html with purpose=reset and the raw one-time token.
    /// </remarks>
    public Task SendPasswordResetEmail(string toEmail, string displayName, string resetLink, CancellationToken ct)
    {
        var subject = "Reset your StackMeet password";
        var body = $"""
        Hello {displayName},

        A StackMeet password reset was requested for your account.

        Reset your password here:
        {resetLink}

        If you did not expect this, contact your StackMeet system admin.
        """;
        return Send(toEmail, subject, body, ct);
    }

    /// <summary>
    /// Sends a plain-text email using the configured SMTP account.
    /// </summary>
    /// <remarks>
    /// System.Net.Mail is used to avoid adding external packages while testing the SMTP workflow.
    /// </remarks>
    async Task Send(string toEmail, string subject, string body, CancellationToken ct)
    {
        var settingsValue = await ReadSettings(ct);
        using var message = new MailMessage
        {
            From = new MailAddress(settingsValue.FromAddress, settingsValue.FromName),
            Subject = subject,
            Body = body
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(settingsValue.Host, settingsValue.Port)
        {
            EnableSsl = settingsValue.UseTls,
            Credentials = new NetworkCredential(settingsValue.Username, settingsValue.Password)
        };
        await client.SendMailAsync(message, ct);
    }

    /// <summary>
    /// Reads and validates SMTP configuration from application configuration.
    /// </summary>
    /// <remarks>
    /// Missing configuration throws a clear operational error returned by admin endpoints.
    /// </remarks>
    async Task<EmailSettings> ReadSettings(CancellationToken ct)
    {
        var section = configuration.GetSection("Email");
        var settings = new EmailSettings(
            await protectedSettings.Get("Email:FromName", ct) ?? section["FromName"] ?? "StackMeet",
            await protectedSettings.Get("Email:FromAddress", ct) ?? section["FromAddress"] ?? "",
            await protectedSettings.Get("Email:SmtpHost", ct) ?? section["SmtpHost"] ?? "",
            int.TryParse(await protectedSettings.Get("Email:SmtpPort", ct), out var port) ? port : section.GetValue("SmtpPort", 587),
            bool.TryParse(await protectedSettings.Get("Email:UseTls", ct), out var useTls) ? useTls : section.GetValue("UseTls", true),
            await protectedSettings.Get("Email:Username", ct) ?? section["Username"] ?? "",
            await protectedSettings.Get("Email:Password", ct) ?? section["Password"] ?? "");

        if (string.IsNullOrWhiteSpace(settings.FromAddress)
            || string.IsNullOrWhiteSpace(settings.Host)
            || string.IsNullOrWhiteSpace(settings.Username)
            || string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new InvalidOperationException("Email SMTP configuration is incomplete.");
        }

        return settings;
    }

    sealed record EmailSettings(
        string FromName,
        string FromAddress,
        string Host,
        int Port,
        bool UseTls,
        string Username,
        string Password);
}
