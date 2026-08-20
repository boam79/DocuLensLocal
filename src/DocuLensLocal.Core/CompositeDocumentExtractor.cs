namespace DocuLensLocal.Core;

public sealed class CompositeDocumentExtractor : IPdfContentExtractor
{
    private readonly IPdfContentExtractor _pdf;

    public CompositeDocumentExtractor()
        : this(new PdfPigContentExtractor())
    {
    }

    public CompositeDocumentExtractor(IPdfContentExtractor pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        _pdf = pdf;
    }

    public PdfExtractedContent Extract(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var body = IndexableFiles.KindOf(path) switch
        {
            IndexableFileKind.Pdf => _pdf.Extract(path, cancellationToken),
            IndexableFileKind.Docx or IndexableFileKind.Hwpx => FromBody(ZipOfficeTextExtractor.Extract(path, cancellationToken)),
            IndexableFileKind.Hwp => FromBody(HwpBinaryTextExtractor.Extract(path, cancellationToken)),
            IndexableFileKind.Doc => FromBody(LegacyDocTextExtractor.Extract(path, cancellationToken)),
            _ => PdfExtractedContent.Empty,
        };
        return body;
    }

    private static PdfExtractedContent FromBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return PdfExtractedContent.Empty;
        }

        var trimmed = body.Trim();
        return new PdfExtractedContent(trimmed, 1, 0, [new PdfPageContent(1, trimmed, "")]);
    }
}
