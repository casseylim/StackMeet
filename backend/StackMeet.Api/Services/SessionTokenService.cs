using System.Security.Cryptography;
using System.Text;

namespace StackMeet.Api.Services;

public sealed class SessionTokenService(IConfiguration configuration)
{
    const string AccountPayloadVersion = "v3";

    /// <summary>
    /// Creates a legacy competition-scoped session token.
    /// </summary>
    /// <remarks>
    /// This remains available while the frontend migrates from competition password login.
    /// </remarks>
    public SessionTokenValue Create(string competitionId, string displayName)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(SessionMinutes());
        var payload = string.Join("|", Escape(competitionId), Escape(displayName), expiresAt.ToUnixTimeSeconds());
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = Sign(payloadBytes);
        return new SessionTokenValue(
            Base64Url(payloadBytes),
            Base64Url(signature),
            new SessionToken(competitionId, displayName, expiresAt)
        );
    }

    /// <summary>
    /// Creates an account-scoped session token for email/password login.
    /// </summary>
    /// <remarks>
    /// The token stores identity metadata but permissions are still checked against the database.
    /// </remarks>
    public SessionTokenValue CreateForUser(
        int userId,
        string email,
        string displayName,
        bool isSystemAdmin,
        long sessionVersion)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(SessionMinutes());
        var payload = string.Join(
            "|",
            AccountPayloadVersion,
            userId,
            Escape(email),
            Escape(displayName),
            isSystemAdmin ? "1" : "0",
            sessionVersion,
            expiresAt.ToUnixTimeSeconds());
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = Sign(payloadBytes);
        return new SessionTokenValue(
            Base64Url(payloadBytes),
            Base64Url(signature),
            new SessionToken("", displayName, expiresAt, userId, email, isSystemAdmin, sessionVersion)
        );
    }

    /// <summary>
    /// Validates either a legacy competition token or a current account token.
    /// </summary>
    /// <remarks>
    /// Account sessions use the v3 payload with a database-backed session version;
    /// legacy competition tokens keep their existing three-field payload.
    /// </remarks>
    public bool TryValidate(string? token, out SessionToken session)
    {
        session = new SessionToken("", "", DateTimeOffset.MinValue);
        if (string.IsNullOrWhiteSpace(SigningKey)) return false;
        var parts = token?.Split('.', 2);
        if (parts is not { Length: 2 }) return false;

        byte[] payloadBytes;
        byte[] suppliedSignature;
        try
        {
            payloadBytes = FromBase64Url(parts[0]);
            suppliedSignature = FromBase64Url(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedSignature = Sign(payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature)) return false;

        var payload = Encoding.UTF8.GetString(payloadBytes).Split('|');
        if (payload.Length == 7 && payload[0] == AccountPayloadVersion)
        {
            if (!int.TryParse(payload[1], out var userId)
                || !long.TryParse(payload[5], out var sessionVersion)
                || sessionVersion < 1
                || !long.TryParse(payload[6], out var accountExpiresUnix))
            {
                return false;
            }

            var accountExpiresAt = DateTimeOffset.FromUnixTimeSeconds(accountExpiresUnix);
            if (accountExpiresAt <= DateTimeOffset.UtcNow) return false;

            session = new SessionToken(
                "",
                Unescape(payload[3]),
                accountExpiresAt,
                userId,
                Unescape(payload[2]),
                payload[4] == "1",
                sessionVersion);
            return !string.IsNullOrWhiteSpace(session.Email);
        }

        if (payload.Length != 3 || !long.TryParse(payload[2], out var expiresUnix)) return false;

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresUnix);
        if (expiresAt <= DateTimeOffset.UtcNow) return false;

        session = new SessionToken(Unescape(payload[0]), Unescape(payload[1]), expiresAt);
        return !string.IsNullOrWhiteSpace(session.CompetitionId);
    }

    public string? SigningKey => configuration["Security:SessionSigningKey"];

    int SessionMinutes()
    {
        var configured = configuration.GetValue<int?>("Security:SessionMinutes");
        return configured is >= 5 and <= 1440 ? configured.Value : 480;
    }

    byte[] Sign(byte[] payloadBytes)
    {
        var signingKey = SigningKey;
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException("Session signing key is not configured.");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        return hmac.ComputeHash(payloadBytes);
    }

    static string Escape(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    static string Unescape(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));

    static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    public sealed record SessionTokenValue(string Payload, string Signature, SessionToken Session)
    {
        public override string ToString() => $"{Payload}.{Signature}";
    }
}
