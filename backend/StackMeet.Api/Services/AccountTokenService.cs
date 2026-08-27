using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

namespace StackMeet.Api.Services;

/// <summary>
/// Creates and consumes one-time tokens for account activation and password reset.
/// </summary>
/// <remarks>
/// Raw token values are returned once for email links; the database stores only SHA-256 hashes.
/// </remarks>
public sealed class AccountTokenService(StackMeetDbContext database)
{
    public const string ActivationPurpose = "Activation";
    public const string PasswordResetPurpose = "PasswordReset";

    public enum ConsumeTokenFailure
    {
        Missing,
        Invalid,
        Expired,
        Used
    }

    public sealed record ConsumeTokenResult(int? UserId, ConsumeTokenFailure? Failure);

    /// <summary>
    /// Creates a one-time token for a user and purpose.
    /// </summary>
    /// <remarks>
    /// Existing unused tokens for the same user/purpose are marked used so only the newest link remains valid.
    /// </remarks>
    public async Task<string> CreateToken(int userId, string purpose, TimeSpan lifetime, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var existing = await database.AppUserTokens
            .Where(item => item.UserId == userId && item.Purpose == purpose && item.UsedAt == null)
            .ToListAsync(ct);
        foreach (var token in existing) token.UsedAt = now;

        var rawToken = Base64Url(RandomNumberGenerator.GetBytes(32));
        database.AppUserTokens.Add(new AppUserToken
        {
            UserId = userId,
            Purpose = purpose,
            TokenHash = Hash(rawToken),
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime)
        });
        await database.SaveChangesAsync(ct);
        return rawToken;
    }

    /// <summary>
    /// Consumes a valid token and returns the associated user ID.
    /// </summary>
    /// <remarks>
    /// Tokens are single-use and expire based on their stored UTC timestamp.
    /// </remarks>
    public async Task<int?> ConsumeToken(string rawToken, string purpose, CancellationToken ct)
    {
        var result = await ConsumeTokenWithResult(rawToken, purpose, ct);
        return result.UserId;
    }

    /// <summary>
    /// Consumes a valid token and reports why invalid tokens fail.
    /// </summary>
    /// <remarks>
    /// Controllers use the failure reason for user-safe messages and audit entries, never for revealing token material.
    /// </remarks>
    public async Task<ConsumeTokenResult> ConsumeTokenWithResult(string rawToken, string purpose, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return new ConsumeTokenResult(null, ConsumeTokenFailure.Missing);

        var hash = Hash(rawToken);
        var now = DateTime.UtcNow;
        var token = await database.AppUserTokens
            .SingleOrDefaultAsync(item =>
                item.TokenHash == hash
                && item.Purpose == purpose,
                ct);
        if (token is null) return new ConsumeTokenResult(null, ConsumeTokenFailure.Invalid);
        if (token.UsedAt is not null) return new ConsumeTokenResult(null, ConsumeTokenFailure.Used);
        if (token.ExpiresAt <= now) return new ConsumeTokenResult(null, ConsumeTokenFailure.Expired);

        token.UsedAt = now;
        await database.SaveChangesAsync(ct);
        return new ConsumeTokenResult(token.UserId, null);
    }

    /// <summary>
    /// Hashes a raw token before lookup or storage.
    /// </summary>
    /// <remarks>
    /// The raw value is treated like a password-reset secret and should not be logged.
    /// </remarks>
    static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }

    static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
