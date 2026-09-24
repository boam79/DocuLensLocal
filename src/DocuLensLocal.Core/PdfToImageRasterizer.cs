using PDFtoImage;
using SkiaSharp;
using System.Runtime.Versioning;

namespace DocuLensLocal.Core;

[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class PdfToImageRasterizer : IPdfPageRasterizer
{
    public const int OcrDpi = 120;

    private static readonly RenderOptions Options = new()
    {
        Dpi = OcrDpi,
        Grayscale = true,
        WithAnnotations = false,
        WithFormFill = false,
    };

    public byte[] RenderPng(string pdfPath, int pageNumber, CancellationToken cancellationToken = default)
    {
        using var session = Open(pdfPath);
        return session.RenderPng(pageNumber, cancellationToken);
    }

    public IPdfRenderSession Open(string pdfPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);
        return new Session(pdfPath);
    }

    private sealed class Session : IPdfRenderSession
    {
        private readonly FileStream _stream;

        public Session(string pdfPath)
        {
            _stream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public byte[] RenderPng(int pageNumber, CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (_stream.CanSeek)
            {
                _stream.Position = 0;
            }

            using var bitmap = Conversion.ToImage(_stream, pageNumber - 1, leaveOpen: true, options: Options);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 75);
            return data.ToArray();
        }

        public void Dispose() => _stream.Dispose();
    }
}
