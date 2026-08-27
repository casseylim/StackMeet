using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;

namespace StackMeet.Api.Services;

public sealed class CertificateGenerationService(
    StackMeetDbContext database,
    CertificateTemplateStorage storage,
    CertificateTemplateDocumentService documents,
    ParticipantCertificateProjectionService projections,
    CompetitionPermissionService permissions,
    IEnumerable<ICertificatePdfRenderer> renderers)
{
    public const string PdfContentType = "application/pdf";

    public async Task<CertificateGenerationResult> GenerateAsync(int competitionId, string participantCode, SessionToken session, CancellationToken cancellationToken)
    {
        if (session.UserId is null) throw new CertificateGenerationException("Authentication is required.", 401);
        var role = await permissions.RoleForCompetitionId(session.UserId.Value, session.IsSystemAdmin, competitionId, cancellationToken);
        if (role is null || !CompetitionPermissionService.CanManageCertificates(role)) throw new CertificateGenerationException("Certificate generation is not authorized for this competition.", 403);
        if (renderers is null || !renderers.Any()) throw new CertificateGenerationException("PDF generation is not configured.", 503);

        var template = await database.CertificateTemplates.AsNoTracking().Include(item => item.Competition)
            .SingleOrDefaultAsync(item => item.CompetitionId == competitionId && item.CertificateType == CertificateTemplateDocumentService.Participation && item.IsActive, cancellationToken);
        if (template is null) throw new CertificateGenerationException("No active Participation certificate template is configured.", 404);
        var projection = await projections.Resolve(competitionId, participantCode.Trim(), cancellationToken);
        if (projection is null) throw new CertificateGenerationException("Participant was not found.", 404);

        await using var source = storage.OpenRead(competitionId, template.StoredFileName);
        var values = BuildValues(template.Competition, projection);
        var filled = documents.Fill(source, values);
        using var validated = new MemoryStream(filled, writable: false);
        documents.Inspect(validated, CertificateTemplateDocumentService.Participation);
        validated.Position = 0;
        await using var pdf = new MemoryStream();
        await renderers.First().RenderAsync(validated, pdf, cancellationToken);
        var bytes = pdf.ToArray();
        return new CertificateGenerationResult(bytes, SafeFileName(template.Competition.CompetitionCode, template.CertificateType, projection.ParticipantCode), PdfContentType, bytes.LongLength);
    }

    Dictionary<string, string> BuildValues(Models.Competition competition, Dtos.ParticipantCertificateProjection participant) => new(StringComparer.Ordinal)
    {
        [CertificatePlaceholderCatalogue.ParticipantName] = participant.ParticipantName,
        [CertificatePlaceholderCatalogue.ParticipantCode] = participant.ParticipantCode,
        [CertificatePlaceholderCatalogue.ParticipantDivision] = participant.Division,
        [CertificatePlaceholderCatalogue.ParticipantOrganization] = participant.Organization,
        [CertificatePlaceholderCatalogue.ParticipantGender] = participant.Gender,
        [CertificatePlaceholderCatalogue.ParticipantCountry] = participant.Country,
        [CertificatePlaceholderCatalogue.ParticipantRegion] = participant.Region,
        [CertificatePlaceholderCatalogue.CompetitionName] = competition.CompetitionName,
        [CertificatePlaceholderCatalogue.CompetitionCode] = competition.CompetitionCode,
        [CertificatePlaceholderCatalogue.CompetitionStartDate] = competition.StartDate.ToString("yyyy-MM-dd"),
        [CertificatePlaceholderCatalogue.CompetitionEndDate] = competition.EndDate.ToString("yyyy-MM-dd"),
        [CertificatePlaceholderCatalogue.CompetitionDate] = competition.StartDate.ToString("yyyy-MM-dd"),
        [CertificatePlaceholderCatalogue.Venue] = competition.Venue,
        [CertificatePlaceholderCatalogue.CertificateNumber] = "GENERATED",
        [CertificatePlaceholderCatalogue.IssueDate] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
        [CertificatePlaceholderCatalogue.VerificationCode] = "GENERATED"
    };

    static string SafeFileName(string competitionCode, string certificateType, string participantCode)
    {
        var raw = $"{competitionCode}-{certificateType}-{participantCode}.pdf";
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(raw.Select(ch => invalid.Contains(ch) || char.IsControl(ch) ? '_' : ch));
    }
}
