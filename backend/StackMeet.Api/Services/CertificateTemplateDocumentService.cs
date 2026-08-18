using System.IO.Compression;
using System.Xml.Linq;
using System.Xml;
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
        try { return InspectCore(input, certificateType); }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) when (ex is XmlException or OpenXmlPackageException or InvalidOperationException or IOException)
        { throw new InvalidDataException("The DOCX package is malformed or unsupported.", ex); }
    }

    CertificateTemplateDocument InspectCore(Stream input, string certificateType)
    {
        if (!string.Equals(certificateType, Participation, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Only Participation templates are supported in Certificate Foundation Phase 1A.");

        if (input.CanSeek) input.Position = 0;
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count > 200)
            throw new InvalidDataException("The DOCX package is too large after expansion.");
        long expandedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            expandedBytes += entry.Length;
            if (expandedBytes > 40 * 1024 * 1024)
                throw new InvalidDataException("The DOCX package is too large after expansion.");
        }
        if (archive.GetEntry("[Content_Types].xml") is null || archive.GetEntry("word/document.xml") is null)
            throw new InvalidDataException("The upload is not a valid Word document.");
        if (archive.Entries.Any(entry => entry.FullName.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(entry.FullName)))
            throw new InvalidDataException("The DOCX contains an unsafe package path.");
        var contentTypes = archive.GetEntry("[Content_Types].xml")!;
        using (var contentStream = contentTypes.Open())
        {
            var contentXml = XDocument.Load(contentStream);
            var mainDocument = contentXml.Descendants().FirstOrDefault(item =>
                string.Equals(item.Attribute("PartName")?.Value, "/word/document.xml", StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(mainDocument?.Attribute("ContentType")?.Value,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
                StringComparison.Ordinal))
                throw new InvalidDataException("Only ordinary DOCX packages are supported.");
        }
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
        foreach (var control in roots.SelectMany(root => root.Descendants<SdtElement>()))
        {
            var tag = control.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
            if (tag?.StartsWith("NADI.", StringComparison.Ordinal) == true &&
                control.SdtProperties?.Elements().Any(element => element.LocalName == "text") != true)
                throw new InvalidDataException("NADI content controls must be text controls.");
        }
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
        try
        {
            var xml = XDocument.Load(stream);
            return xml.Descendants().Any(node => string.Equals(node.Attribute("TargetMode")?.Value, "External", StringComparison.OrdinalIgnoreCase));
        }
        catch (XmlException ex) { throw new InvalidDataException("The DOCX relationships XML is malformed.", ex); }
    }
}
