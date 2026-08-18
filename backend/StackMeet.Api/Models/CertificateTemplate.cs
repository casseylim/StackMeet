namespace StackMeet.Api.Models;

public sealed class CertificateTemplate
{
    public long Id { get; set; }
    public int CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;
    public string CertificateType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public int TemplateVersion { get; set; }
    public int TemplateSchemaVersion { get; set; } = 1;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public AppUser? UpdatedByUser { get; set; }
}
