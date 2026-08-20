namespace DocuLensLocal.Core;

public sealed class CompositeDocumentExtractor : IPdfContentExtractor
{
    private readonly IPdfContentExtractor _pdf;
    private readonly IOcrEngine _ocr;

    public CompositeDocumentExtractor()
        : this(new PdfPigContentExtractor(), CompositeOcrEngine.CreateDefault())
    {
    }

    public CompositeDocumentExtractor(IPdfContentExtractor pdf)
        : this(pdf, CompositeOcrEngine.CreateDefault())
    {
    }

    public CompositeDocumentExtractor(IPdfContentExtractor pdf, IOcrEngine ocr)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(ocr);
        _pdf = pdf;
        _ocr = ocr;
    }

    public PdfExtractedContent Extract(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return IndexableFiles.KindOf(path) switch
        {
            IndexableFileKind.Pdf => _pdf.Extract(path, cancellationToken),
            IndexableFileKind.Docx or IndexableFileKind.Hwpx => OfficeBodyExtractor.Combine(
                ZipOfficeTextExtractor.Extract(path, cancellationToken),
                ZipOfficeMedia.ReadImages(path, cancellationToken),
                _ocr,
                cancellationToken),
            IndexableFileKind.Hwp => CombineHwp(path, cancellationToken),
            IndexableFileKind.Xlsx or IndexableFileKind.Xlsm => OfficeBodyExtractor.Combine(
                ExcelZipTextExtractor.Extract(path, cancellationToken),
                ZipOfficeMedia.ReadImages(path, cancellationToken),
                _ocr,
                cancellationToken),
            IndexableFileKind.Xls => OfficeBodyExtractor.Combine(
                LegacyXlsTextExtractor.Extract(path, cancellationToken),
                OleEmbeddedImages.Read(path, cancellationToken),
                _ocr,
                cancellationToken),
            IndexableFileKind.Doc => OfficeBodyExtractor.Combine(
                LegacyDocTextExtractor.Extract(path, cancellationToken),
                OleEmbeddedImages.Read(path, cancellationToken),
                _ocr,
                cancellationToken),
            _ => PdfExtractedContent.Empty,
        };
    }

    private PdfExtractedContent CombineHwp(string path, CancellationToken cancellationToken)
    {
        var extracted = HwpBinaryTextExtractor.ExtractAll(path, cancellationToken);
        return OfficeBodyExtractor.Combine(extracted.Text, extracted.Images, _ocr, cancellationToken);
    }
}
