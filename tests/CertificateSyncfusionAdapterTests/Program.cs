using StackMeet.Api.Services;
using StackMeet.Certificate.Syncfusion;

var adapter = new SyncfusionCertificatePdfRenderer();
if (adapter.IsConfigured) throw new InvalidOperationException("Test must not receive a license key.");
await using var input = new MemoryStream([0x50, 0x4b]);
await using var output = new MemoryStream();
try { await adapter.RenderAsync(input, output, CancellationToken.None); throw new InvalidOperationException("Unlicensed production-mode adapter unexpectedly rendered."); }
catch (CertificateGenerationException ex) when (ex.StatusCode == 503) { Console.WriteLine("PASS missing license reports sanitized 503"); }
Console.WriteLine("Syncfusion adapter capability test passed.");

var source = File.ReadAllBytes(Path.Combine("Participation-Certificate-Word-Original.docx"));
var values = new Dictionary<string, string>
{
    [CertificatePlaceholderCatalogue.ParticipantName] = "Nur Aisyah Binti 李明 & O'Connor <Test>",
    [CertificatePlaceholderCatalogue.ParticipantCode] = "1.123",
    [CertificatePlaceholderCatalogue.ParticipantDivision] = "11 & Under Female",
    [CertificatePlaceholderCatalogue.ParticipantOrganization] = "Sekolah Kebangsaan / École",
    [CertificatePlaceholderCatalogue.CompetitionName] = "NADITrack 2026",
    [CertificatePlaceholderCatalogue.Venue] = "Kuala Lumpur",
    [CertificatePlaceholderCatalogue.CertificateNumber] = "PREVIEW"
};
var filled = new CertificateTemplateDocumentService().Fill(new MemoryStream(source), values);
await using var trialInput = new MemoryStream(filled);
await using var trialOutput = new MemoryStream();
await new SyncfusionCertificatePdfRenderer(allowTrial: true).RenderAsync(trialInput, trialOutput, CancellationToken.None);
var pdf = trialOutput.ToArray();
if (pdf.Length == 0 || !pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8)) throw new InvalidOperationException("Trial render did not produce a PDF signature.");
Console.WriteLine($"PASS trial mechanical PDF render: {pdf.Length} bytes; visual/license acceptance remains separate.");
