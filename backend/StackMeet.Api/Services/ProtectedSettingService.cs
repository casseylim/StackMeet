using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

namespace StackMeet.Api.Services;

/// <summary>
/// Reads and writes admin-managed runtime settings, including encrypted secrets.
/// </summary>
/// <remarks>
/// Encryption depends on Security:SettingsEncryptionKey, which must be configured on each server that needs to decrypt saved secrets.
/// </remarks>
public sealed class ProtectedSettingService(StackMeetDbContext database, IConfiguration configuration)
{
    /// <summary>
    /// Gets a setting value by key.
    /// </summary>
    /// <remarks>
    /// Protected values are decrypted before return; missing values return null.
    /// </remarks>
    public async Task<string?> Get(string key, CancellationToken ct)
    {
        var setting = await database.AppSettings.AsNoTracking().SingleOrDefaultAsync(item => item.Key == key, ct);
        if (setting is null) return null;
        return setting.IsProtected ? Unprotect(setting.Value) : setting.Value;
    }

    /// <summary>
    /// Saves or updates one runtime setting.
    /// </summary>
    /// <remarks>
    /// When protect is true, the value is encrypted before it is written to SQL.
    /// </remarks>
    public async Task Set(string key, string value, bool protect, CancellationToken ct)
    {
        var setting = await database.AppSettings.SingleOrDefaultAsync(item => item.Key == key, ct);
        if (setting is null)
        {
            setting = new AppSetting { Key = key };
            database.AppSettings.Add(setting);
        }

        setting.Value = protect ? Protect(value) : value;
        setting.IsProtected = protect;
        setting.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Returns whether protected setting encryption is configured.
    /// </summary>
    /// <remarks>
    /// Admin SMTP setup should not save passwords until this is true.
    /// </remarks>
    public bool CanProtect => !string.IsNullOrWhiteSpace(EncryptionKey);

    string Protect(string value)
    {
        var key = EncryptionBytes();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(value);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return $"{Convert.ToBase64String(aes.IV)}.{Convert.ToBase64String(cipherBytes)}";
    }

    string Unprotect(string value)
    {
        var parts = value.Split('.', 2);
        if (parts.Length != 2) throw new InvalidOperationException("Protected setting value is malformed.");

        var key = EncryptionBytes();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = Convert.FromBase64String(parts[0]);
        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(parts[1]);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    byte[] EncryptionBytes()
    {
        var key = EncryptionKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Security:SettingsEncryptionKey is required to save or read protected settings.");
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(key));
    }

    string? EncryptionKey => configuration["Security:SettingsEncryptionKey"];
}
