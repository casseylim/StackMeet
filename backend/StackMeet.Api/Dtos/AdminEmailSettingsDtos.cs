namespace StackMeet.Api.Dtos;

/// <summary>
/// Email delivery settings editable by a system admin.
/// </summary>
/// <remarks>
/// SMTP password and Brevo API key are accepted on save but are never returned by GET.
/// </remarks>
public sealed record AdminEmailSettingsRequest(
    string Provider,
    string FromName,
    string FromAddress,
    string SmtpHost,
    int SmtpPort,
    bool UseTls,
    string Username,
    string? Password,
    string? BrevoApiKey);

/// <summary>
/// Email delivery settings status returned to the admin UI.
/// </summary>
/// <remarks>
/// Secret status flags confirm whether protected credentials are already configured.
/// </remarks>
public sealed record AdminEmailSettingsResponse(
    string Provider,
    string FromName,
    string FromAddress,
    string SmtpHost,
    int SmtpPort,
    bool UseTls,
    string Username,
    bool HasPassword,
    bool HasBrevoApiKey,
    bool CanStoreProtectedSecrets);

/// <summary>
/// Request for sending a test transactional email.
/// </summary>
/// <remarks>
/// This verifies the configured Brevo credentials without creating a user account.
/// </remarks>
public sealed record AdminTestEmailRequest(string ToEmail);
