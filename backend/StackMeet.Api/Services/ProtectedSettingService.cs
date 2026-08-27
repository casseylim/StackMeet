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
/// Protection depends on Security:SettingsEncryptionKey, which must be configured on each server that needs to decrypt saved secrets.
/// New protected values use authenticated AES-GCM; legacy AES-CBC values remain decrypt-only and are upgraded after a successful read.
/// </remarks>
public sealed class ProtectedSettingService(
    StackMeetDbContext database,
    IConfiguration configuration,
    ILogger<ProtectedSettingService> logger)
{
    const string CurrentFormat = "v2";
    const int GcmNonceSize = 12;
    const int GcmTagSize = 16;

    /// <summary>
    /// Gets a setting value by key.
    /// </summary>
    /// <remarks>
    /// Protected values are decrypted before return; missing values return null.
    /// Legacy protected values are transparently upgraded without overwriting a concurrent admin change.
    /// </remarks>
    public async Task<string?> Get(string key, CancellationToken ct)
    {
        var setting = await database.AppSettings.AsNoTracking().SingleOrDefaultAsync(item => item.Key == key, ct);
        if (setting is null) return null;
        if (!setting.IsProtected)
        {
            logger.LogDebug("Runtime setting {SettingKey} loaded as unprotected value.", key);
            return setting.Value;
        }

        try
        {
            var isLegacy = !IsCurrentFormat(setting.Value);
            var value = Unprotect(key, setting.Value);
            logger.LogInformation("Protected runtime setting {SettingKey} decrypted successfully.", key);

            if (isLegacy)
            {
                await TryUpgradeLegacyValue(key, setting, value, ct);
            }

            return value;
        }
        catch (Exception error)
        {
            logger.LogError(error, "Protected runtime setting {SettingKey} decryption failed.", key);
            throw;
        }
    }

    /// <summary>
    /// Checks whether a setting has a stored value without decrypting protected secrets.
    /// </summary>
    /// <remarks>
    /// Admin screens use this to display secret status even if a password value must remain hidden.
    /// </remarks>
    public async Task<bool> HasValue(string key, CancellationToken ct)
    {
        return await database.AppSettings
            .AsNoTracking()
            .AnyAsync(item => item.Key == key && item.Value != "", ct);
    }

    /// <summary>
    /// Saves or updates one runtime setting.
    /// </summary>
    /// <remarks>
    /// When protect is true, the value is authenticated and encrypted before it is written to SQL.
    /// </remarks>
    public async Task Set(string key, string value, bool protect, CancellationToken ct)
    {
        var setting = await database.AppSettings.SingleOrDefaultAsync(item => item.Key == key, ct);
        if (setting is null)
        {
            setting = new AppSetting { Key = key };
            database.AppSettings.Add(setting);
        }

        setting.Value = protect ? Protect(key, value) : value;
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

    string Protect(string settingKey, string value)
    {
        var encryptionKey = EncryptionBytes();
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(GcmNonceSize);
            var plainBytes = Encoding.UTF8.GetBytes(value);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[GcmTagSize];

            using var aes = new AesGcm(encryptionKey, GcmTagSize);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag, AssociatedData(settingKey));

            return string.Join(
                ".",
                CurrentFormat,
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(tag),
                Convert.ToBase64String(cipherBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
        }
    }

    string Unprotect(string settingKey, string value) =>
        IsCurrentFormat(value)
            ? UnprotectCurrent(settingKey, value)
            : UnprotectLegacy(value);

    string UnprotectCurrent(string settingKey, string value)
    {
        var parts = value.Split('.', 4);
        if (parts.Length != 4 || parts[0] != CurrentFormat)
        {
            throw new InvalidOperationException("Protected setting value is malformed.");
        }

        var nonce = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);
        var cipherBytes = Convert.FromBase64String(parts[3]);
        if (nonce.Length != GcmNonceSize || tag.Length != GcmTagSize)
        {
            throw new InvalidOperationException("Protected setting value is malformed.");
        }

        var encryptionKey = EncryptionBytes();
        try
        {
            var plainBytes = new byte[cipherBytes.Length];
            using var aes = new AesGcm(encryptionKey, GcmTagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes, AssociatedData(settingKey));
            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
        }
    }

    string UnprotectLegacy(string value)
    {
        var parts = value.Split('.', 2);
        if (parts.Length != 2) throw new InvalidOperationException("Protected setting value is malformed.");

        var encryptionKey = EncryptionBytes();
        try
        {
            using var aes = Aes.Create();
            aes.Key = encryptionKey;
            aes.IV = Convert.FromBase64String(parts[0]);
            using var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(parts[1]);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
        }
    }

    async Task TryUpgradeLegacyValue(string settingKey, AppSetting setting, string plainValue, CancellationToken ct)
    {
        try
        {
            var upgradedValue = Protect(settingKey, plainValue);
            var updated = await database.AppSettings
                .Where(item => item.Id == setting.Id && item.IsProtected && item.Value == setting.Value)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Value, upgradedValue), ct);

            if (updated == 1)
            {
                logger.LogInformation("Protected runtime setting {SettingKey} upgraded to authenticated encryption.", settingKey);
            }
            else
            {
                logger.LogDebug("Protected runtime setting {SettingKey} upgrade skipped because the stored value changed concurrently.", settingKey);
            }
        }
        catch (Exception error)
        {
            // A legacy value that decrypts successfully must remain usable even if the opportunistic
            // storage upgrade fails. A later read can retry the upgrade.
            logger.LogWarning(error, "Protected runtime setting {SettingKey} legacy encryption upgrade failed; the decrypted value remains usable.", settingKey);
        }
    }

    static bool IsCurrentFormat(string value) => value.StartsWith(CurrentFormat + ".", StringComparison.Ordinal);

    static byte[] AssociatedData(string settingKey) =>
        Encoding.UTF8.GetBytes($"StackMeet.ProtectedSetting.{CurrentFormat}|{settingKey}");

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
