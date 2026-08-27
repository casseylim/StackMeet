namespace StackMeet.Api.Dtos;

public sealed record CertificateTemplateResponse(long Id, int CompetitionId, string CertificateType, string Name, string OriginalFileName, int TemplateVersion, bool IsActive, long FileSize, string Sha256, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record CertificateTemplatePreviewRequest(string ParticipantCode);
public sealed record ParticipantCertificateProjection(string ParticipantCode, string ParticipantName, string Organization, string Division, string Gender, string Country, string Region, bool IsSpecial);
