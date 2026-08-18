using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

[ApiController, Route("api/competitions/{competitionId:int}/certificate-templates")]
public sealed class CertificateTemplatesController(
    StackMeetDbContext database,
    CompetitionPermissionService permissions,
    CertificateTemplateStorage storage,
    CertificateTemplateDocumentService documents,
    ParticipantCertificateProjectionService projections,
    AuditLogService audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CertificateTemplateResponse>>> List(int competitionId, CancellationToken ct)
    {
        var access = await Access(competitionId, ct);
        if (access is not null) return access;
        return Ok(await database.CertificateTemplates.AsNoTracking().Where(item => item.CompetitionId == competitionId).OrderBy(item => item.CertificateType).ThenByDescending(item => item.TemplateVersion).Select(item => Map(item)).ToListAsync(ct));
    }

    [HttpPost]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<ActionResult<CertificateTemplateResponse>> Upload(int competitionId, [FromForm] string certificateType, [FromForm] string name, IFormFile file, CancellationToken ct)
    {
        var access = await Access(competitionId, ct);
        if (access is not null) return access;
        if (file is null || file.Length == 0 || file.Length > 8 * 1024 * 1024) return BadRequest(new { error = "A non-empty DOCX file up to 8 MB is required." });
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150) return BadRequest(new { error = "Template name is required and must be 150 characters or fewer." });
        if (string.IsNullOrWhiteSpace(file.FileName) || Path.GetFileName(file.FileName).Length > 255) return BadRequest(new { error = "The original filename must be 255 characters or fewer." });
        if (!string.Equals(certificateType, CertificateTemplateDocumentService.Participation, StringComparison.OrdinalIgnoreCase)) return BadRequest(new { error = "Only Participation templates are supported in Phase 1A." });
        await using var input = file.OpenReadStream();
        CertificateTemplateDocument inspected;
        try { inspected = documents.Inspect(input, certificateType); }
        catch (InvalidDataException ex) { return BadRequest(new { error = ex.Message }); }
        input.Position = 0;
        var saved = await storage.SaveAsync(competitionId, input, ct);
        var actor = (HttpContext.Items["StackMeetSession"] as SessionToken)?.UserId;
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var type = CertificateTemplateDocumentService.Participation;
            var version = (await database.CertificateTemplates.Where(item => item.CompetitionId == competitionId && item.CertificateType == type).MaxAsync(item => (int?)item.TemplateVersion, ct) ?? 0) + 1;
            var now = DateTime.UtcNow;
            var entity = new CertificateTemplate { CompetitionId = competitionId, CertificateType = type, Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(file.FileName) : name.Trim(), OriginalFileName = Path.GetFileName(file.FileName), StoredFileName = saved.StoredFileName, ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", FileSize = file.Length, Sha256 = saved.Sha256, TemplateVersion = version, IsActive = false, CreatedAt = now, UpdatedAt = now, CreatedByUserId = actor, UpdatedByUserId = actor };
            database.CertificateTemplates.Add(entity);
            await audit.Write("CertificateTemplateUploaded", "CertificateTemplate", null, actor, competitionId, null, new { entity.CertificateType, entity.TemplateVersion, tags = inspected.Tags }, ct);
            await transaction.CommitAsync(ct);
            return Ok(Map(entity));
        }
        catch
        {
            storage.Delete(competitionId, saved.StoredFileName);
            throw;
        }
    }

    [HttpGet("{templateId:long}/download")]
    public async Task<IActionResult> Download(int competitionId, long templateId, CancellationToken ct)
    {
        var access = await Access(competitionId, ct);
        if (access is not null) return access;
        var item = await database.CertificateTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == templateId && x.CompetitionId == competitionId, ct);
        if (item is null) return NotFound();
        try
        {
            Response.Headers.CacheControl = "no-store";
            return File(storage.OpenRead(competitionId, item.StoredFileName), item.ContentType, item.OriginalFileName, enableRangeProcessing: true);
        }
        catch (FileNotFoundException) { return NotFound(); }
    }

    [HttpPost("{templateId:long}/activate")]
    public async Task<ActionResult<CertificateTemplateResponse>> Activate(int competitionId, long templateId, CancellationToken ct)
    {
        var access = await Access(competitionId, ct);
        if (access is not null) return access;
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var selected = await database.CertificateTemplates.SingleOrDefaultAsync(x => x.Id == templateId && x.CompetitionId == competitionId, ct);
        if (selected is null) return NotFound();
        if (selected.IsActive)
        {
            return Ok(Map(selected));
        }
        var active = await database.CertificateTemplates.Where(x => x.CompetitionId == competitionId && x.CertificateType == selected.CertificateType && x.IsActive).ToListAsync(ct);
        active.ForEach(item => item.IsActive = false);
        await database.SaveChangesAsync(ct);
        selected.IsActive = true;
        selected.UpdatedAt = DateTime.UtcNow;
        selected.UpdatedByUserId = (HttpContext.Items["StackMeetSession"] as SessionToken)?.UserId;
        await database.SaveChangesAsync(ct);
        await audit.Write("CertificateTemplateActivated", "CertificateTemplate", selected.Id.ToString(), selected.UpdatedByUserId, competitionId, null, new { selected.CertificateType, selected.TemplateVersion }, ct);
        await transaction.CommitAsync(ct);
        return Ok(Map(selected));
    }

    [HttpPost("{templateId:long}/preview")]
    public async Task<IActionResult> Preview(int competitionId, long templateId, CertificateTemplatePreviewRequest request, CancellationToken ct)
    {
        var access = await Access(competitionId, ct);
        if (access is not null) return access;
        var item = await database.CertificateTemplates.AsNoTracking().Include(x => x.Competition).SingleOrDefaultAsync(x => x.Id == templateId && x.CompetitionId == competitionId, ct);
        if (item is null) return NotFound();
        var participant = await projections.Resolve(competitionId, request.ParticipantCode.Trim(), ct);
        if (participant is null) return NotFound(new { error = "Participant was not found." });
        await using var source = storage.OpenRead(competitionId, item.StoredFileName);
        var competition = item.Competition;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NADI.Participant.Name"] = participant.ParticipantName,
            ["NADI.Participant.Code"] = participant.ParticipantCode,
            ["NADI.Participant.Division"] = participant.Division,
            ["NADI.Participant.Organization"] = participant.Organization,
            ["NADI.Participant.Gender"] = participant.Gender,
            ["NADI.Participant.Country"] = participant.Country,
            ["NADI.Participant.Region"] = participant.Region,
            ["NADI.Competition.Name"] = competition.CompetitionName,
            ["NADI.Competition.Code"] = competition.CompetitionCode,
            ["NADI.Competition.StartDate"] = competition.StartDate.ToString("yyyy-MM-dd"),
            ["NADI.Competition.EndDate"] = competition.EndDate.ToString("yyyy-MM-dd"),
            ["NADI.Competition.Date"] = competition.StartDate.ToString("yyyy-MM-dd"),
            ["NADI.Competition.Venue"] = competition.Venue,
            ["NADI.Certificate.Number"] = "PREVIEW",
            ["NADI.Certificate.IssueDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["NADI.Certificate.VerificationCode"] = "PREVIEW"
        };
        var bytes = documents.Fill(source, values);
        Response.Headers.CacheControl = "no-store";
        return File(bytes, item.ContentType, $"preview-{participant.ParticipantCode}.docx");
    }

    async Task<ActionResult?> Access(int competitionId, CancellationToken ct)
    {
        if (HttpContext.Items["StackMeetSession"] is not SessionToken session || session.UserId is null) return Unauthorized();
        var role = await permissions.RoleForCompetitionId(session.UserId.Value, session.IsSystemAdmin, competitionId, ct);
        return role is null || !CompetitionPermissionService.CanManageCertificates(role) ? Forbid() : null;
    }

    static CertificateTemplateResponse Map(CertificateTemplate item) => new(item.Id, item.CompetitionId, item.CertificateType, item.Name, item.OriginalFileName, item.TemplateVersion, item.IsActive, item.FileSize, item.Sha256, item.CreatedAt, item.UpdatedAt);
}
