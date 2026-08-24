# Certificate PDF Renderer GemBox Spike

This sibling console benchmark tests GemBox.Document against the same real Word-created certificate template and the same NADI fill values as the Syncfusion spike. It references the existing fill service read-only and does not modify `StackMeet.Api`.

Run it with:

```powershell
 $env:GEMBOX_LICENSE="<evaluation-key-supplied-out-of-band>"
dotnet run --project tests/CertificatePdfRendererGemBoxSpike/CertificatePdfRendererGemBoxSpike.csproj -- `
  Participation-Certificate-Word-Original.docx `
  "$env:TEMP\stackmeet-certificate-pdf-renderer-gembox-spike" 5
```

The spike reads an evaluation key from GEMBOX_LICENSE at runtime; no key is stored in source. Any evaluation limit, watermark, or conversion restriction is intentionally recorded as a benchmark result.
