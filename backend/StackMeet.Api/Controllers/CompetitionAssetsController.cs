using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

[ApiController, Route("api/competitions/{competitionId:int}/assets")]
public sealed class CompetitionAssetsController(StackMeetDbContext database, CompetitionPermissionService permissions, CompetitionAssetStorage storage, AuditLogService audit, ILogger<CompetitionAssetsController> logger) : ControllerBase
{
    static readonly HashSet<string> Types = ["logo", "banner"];

    [HttpGet] public async Task<ActionResult<IReadOnlyList<CompetitionAssetResponse>>> List(int competitionId, CancellationToken ct) { var access = await Access(competitionId, false, ct); if (access is not null) return access; return Ok(await database.CompetitionAssets.AsNoTracking().Where(x => x.CompetitionId == competitionId).Select(x => Map(x)).ToListAsync(ct)); }
    [HttpPost("{assetType}")] [RequestSizeLimit(5 * 1024 * 1024)] public async Task<ActionResult<CompetitionAssetResponse>> Upload(int competitionId, string assetType, IFormFile file, CancellationToken ct)
    {
        assetType = assetType.Trim().ToLowerInvariant(); if (!Types.Contains(assetType) || file is null || file.Length == 0 || file.Length > 5 * 1024 * 1024) return BadRequest(new { error = "Only non-empty images up to 5 MB are allowed." });
        var access = await Access(competitionId, true, ct); if (access is not null) return access;
        await using var input = file.OpenReadStream(); var format = await DetectFormat(input, ct); if (format is null) return BadRequest(new { error = "The upload is not a valid PNG, JPEG, or WebP image." }); input.Position = 0;
        var saved = await storage.SaveAsync(competitionId, format.Value.Extension, input, ct);
        var existing = await database.CompetitionAssets.SingleOrDefaultAsync(x => x.CompetitionId == competitionId && x.AssetType == assetType, ct); var oldStored = existing?.StoredFileName; var now = DateTime.UtcNow; var created = existing is null;
        if (existing is null) { existing = new CompetitionAsset { CompetitionId = competitionId, AssetType = assetType, CreatedAt = now }; database.CompetitionAssets.Add(existing); }
        existing.FileName = Path.GetFileName(file.FileName); existing.StoredFileName = saved.StoredFileName; existing.ContentType = format.Value.ContentType; existing.FileSize = file.Length; existing.Sha256 = saved.Sha256; existing.UpdatedAt = now; existing.UpdatedByUserId = (HttpContext.Items["StackMeetSession"] as SessionToken)?.UserId;
        try { await database.SaveChangesAsync(ct); } catch { storage.Delete(competitionId, saved.StoredFileName); throw; }
        if (!string.IsNullOrWhiteSpace(oldStored)) { try { storage.Delete(competitionId, oldStored); } catch (Exception ex) { logger.LogWarning(ex, "Unable to remove replaced competition asset file {StoredFileName} for competition {CompetitionId}", oldStored, competitionId); } }
        await audit.Write(created ? "CompetitionAssetUploaded" : "CompetitionAssetReplaced", "CompetitionAsset", assetType, existing.UpdatedByUserId, competitionId, null, new { assetType, fileName = existing.FileName, sha256 = existing.Sha256, fileSize = existing.FileSize }, ct);
        return Ok(Map(existing));
    }
    [HttpDelete("{assetType}")] public async Task<IActionResult> Delete(int competitionId, string assetType, CancellationToken ct) { var access = await Access(competitionId, true, ct); if (access is not null) return access; var competition = await database.Competitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == competitionId, ct); if (competition is null) return NotFound(); if (competition.ArchivedAt is not null || competition.Status.Equals("Archived", StringComparison.OrdinalIgnoreCase)) return Conflict(new { error = "Archived competitions cannot change branding." }); var item = await database.CompetitionAssets.SingleOrDefaultAsync(x => x.CompetitionId == competitionId && x.AssetType == assetType.ToLower(), ct); if (item is null) return NoContent(); var stored = item.StoredFileName; var userId = (HttpContext.Items["StackMeetSession"] as SessionToken)?.UserId; database.Remove(item); await database.SaveChangesAsync(ct); try { storage.Delete(competitionId, stored); } catch (Exception ex) { logger.LogWarning(ex, "Unable to remove deleted competition asset file {StoredFileName} for competition {CompetitionId}", stored, competitionId); } await audit.Write("CompetitionAssetRemoved", "CompetitionAsset", assetType, userId, competitionId, new { assetType, sha256 = item.Sha256, fileSize = item.FileSize }, null, ct); return NoContent(); }
    [HttpGet("/api/public/competitions/{competitionId:int}/assets/{assetType}")] public async Task<IActionResult> Public(int competitionId, string assetType, CancellationToken ct) { var item = await database.CompetitionAssets.AsNoTracking().Include(x => x.Competition).SingleOrDefaultAsync(x => x.CompetitionId == competitionId && x.AssetType == assetType.ToLower(), ct); if (item?.Competition is null || item.Competition.ArchivedAt is not null || item.Competition.Status.Equals("Archived", StringComparison.OrdinalIgnoreCase)) return NotFound(); var path = storage.FullPath(competitionId, item.StoredFileName); if (!System.IO.File.Exists(path)) return NotFound(); Response.Headers.ETag = $"\"{item.Sha256}\""; Response.Headers.CacheControl = "no-cache"; if (Request.Headers.IfNoneMatch == Response.Headers.ETag) return StatusCode(StatusCodes.Status304NotModified); return PhysicalFile(path, item.ContentType, enableRangeProcessing: true); }
    async Task<ActionResult?> Access(int id, bool write, CancellationToken ct) { if (HttpContext.Items["StackMeetSession"] is not SessionToken session || session.UserId is null) return Unauthorized(); var role = await permissions.RoleForCompetitionId(session.UserId.Value, session.IsSystemAdmin, id, ct); if (role is null || (write ? !CompetitionPermissionService.CanManageCompetition(role) : !CompetitionPermissionService.CanViewCompetition(role))) return StatusCode(StatusCodes.Status403Forbidden); return null; }
    static CompetitionAssetResponse Map(CompetitionAsset x) => new(x.AssetType, x.FileName, x.ContentType, x.FileSize, $"/api/public/competitions/{x.CompetitionId}/assets/{x.AssetType}", x.UpdatedAt);
    static async Task<(string Extension, string ContentType)?> DetectFormat(Stream input, CancellationToken ct) { var b = new byte[12]; var read = await input.ReadAsync(b, ct); input.Position = 0; if (read >= 8 && b.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return (".png", "image/png"); if (read >= 3 && b[0] == 0xff && b[1] == 0xd8 && b[2] == 0xff) return (".jpg", "image/jpeg"); if (read >= 12 && b.AsSpan(0, 4).SequenceEqual("RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return (".webp", "image/webp"); return null; }
}
