namespace StackMeet.Api.Dtos;

/// <summary>
/// SMTP settings editable by a system admin.
/// </summary>
/// <remarks>
/// Password is accepted on save but is never returned by GET.
/// </remarks>
public sealed record AdminEmailSettingsRequest(
    string FromName,
    string FromAddress,
    string SmtpHost,
    int SmtpPort,
    bool UseTls,
    string Username,
    string? Password);

/// <summary>
/// SMTP settings status returned to the admin UI.
/// </summary>
/// <remarks>
/// HasPassword confirms whether a protected SMTP secret is already configured.
/// </remarks>
public sealed record AdminEmailSettingsResponse(
    string FromName,
    string FromAddress,
    string SmtpHost,
    int SmtpPort,
    bool UseTls,
    string Username,
    bool HasPassword,
    bool CanStoreProtectedSecrets);

/// <summary>
/// Request for sending a test SMTP email.
/// </summary>
/// <remarks>
/// This verifies the configured Brevo credentials without creating a user account.
/// </remarks>
public sealed record AdminTestEmailRequest(string ToEmail);
