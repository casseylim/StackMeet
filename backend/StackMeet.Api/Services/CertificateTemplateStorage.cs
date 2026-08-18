using System.Security.Cryptography;

namespace StackMeet.Api.Services;

public sealed class CertificateTemplateStorage(IConfiguration configuration, IWebHostEnvironment environment, ILogger<CertificateTemplateStorage> logger)
{
    public string RootPath
    {
        get
        {
            var configured = configuration["CertificateTemplatesPath"];
            if (string.IsNullOrWhiteSpace(configured)) return Path.Combine(environment.ContentRootPath, "certificate-templates");
            return Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured);
        }
    }

    public string CompetitionPath(int competitionId) => SafePath(competitionId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public string FullPath(int competitionId, string storedFileName) => SafePath(Path.Combine(competitionId.ToString(System.Globalization.CultureInfo.InvariantCulture), storedFileName));

    public async Task<(string StoredFileName, string Sha256)> SaveAsync(int competitionId, Stream content, CancellationToken ct)
    {
        Directory.CreateDirectory(CompetitionPath(competitionId));
        var stored = $"{Guid.NewGuid():N}.docx";
        var path = FullPath(competitionId, stored);
        try
        {
            await using var output = File.Create(path);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                hash.AppendData(buffer, 0, read);
            }
            return (stored, Convert.ToHexString(hash.GetHashAndReset()));
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    public Stream OpenRead(int competitionId, string storedFileName) => File.OpenRead(FullPath(competitionId, storedFileName));

    public void Delete(int competitionId, string storedFileName) => TryDelete(FullPath(competitionId, storedFileName));

    void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { logger.LogWarning(ex, "Unable to clean certificate template file {Path}", path); }
    }

    string SafePath(string relative)
    {
        var root = Path.GetFullPath(RootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, relative));
        var remainder = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(remainder) || remainder == ".." || remainder.StartsWith(".." + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Certificate template path escapes the configured storage root.");
        return path;
    }
}
