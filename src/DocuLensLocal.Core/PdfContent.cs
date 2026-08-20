namespace DocuLensLocal.Core;

public sealed record PdfPageContent(int PageNumber, string DigitalText, string OcrText)
{
    public string CombinedText => string.Join(" ", new[] { DigitalText, OcrText }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public sealed record PdfExtractedContent(
    string BodyText,
    int PageCount,
    int OcrPageCount,
    IReadOnlyList<PdfPageContent> Pages)
{
    public static PdfExtractedContent Empty { get; } = new("", 0, 0, []);
}

public interface IPdfContentExtractor
{
    PdfExtractedContent Extract(string pdfPath, CancellationToken cancellationToken = default);
}

public interface IOcrEngine
{
    bool IsAvailable { get; }

    string RecognizePng(byte[] pngBytes, CancellationToken cancellationToken = default);
}

public interface IPdfPageRasterizer
{
    byte[] RenderPng(string pdfPath, int pageNumber, CancellationToken cancellationToken = default);

    IPdfRenderSession Open(string pdfPath) => new DelegatingPdfRenderSession(this, pdfPath);
}

public interface IPdfRenderSession : IDisposable
{
    byte[] RenderPng(int pageNumber, CancellationToken cancellationToken = default);
}

internal sealed class DelegatingPdfRenderSession : IPdfRenderSession
{
    private readonly IPdfPageRasterizer _rasterizer;
    private readonly string _pdfPath;

    public DelegatingPdfRenderSession(IPdfPageRasterizer rasterizer, string pdfPath)
    {
        _rasterizer = rasterizer;
        _pdfPath = pdfPath;
    }

    public byte[] RenderPng(int pageNumber, CancellationToken cancellationToken = default) =>
        _rasterizer.RenderPng(_pdfPath, pageNumber, cancellationToken);

    public void Dispose()
    {
    }
}

public enum SearchMatchKind
{
    FileName,
    Body,
    Both,
}

public sealed class SearchHit
{
    public required IndexedDocument Document { get; init; }

    public required SearchMatchKind MatchKind { get; init; }

    public required string Snippet { get; init; }

    public required string MatchLabelKo { get; init; }
}
