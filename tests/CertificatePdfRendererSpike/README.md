# Certificate PDF Renderer Spike

This isolated console benchmark tests Syncfusion DocIO against a real Word-created certificate template. It references the existing certificate fill service read-only; it does not modify `StackMeet.Api`.

Run it with:

```powershell
dotnet run --project tests/CertificatePdfRendererSpike/CertificatePdfRendererSpike.csproj -- `
  Participation-Certificate-Word-Original.docx `
  "$env:TEMP\stackmeet-certificate-pdf-renderer-spike" 5
```

The current benchmark produced a one-page A4 landscape PDF. The first conversion took approximately 6.96 seconds; warm conversions were approximately 53–60 ms in a five-run sequential benchmark. Extracted PDF text included the long participant name, code, division, organization, competition, and venue.

The output contained Syncfusion’s trial watermark because no Syncfusion license key was configured. This spike does not add or store a license key. Licensing and artwork-heavy fidelity remain decision gates before any production renderer work.
