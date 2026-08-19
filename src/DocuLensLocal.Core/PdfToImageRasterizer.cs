using PDFtoImage;
using SkiaSharp;
using System.Runtime.Versioning;

namespace DocuLensLocal.Core;

[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class PdfToImageRasterizer : IPdfPageRasterizer
{
    public byte[] RenderPng(string pdfPath, int pageNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var stream = File.OpenRead(pdfPath);
        using var bitmap = Conversion.ToImage(stream, pageNumber - 1, options: new RenderOptions
        {
            Dpi = 150,
        });
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
