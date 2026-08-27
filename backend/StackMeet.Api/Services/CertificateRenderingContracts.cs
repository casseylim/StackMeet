namespace StackMeet.Api.Services;

public interface ICertificatePdfRenderer
{
    Task RenderAsync(Stream validatedDocx, Stream pdfOutput, CancellationToken cancellationToken);
}

public sealed record CertificatePdfRenderRequest(Stream ValidatedDocx, string CertificateType);

public sealed record CertificateGenerationResult(
    byte[] Pdf,
    string FileName,
    string ContentType,
    long Length);

public sealed class CertificateGenerationException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public static class CertificatePlaceholderCatalogue
{
    public const string ParticipantName = "NADI.Participant.Name";
    public const string ParticipantCode = "NADI.Participant.Code";
    public const string ParticipantDivision = "NADI.Participant.Division";
    public const string ParticipantOrganization = "NADI.Participant.Organization";
    public const string ParticipantGender = "NADI.Participant.Gender";
    public const string ParticipantCountry = "NADI.Participant.Country";
    public const string ParticipantRegion = "NADI.Participant.Region";
    public const string CompetitionName = "NADI.Competition.Name";
    public const string CompetitionCode = "NADI.Competition.Code";
    public const string CompetitionStartDate = "NADI.Competition.StartDate";
    public const string CompetitionEndDate = "NADI.Competition.EndDate";
    public const string CompetitionDate = "NADI.Competition.Date";
    public const string Venue = "NADI.Competition.Venue";
    public const string CertificateNumber = "NADI.Certificate.Number";
    public const string CertificateType = "NADI.Certificate.Type";
    public const string IssueDate = "NADI.Certificate.IssueDate";
    public const string VerificationCode = "NADI.Certificate.VerificationCode";

    public static readonly IReadOnlySet<string> SupportedTags = new HashSet<string>(StringComparer.Ordinal)
    {
        ParticipantName, ParticipantCode, ParticipantDivision, ParticipantOrganization, ParticipantGender,
        ParticipantCountry, ParticipantRegion, CompetitionName, CompetitionCode, CompetitionStartDate,
        CompetitionEndDate, CompetitionDate, Venue, CertificateNumber, IssueDate, VerificationCode
    };
}
