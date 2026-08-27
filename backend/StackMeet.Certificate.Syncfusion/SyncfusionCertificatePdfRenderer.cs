using StackMeet.Api.Services;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;

namespace StackMeet.Certificate.Syncfusion;

public sealed class SyncfusionCertificatePdfRenderer(string? licenseKey = null, bool allowTrial = false) : ICertificatePdfRenderer
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(licenseKey);

    public async Task RenderAsync(Stream validatedDocx, Stream pdfOutput, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validatedDocx);
        ArgumentNullException.ThrowIfNull(pdfOutput);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsConfigured && !allowTrial) throw new CertificateGenerationException("PDF generation is not configured.", 503);
        if (!string.IsNullOrWhiteSpace(licenseKey)) global::Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
        using var word = new WordDocument(validatedDocx, FormatType.Docx);
        using var renderer = new DocIORenderer();
        using var pdf = renderer.ConvertToPDF(word);
        cancellationToken.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        pdf.Save(buffer);
        buffer.Position = 0;
        await buffer.CopyToAsync(pdfOutput, cancellationToken);
    }
}
