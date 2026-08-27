namespace StackMeet.Api.Services;

/// <summary>
/// Builds public account-management links for activation and password reset emails.
/// </summary>
/// <remarks>
/// Hosted IIS deployments can receive internal HTTP metadata, so this service prefers configured
/// or forwarded public origin details before falling back to the request host.
/// </remarks>
public sealed class AccountLinkService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
{
    /// <summary>
    /// Creates an activation link containing the one-time token.
    /// </summary>
    /// <remarks>
    /// The raw token is placed only in the email URL and is never logged by this service.
    /// </remarks>
    public string ActivationLink(string rawToken) => AccountLink("activate", rawToken);

    /// <summary>
    /// Creates a password-reset link containing the one-time token.
    /// </summary>
    /// <remarks>
    /// Reset links share the same account page as activation links but use a different purpose value.
    /// </remarks>
    public string PasswordResetLink(string rawToken) => AccountLink("reset", rawToken);

    /// <summary>
    /// Builds the browser URL for account.html using the public application origin.
    /// </summary>
    /// <remarks>
    /// Account:PublicBaseUrl wins when configured; otherwise X-Forwarded-* headers and the request host
    /// are used. Non-local HTTP hosts are upgraded to HTTPS for email links.
    /// </remarks>
    string AccountLink(string purpose, string rawToken)
    {
        var origin = PublicOrigin();
        var builder = new UriBuilder(origin)
        {
            Path = "account.html",
            Query = $"purpose={Uri.EscapeDataString(purpose)}&token={Uri.EscapeDataString(rawToken)}"
        };
        return builder.Uri.ToString();
    }

    /// <summary>
    /// Resolves the public origin that recipients should open from their email client.
    /// </summary>
    /// <remarks>
    /// This avoids generating localhost, internal HTTP, or proxy-only URLs when the app is hosted.
    /// </remarks>
    Uri PublicOrigin()
    {
        var configured = configuration["Account:PublicBaseUrl"] ?? configuration["App:PublicBaseUrl"];
        if (Uri.TryCreate(configured, UriKind.Absolute, out var configuredUri))
        {
            return RootUri(configuredUri);
        }

        var request = httpContextAccessor.HttpContext?.Request;
        var forwardedHost = request?.Headers["X-Forwarded-Host"].FirstOrDefault();
        var forwardedProto = request?.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var host = !string.IsNullOrWhiteSpace(forwardedHost) ? forwardedHost : request?.Host.Value;
        var scheme = !string.IsNullOrWhiteSpace(forwardedProto) ? forwardedProto : request?.Scheme;
        if (string.IsNullOrWhiteSpace(host)) host = "localhost";
        if (string.IsNullOrWhiteSpace(scheme)) scheme = "https";
        if (scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && !IsLocalHost(host))
        {
            scheme = "https";
        }

        return RootUri(new Uri($"{scheme}://{host}"));
    }

    /// <summary>
    /// Keeps only scheme, host, and port from a configured or request-derived URI.
    /// </summary>
    /// <remarks>
    /// Account links always target the application root account page, not a caller-specific path.
    /// </remarks>
    static Uri RootUri(Uri uri)
    {
        var builder = new UriBuilder(uri.Scheme, uri.Host, uri.IsDefaultPort ? -1 : uri.Port);
        return builder.Uri;
    }

    /// <summary>
    /// Detects development hosts where HTTP reset links are acceptable.
    /// </summary>
    /// <remarks>
    /// Production host names should use HTTPS even when the upstream request reaches Kestrel over HTTP.
    /// </remarks>
    static bool IsLocalHost(string host)
    {
        var name = host.Split(':', 2)[0];
        return name.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || name.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || name.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}
