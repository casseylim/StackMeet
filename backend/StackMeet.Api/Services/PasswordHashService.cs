using System.Security.Cryptography;

namespace StackMeet.Api.Services;

public sealed class PasswordHashService
{
    const int SaltSize = 16;
    const int KeySize = 32;
    const int Iterations = 210_000;
    // Bound verification work independently from the current generation policy.
    const int MaxVerificationIterations = 1_000_000;
    const int MaxStoredHashLength = 500;

    public string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password is required.", nameof(password));
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"PBKDF2-SHA256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string? hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash) || hash.Length > MaxStoredHashLength) return false;
        var parts = hash.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2-SHA256"
            || !int.TryParse(parts[1], out var iterations)
            || iterations < 1 || iterations > MaxVerificationIterations) return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        // Hash() has always emitted these lengths in the reviewed implementation.
        if (salt.Length != SaltSize || expected.Length != KeySize) return false;
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}