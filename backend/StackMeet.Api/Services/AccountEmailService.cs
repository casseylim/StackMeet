using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StackMeet.Api.Services;

/// <summary>
/// Sends account activation and password-reset emails through configured Brevo delivery.
/// </summary>
/// <remarks>
/// The preferred provider is Brevo's transactional email API; SMTP relay remains available as a fallback.
/// </remarks>
public sealed class AccountEmailService(
    HttpClient http,
    IConfiguration configuration,
    ProtectedSettingService protectedSettings)
{
    /// <summary>
    /// Sends an activation email containing a password setup link.
    /// </summary>
    /// <remarks>
    /// The link should point to /account.html with purpose=activate and the raw one-time token.
    /// </remarks>
    public Task SendActivationEmail(string toEmail, string displayName, string activationLink, CancellationToken ct)
    {
        var subject = "Activate your NADITrack account";
        var body = $"""
        Hello {displayName},

        Your NADITrack account has been created.

        Set your password here:
        {activationLink}

        This link is temporary. If it expires, ask a NADITrack system admin to send a new activation or reset link.
        """;
        return Send(toEmail, subject, body, ct: ct);
    }

    /// <summary>
    /// Sends a password-reset email containing a reset link.
    /// </summary>
    /// <remarks>
    /// The link should point to /account.html with purpose=reset and the raw one-time token.
    /// </remarks>
    public Task SendPasswordResetEmail(string toEmail, string displayName, string resetLink, CancellationToken ct)
    {
        var subject = "Reset your NADITrack password";
        var body = $"""
        Hello {displayName},

        A NADITrack password reset was requested for your account.

        Reset your password here:
        {resetLink}

        If you did not expect this, contact your NADITrack system admin.
        """;
        return Send(toEmail, subject, body, ct: ct);
    }

    /// <summary>Emails a generated CSV activity report as an attachment.</summary>
    /// <remarks>The report contents are supplied by the scheduled audit-report worker.</remarks>
    public Task SendAuditReportEmail(string toEmail, DateOnly reportDate, byte[] csv, CancellationToken ct)
    {
        var subject = $"NADITrack user activity report - {reportDate:yyyy-MM-dd}";
        var body = $"Attached is the NADITrack user activity report for {reportDate:dd/MM/yyyy} (MYT).";
        return Send(toEmail, subject, body, "NADITrack-user-activity-" + reportDate.ToString("yyyy-MM-dd") + ".csv", csv, ct);
    }

    /// <summary>
    /// Sends a plain-text account email using the selected delivery provider.
    /// </summary>
    /// <remarks>
    /// Brevo API is selected by "BrevoApi"; any other provider value uses the legacy SMTP relay.
    /// </remarks>
    async Task Send(string toEmail, string subject, string body, string? attachmentName = null, byte[]? attachment = null, CancellationToken ct = default)
    {
        var settingsValue = await ReadSettings(ct);
        if (settingsValue.Provider.Equals(EmailProvider.BrevoApi, StringComparison.OrdinalIgnoreCase))
        {
            await SendBrevoApi(settingsValue, toEmail, subject, body, attachmentName, attachment, ct);
            return;
        }

        await SendSmtp(settingsValue, toEmail, subject, body, attachmentName, attachment, ct);
    }

    /// <summary>
    /// Sends one transactional email through Brevo's HTTP API.
    /// </summary>
    /// <remarks>
    /// The API key is sent only in the header and is never included in error messages or audit snapshots.
    /// </remarks>
    async Task SendBrevoApi(EmailSettings settingsValue, string toEmail, string subject, string body, string? attachmentName, byte[]? attachment, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Add("api-key", settingsValue.BrevoApiKey);
        var attachments = attachmentName is not null && attachment is not null
            ? [new BrevoAttachment(attachmentName, Convert.ToBase64String(attachment))]
            : Array.Empty<BrevoAttachment>();
        request.Content = JsonContent.Create(new BrevoEmailRequest(
            new BrevoSender(settingsValue.FromName, settingsValue.FromAddress),
            [new BrevoRecipient(toEmail, toEmail)],
            subject,
            body,
            attachments));

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Brevo API rejected the email with HTTP {(int)response.StatusCode}: {details}");
        }
    }

    /// <summary>
    /// Sends one transactional email through the legacy Brevo SMTP relay.
    /// </summary>
    /// <remarks>
    /// This preserves existing saved SMTP settings while admins transition to the Brevo API key flow.
    /// </remarks>
    async Task SendSmtp(EmailSettings settingsValue, string toEmail, string subject, string body, string? attachmentName, byte[]? attachment, CancellationToken ct)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(settingsValue.FromAddress, settingsValue.FromName),
            Subject = subject,
            Body = body
        };
        message.To.Add(toEmail);
        if (attachmentName is not null && attachment is not null)
        {
            message.Attachments.Add(new Attachment(new MemoryStream(attachment), attachmentName, "text/csv"));
        }

        using var client = new SmtpClient(settingsValue.Host, settingsValue.Port)
        {
            EnableSsl = settingsValue.UseTls,
            Credentials = new NetworkCredential(settingsValue.Username, settingsValue.Password)
        };
        await client.SendMailAsync(message, ct);
    }

    /// <summary>
    /// Reads and validates email delivery configuration from protected settings.
    /// </summary>
    /// <remarks>
    /// Missing provider-specific secrets throw a clear operational error returned by admin endpoints.
    /// </remarks>
    async Task<EmailSettings> ReadSettings(CancellationToken ct)
    {
        var section = configuration.GetSection("Email");
        var brevoApiKey = await protectedSettings.Get("Email:BrevoApiKey", ct) ?? section["BrevoApiKey"] ?? "";
        var provider = await protectedSettings.Get("Email:Provider", ct)
            ?? section["Provider"]
            ?? (string.IsNullOrWhiteSpace(brevoApiKey) ? EmailProvider.Smtp : EmailProvider.BrevoApi);
        var isBrevoApi = provider.Equals(EmailProvider.BrevoApi, StringComparison.OrdinalIgnoreCase);
        var smtpPassword = isBrevoApi
            ? ""
            : await protectedSettings.Get("Email:Password", ct) ?? section["Password"] ?? "";
        var settings = new EmailSettings(
            provider,
            await protectedSettings.Get("Email:FromName", ct) ?? section["FromName"] ?? "StackMeet",
            await protectedSettings.Get("Email:FromAddress", ct) ?? section["FromAddress"] ?? "",
            await protectedSettings.Get("Email:SmtpHost", ct) ?? section["SmtpHost"] ?? "",
            int.TryParse(await protectedSettings.Get("Email:SmtpPort", ct), out var port) ? port : section.GetValue("SmtpPort", 587),
            bool.TryParse(await protectedSettings.Get("Email:UseTls", ct), out var useTls) ? useTls : section.GetValue("UseTls", true),
            await protectedSettings.Get("Email:Username", ct) ?? section["Username"] ?? "",
            smtpPassword,
            brevoApiKey);

        if (string.IsNullOrWhiteSpace(settings.FromAddress))
        {
            throw new InvalidOperationException("Email sender address is not configured.");
        }

        if (isBrevoApi)
        {
            if (string.IsNullOrWhiteSpace(settings.BrevoApiKey))
            {
                throw new InvalidOperationException("Brevo API key is not configured.");
            }
        }
        else if (string.IsNullOrWhiteSpace(settings.Host)
            || string.IsNullOrWhiteSpace(settings.Username)
            || string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new InvalidOperationException("Email SMTP configuration is incomplete.");
        }

        return settings;
    }

    public static class EmailProvider
    {
        public const string BrevoApi = "BrevoApi";
        public const string Smtp = "Smtp";
    }

    sealed record EmailSettings(
        string Provider,
        string FromName,
        string FromAddress,
        string Host,
        int Port,
        bool UseTls,
        string Username,
        string Password,
        string BrevoApiKey);

    sealed record BrevoSender(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("email")] string Email);

    sealed record BrevoRecipient(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string Name);

    sealed record BrevoEmailRequest(
        [property: JsonPropertyName("sender")] BrevoSender Sender,
        [property: JsonPropertyName("to")] IReadOnlyCollection<BrevoRecipient> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("textContent")] string TextContent,
        [property: JsonPropertyName("attachment")] IReadOnlyCollection<BrevoAttachment> Attachment);

    sealed record BrevoAttachment(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("content")] string Content);
}
