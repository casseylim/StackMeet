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
    ProtectedSettingService protectedSettings,
    ILogger<AccountEmailService> logger,
    IHostEnvironment environment)
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

    /// <summary>Sends the reset link and explains that a permanent lockout requires password reset.</summary>
    /// <remarks>Resetting the password automatically unlocks the account.</remarks>
    public Task SendAccountLockedEmail(string toEmail, string displayName, string resetLink, CancellationToken ct)
    {
        var subject = "NADITrack account locked - password reset required";
        var body = $"""
        Hello {displayName},

        Your NADITrack account has been locked after three rounds of failed password attempts.
        Please reset your password using the link below. Your account will be unlocked after the password is successfully changed.

        Reset your password here:
        {resetLink}

        This link expires in 60 minutes. If you did not make these attempts, contact your NADITrack system admin.
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
        await WriteDebugLog("[Start] Account email send requested.");
        EmailSettings settingsValue;
        try
        {
            settingsValue = await ReadSettings(ct);
            await WriteDebugLog($"Settings loaded = OK; Provider = {settingsValue.Provider}; Brevo key exists = {!string.IsNullOrWhiteSpace(settingsValue.BrevoApiKey)}.");
        }
        catch (Exception error)
        {
            await WriteDebugLog($"Configuration/decryption exception: {error}");
            logger.LogError(error, "Email delivery configuration or protected-setting decryption failed.");
            throw;
        }
        logger.LogInformation(
            "Account email delivery selected provider {Provider}; protected Brevo API key exists: {HasBrevoApiKey}.",
            settingsValue.Provider,
            !string.IsNullOrWhiteSpace(settingsValue.BrevoApiKey));
        if (settingsValue.Provider.Equals(EmailProvider.BrevoApi, StringComparison.OrdinalIgnoreCase))
        {
            await SendBrevoApi(settingsValue, toEmail, subject, body, attachmentName, attachment, ct);
            return;
        }

        await WriteDebugLog("SMTP provider selected; beginning SMTP send.");
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
        await WriteDebugLog("Creating Brevo HTTP request.");
        logger.LogInformation("Beginning outbound Brevo transactional email request for recipient {Recipient}.", RedactEmail(toEmail));
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

        try
        {
            await WriteDebugLog("Sending Brevo HTTP request.");
            using var response = await http.SendAsync(request, ct);
            var details = await response.Content.ReadAsStringAsync(ct);
            await WriteDebugLog($"Brevo HTTP status = {(int)response.StatusCode}; response = {SanitizeResponse(details)}");
            logger.LogInformation(
                "Brevo transactional email response status {StatusCode}; response: {Response}",
                (int)response.StatusCode,
                SanitizeResponse(details));
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Brevo API rejected the email with HTTP {(int)response.StatusCode}: {SanitizeResponse(details)}");
            }
        }
        catch (HttpRequestException error)
        {
            await WriteDebugLog($"DNS/network/TLS exception: {error}");
            logger.LogError(error, "Brevo transactional email request failed at the DNS, network, or TLS layer.");
            throw;
        }
        catch (TaskCanceledException error) when (!ct.IsCancellationRequested)
        {
            await WriteDebugLog($"Timeout exception: {error}");
            logger.LogError(error, "Brevo transactional email request timed out.");
            throw new TimeoutException("Brevo email request timed out.", error);
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

        logger.LogInformation(
            "Email configuration loaded with provider {Provider}; protected Brevo API key exists: {HasBrevoApiKey}; sender configured: {HasSender}.",
            settings.Provider,
            !string.IsNullOrWhiteSpace(settings.BrevoApiKey),
            !string.IsNullOrWhiteSpace(settings.FromAddress));

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

    static string RedactEmail(string email) => string.IsNullOrWhiteSpace(email) ? "<empty>" : email.Contains('@') ? $"<redacted>@{email[(email.IndexOf('@') + 1)..]}" : "<redacted>";

    static string SanitizeResponse(string response)
    {
        var compact = string.Join(' ', response.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 500 ? compact : compact[..500] + "...";
    }

    // Writes temporary provider diagnostics for shared hosting where ASP.NET logs are unavailable.
    async Task WriteDebugLog(string message)
    {
        try
        {
            var directory = Path.Combine(environment.ContentRootPath, "App_Data", "logs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "email-debug.log");
            await File.AppendAllTextAsync(path, $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Unable to write temporary email diagnostics.");
        }
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
