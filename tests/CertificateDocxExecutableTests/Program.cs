using System.Text;
using System.IO.Compression;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using StackMeet.Api.Services;

const string Participant = "Nur Aisyah Binti Ahmad";
const string Competition = "NADITrack Test Championship 2026";
var values = new Dictionary<string, string>
{
    ["NADI.Participant.Name"] = Participant, ["NADI.Participant.Code"] = "1.123",
    ["NADI.Participant.Division"] = "11 & Under Female", ["NADI.Participant.Organization"] = "Sekolah Kebangsaan Example",
    ["NADI.Participant.Gender"] = "Female", ["NADI.Competition.Name"] = Competition,
    ["NADI.Competition.Venue"] = "Kuala Lumpur", ["NADI.Certificate.Number"] = "PREVIEW"
};

var service = new CertificateTemplateDocumentService();
var valid = Fixture.Create();
var inspected = service.Inspect(new MemoryStream(valid), CertificateTemplateDocumentService.Participation);
AssertEx.True(inspected.Tags.Contains("NADI.Participant.Name") && inspected.Tags.Contains("NADI.Competition.Name"), "required tags discovered");
var filled = service.Fill(new MemoryStream(valid), values);
using (var reopened = WordprocessingDocument.Open(new MemoryStream(filled), false))
{
    AssertEx.Equal(Participant, Read(reopened.MainDocumentPart!.Document.Body!, "NADI.Participant.Name"), "body participant");
    AssertEx.Equal(Competition, Read(reopened.MainDocumentPart.Document.Body!, "NADI.Competition.Name"), "body competition");
    AssertEx.Equal("Kuala Lumpur", Read(reopened.MainDocumentPart.HeaderParts.First().Header, "NADI.Competition.Venue"), "header venue");
    AssertEx.Equal("PREVIEW", Read(reopened.MainDocumentPart.FooterParts.First().Footer, "NADI.Certificate.Number"), "footer number");
    AssertEx.Equal(2, reopened.MainDocumentPart.Document.Body!.Descendants<SdtElement>().Count(x => x.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value == "NADI.Participant.Name" && x.Descendants<Text>().Any(t => t.Text == Participant)), "repeated controls");
    AssertEx.True(!reopened.MainDocumentPart.Document.Body.InnerText.Contains("STALE-RUN"), "stale split-run text removed");
    AssertEx.True(reopened.MainDocumentPart.Document.Body.InnerText.Contains("Artwork remains"), "unrelated content preserved");
}
Console.WriteLine("PASS valid DOCX inspect/fill/reopen");

Reject("unknown NADI tag", Mutate(valid, (n, b) => n == "word/document.xml" ? b.Replace("NADI.Participant.Name", "NADI.Unknown") : b));
Reject("missing participant required tag", Mutate(valid, (n, b) => n == "word/document.xml" ? b.Replace("NADI.Participant.Name", "OTHER.Participant.Name") : b));
Reject("missing competition required tag", Mutate(valid, (n, b) => n == "word/document.xml" ? b.Replace("NADI.Competition.Name", "OTHER.Competition.Name") : b));
Reject("NADI rich-text control", Mutate(valid, (n, b) => n == "word/document.xml" ? b.Replace("<w:text />", "<w:date />") : b));
Reject("external relationship", Mutate(valid, (n, b) => n == "word/_rels/document.xml.rels" ? b.Replace("TargetMode=\"External\"", "TargetMode=\"External\"") .Replace("</Relationships>", "<Relationship Id=\"rId99\" Type=\"x\" Target=\"https://example.com\" TargetMode=\"External\"/></Relationships>") : b));
Reject("macro content type", Mutate(valid, (n, b) => n == "[Content_Types].xml" ? b.Replace("application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml", "application/vnd.ms-word.document.macroEnabled.main+xml") : b));
Reject("vbaProject.bin", AddEntry(valid, "word/vbaProject.bin", Array.Empty<byte>()));
Reject("ActiveX content", AddEntry(valid, "word/activeX/activeX1.xml", Encoding.UTF8.GetBytes("x")));
Reject("OLE content", AddEntry(valid, "word/embeddings/oleObject1.bin", Array.Empty<byte>()));
Reject("unsafe traversal entry", AddEntry(valid, "../escape.txt", Encoding.UTF8.GetBytes("x")));
AssertEx.Throws<InvalidDataException>(() => service.Inspect(new MemoryStream(Mutate(valid, (n, b) => n == "[Content_Types].xml" ? "<broken" : b)), CertificateTemplateDocumentService.Participation), "malformed content types");
AssertEx.Throws<InvalidDataException>(() => service.Inspect(new MemoryStream(Mutate(valid, (n, b) => n == "word/_rels/document.xml.rels" ? "<broken" : b)), CertificateTemplateDocumentService.Participation), "malformed relationships");
AssertEx.Throws<InvalidDataException>(() => service.Inspect(new MemoryStream(Encoding.UTF8.GetBytes("not a docx")), CertificateTemplateDocumentService.Participation), "malformed package");
Reject("entry count limit", AddEntries(valid, 201));
Reject("expanded size limit", AddEntry(valid, "word/large.bin", new byte[40 * 1024 * 1024]));
var boundary = AddEntry(valid, "word/boundary.bin", new byte[40 * 1024 * 1024 - (int)ExpandedSize(valid) - 1]);
service.Inspect(new MemoryStream(boundary), CertificateTemplateDocumentService.Participation);
Console.WriteLine("PASS boundary at/below expanded size");
Console.WriteLine("Certificate DOCX executable tests passed.");

static string Read(OpenXmlElement root, string tag) => root.Descendants<SdtElement>().Where(x => x.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value == tag).Select(x => x.Descendants<Text>().FirstOrDefault()?.Text ?? "").FirstOrDefault() ?? "";
static void Reject(string name, byte[] package) => AssertEx.Throws<InvalidDataException>(() => new CertificateTemplateDocumentService().Inspect(new MemoryStream(package), CertificateTemplateDocumentService.Participation), name);

static byte[] Mutate(byte[] source, Func<string, string, string> edit) => Repack(source, (n, b) => Encoding.UTF8.GetString(b) is var text && (n.EndsWith(".xml") || n.EndsWith(".rels")) ? Encoding.UTF8.GetBytes(edit(n, text)) : b);
static long ExpandedSize(byte[] source) { using var zip = new ZipArchive(new MemoryStream(source), ZipArchiveMode.Read); return zip.Entries.Sum(x => x.Length); }
static byte[] AddEntry(byte[] source, string name, byte[] content) => Repack(source, (n, b) => b, (z) => z.CreateEntry(name).Open().Write(content));
static byte[] AddEntries(byte[] source, int count) => Repack(source, (n, b) => b, z => { for (var i = 0; i < count; i++) { using var s = z.CreateEntry($"extra/{i}.bin").Open(); } });
static byte[] Repack(byte[] source, Func<string, byte[], byte[]> edit, Action<ZipArchive>? add = null)
{
    using var input = new ZipArchive(new MemoryStream(source), ZipArchiveMode.Read);
    using var output = new MemoryStream(); using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
    { foreach (var e in input.Entries) { var n = zip.CreateEntry(e.FullName); using var os = n.Open(); using var es = e.Open(); using var ms = new MemoryStream(); es.CopyTo(ms); var bytes = edit(e.FullName, ms.ToArray()); os.Write(bytes); } add?.Invoke(zip); }
    return output.ToArray();
}

sealed class Fixture
{
    public static byte[] Create()
    {
        using var ms = new MemoryStream(); using (var doc = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart(); main.Document = new Document(new Body(
                P("Artwork remains"), Control("NADI.Participant.Name", "STALE-RUN", true), Control("NADI.Participant.Name", "old"), Control("NADI.Competition.Name", "old"), Control("NADI.Participant.Division", "old"))); main.Document.Save();
            var header = main.AddNewPart<HeaderPart>(); header.Header = new Header(Control("NADI.Competition.Venue", "old")); header.Header.Save();
            var footer = main.AddNewPart<FooterPart>(); footer.Footer = new Footer(Control("NADI.Certificate.Number", "old")); footer.Footer.Save();
            if (main.Document.Body!.GetFirstChild<SectionProperties>() is null) main.Document.Body.AppendChild(new SectionProperties(new HeaderReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(header) }, new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) }));
        }
return AddMainOverride(ms.ToArray());
    }
    static byte[] AddMainOverride(byte[] source)
    {
        using var input = new ZipArchive(new MemoryStream(source), ZipArchiveMode.Read); using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in input.Entries)
            {
                var target = zip.CreateEntry(entry.FullName); using var os = target.Open(); using var es = entry.Open(); using var copy = new MemoryStream(); es.CopyTo(copy);
                var bytes = copy.ToArray();
                if (entry.FullName == "[Content_Types].xml") bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("</Types>", "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\" /></Types>"));
                os.Write(bytes);
            }
        }
        return output.ToArray();
    }
    static Paragraph P(string text) => new(new Run(new Text(text)));
    static SdtBlock Control(string tag, string text, bool split = false) => new(new SdtProperties(new Tag { Val = tag }, new SdtContentText()), new SdtContentBlock(new Paragraph(new Run(new Text(split ? "STALE-" : text)), split ? new Run(new Text("RUN")) : new Run(new Text(text)))));
}

static class AssertEx
{
    public static void True(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void Equal<T>(T expected, T actual, string message) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'"); }
    public static void Throws<T>(Action action, string name) where T : Exception
    { try { action(); throw new InvalidOperationException($"{name}: expected {typeof(T).Name}"); } catch (T) { Console.WriteLine($"PASS {name}"); } }
}

