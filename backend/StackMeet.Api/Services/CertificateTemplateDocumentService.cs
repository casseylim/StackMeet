using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace StackMeet.Api.Services;

public sealed record CertificateTemplateDocument(IReadOnlySet<string> Tags);

public sealed class CertificateTemplateDocumentService
{
    public const string Participation = "Participation";
    public static readonly IReadOnlySet<string> SupportedTags = new HashSet<string>(StringComparer.Ordinal)
    {
        "NADI.Participant.Name", "NADI.Participant.Code", "NADI.Participant.Division", "NADI.Participant.Organization",
        "NADI.Participant.Gender", "NADI.Participant.Country", "NADI.Participant.Region", "NADI.Competition.Name",
        "NADI.Competition.Code", "NADI.Competition.StartDate", "NADI.Competition.EndDate", "NADI.Competition.Date",
        "NADI.Competition.Venue", "NADI.Certificate.Number", "NADI.Certificate.IssueDate", "NADI.Certificate.VerificationCode"
    };

    public CertificateTemplateDocument Inspect(Stream input, string certificateType)
    {
        if (!string.Equals(certificateType, Participation, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Only Participation templates are supported in Certificate Foundation Phase 1A.");

        if (input.CanSeek) input.Position = 0;
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count > 200 || archive.Entries.Sum(entry => entry.Length) > 40 * 1024 * 1024)
            throw new InvalidDataException("The DOCX package is too large after expansion.");
        if (archive.GetEntry("[Content_Types].xml") is null || archive.GetEntry("word/document.xml") is null)
            throw new InvalidDataException("The upload is not a valid Word document.");
        if (archive.Entries.Any(entry => entry.FullName.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(entry.FullName)))
            throw new InvalidDataException("The DOCX contains an unsafe package path.");
        if (!string.Equals(archive.GetEntry("[Content_Types].xml")?.Name, "[Content_Types].xml", StringComparison.Ordinal) ||
            !string.Equals(archive.GetEntry("word/document.xml")?.Name, "document.xml", StringComparison.Ordinal))
            throw new InvalidDataException("Only ordinary DOCX packages are supported.");
        if (archive.Entries.Any(entry => entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase) ||
            entry.FullName.StartsWith("word/embeddings/", StringComparison.OrdinalIgnoreCase) ||
            entry.FullName.StartsWith("word/activeX/", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Macro-enabled Word documents are not supported.");
        if (archive.Entries.Where(entry => entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)).Any(HasExternalRelationship))
            throw new InvalidDataException("External DOCX relationships are not supported.");

        if (input.CanSeek) input.Position = 0;
        using var document = WordprocessingDocument.Open(input, false);
        var roots = new List<OpenXmlElement>();
        if (document.MainDocumentPart?.Document is not null) roots.Add(document.MainDocumentPart.Document);
        roots.AddRange(document.MainDocumentPart?.HeaderParts.Select(part => (OpenXmlElement)part.Header) ?? []);
        roots.AddRange(document.MainDocumentPart?.FooterParts.Select(part => (OpenXmlElement)part.Footer) ?? []);
        var tags = roots.SelectMany(root => root.Descendants<SdtProperties>())
            .Select(properties => properties.GetFirstChild<Tag>()?.Val?.Value)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!)
            .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        var allowedProperties = new HashSet<string>(StringComparer.Ordinal) { "tag", "alias", "id", "lock", "text", "rPr", "placeholder", "showingPlcHdr" };
        if (roots.SelectMany(root => root.Descendants<SdtElement>()).Any(control =>
            control.SdtProperties?.Elements().Any(element => !allowedProperties.Contains(element.LocalName)) == true))
            throw new InvalidDataException("Only text content controls are supported.");
        var unknown = tags.Where(tag => tag.StartsWith("NADI.", StringComparison.Ordinal) && !SupportedTags.Contains(tag)).ToArray();
        if (unknown.Length > 0) throw new InvalidDataException($"Unknown NADI content control: {unknown[0]}");
        if (!tags.Contains("NADI.Participant.Name") || !tags.Contains("NADI.Competition.Name"))
            throw new InvalidDataException("Participation templates require NADI.Participant.Name and NADI.Competition.Name.");
        return new CertificateTemplateDocument(tags);
    }

    public byte[] Fill(Stream input, IReadOnlyDictionary<string, string> values)
    {
        if (input.CanSeek) input.Position = 0;
        using var output = new MemoryStream();
        input.CopyTo(output);
        output.Position = 0;
        using (var document = WordprocessingDocument.Open(output, true))
        {
            var roots = new List<OpenXmlElement>();
            if (document.MainDocumentPart?.Document is not null) roots.Add(document.MainDocumentPart.Document);
            roots.AddRange(document.MainDocumentPart?.HeaderParts.Select(part => (OpenXmlElement)part.Header) ?? []);
            roots.AddRange(document.MainDocumentPart?.FooterParts.Select(part => (OpenXmlElement)part.Footer) ?? []);
            foreach (var control in roots.SelectMany(root => root.Descendants<SdtElement>()))
            {
                var tag = control.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
                if (tag is null || !values.TryGetValue(tag, out var value)) continue;
                var texts = control.Descendants<Text>().ToList();
                if (texts.Count > 0)
                {
                    texts[0].Text = value;
                    foreach (var extra in texts.Skip(1)) extra.Text = string.Empty;
                }
            }
            document.MainDocumentPart?.Document.Save();
        }
        return output.ToArray();
    }

    static bool HasExternalRelationship(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        var xml = XDocument.Load(stream);
        return xml.Descendants().Any(node => string.Equals(node.Attribute("TargetMode")?.Value, "External", StringComparison.OrdinalIgnoreCase));
    }
}
