using System.Diagnostics;
using DocumentFormat.OpenXml.Packaging;
using GemBox.Document;
using StackMeet.Api.Services;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotnet run -- <word-template.docx> [output-directory] [iterations]");
    return;
}

var license = Environment.GetEnvironmentVariable("GEMBOX_LICENSE") ?? throw new InvalidOperationException("Set GEMBOX_LICENSE for the isolated evaluation run; no license is stored in source.");
ComponentInfo.SetLicense(license);
var inputPath = Path.GetFullPath(args[0]);
var outputDirectory = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "stackmeet-certificate-pdf-renderer-gembox-spike"));
var iterations = args.Length > 2 && int.TryParse(args[2], out var parsed) ? parsed : 1;
if (!File.Exists(inputPath)) throw new FileNotFoundException("Word template not found.", inputPath);
if (iterations is < 1 or > 25) throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations must be between 1 and 25.");
Directory.CreateDirectory(outputDirectory);

var values = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["NADI.Participant.Name"] = "Nur Aisyah Binti Muhammad Syafiq Abdul Rahman",
    ["NADI.Participant.Code"] = "1.123",
    ["NADI.Participant.Division"] = "11 & Under Female",
    ["NADI.Participant.Organization"] = "Sekolah Kebangsaan Taman Seri Puteri Antarabangsa",
    ["NADI.Participant.Gender"] = "Female",
    ["NADI.Competition.Name"] = "NADITrack International Sport Stacking Championship 2026",
    ["NADI.Competition.Venue"] = "Kuala Lumpur",
    ["NADI.Competition.Date"] = "2026-07-11",
    ["NADI.Certificate.Number"] = "PREVIEW"
};

var filled = new CertificateTemplateDocumentService().Fill(new MemoryStream(File.ReadAllBytes(inputPath)), values);
using (var reopened = WordprocessingDocument.Open(new MemoryStream(filled), false))
    Console.WriteLine($"Filled DOCX reopened: {reopened.MainDocumentPart?.Document?.Body?.InnerText.Length ?? 0} body characters");

var timings = new List<double>();
for (var i = 0; i < iterations; i++)
{
    var stopwatch = Stopwatch.StartNew();
    var document = DocumentModel.Load(new MemoryStream(filled), LoadOptions.DocxDefault);
    var outputPath = Path.Combine(outputDirectory, i == 0 ? "Participation-Certificate-GemBox.pdf" : $"Participation-Certificate-GemBox-{i + 1}.pdf");
    document.Save(outputPath, SaveOptions.PdfDefault);
    stopwatch.Stop();
    timings.Add(stopwatch.Elapsed.TotalMilliseconds);
    Console.WriteLine($"PASS conversion {i + 1}/{iterations}: {stopwatch.Elapsed.TotalMilliseconds:F1} ms, {new FileInfo(outputPath).Length} bytes, {outputPath}");
}

Console.WriteLine($"GemBox benchmark complete: runs={iterations}, averageMs={timings.Average():F1}, minMs={timings.Min():F1}, maxMs={timings.Max():F1}");
