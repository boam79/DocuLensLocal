using UglyToad.PdfPig;

namespace DocuLensLocal.Core;

public sealed class PdfPigContentExtractor : IPdfContentExtractor
{
    public const int SparseTextLetterThreshold = 40;

    private readonly IOcrEngine _ocr;
    private readonly IPdfPageRasterizer _rasterizer;

    public PdfPigContentExtractor()
        : this(new TesseractCliOcrEngine(), new PdfToImageRasterizer())
    {
    }

    public PdfPigContentExtractor(IOcrEngine ocr, IPdfPageRasterizer rasterizer)
    {
        _ocr = ocr;
        _rasterizer = rasterizer;
    }

    public PdfExtractedContent Extract(string pdfPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);
        if (!File.Exists(pdfPath))
        {
            return PdfExtractedContent.Empty;
        }

        try
        {
            using var document = PdfDocument.Open(pdfPath, new ParsingOptions { UseLenientParsing = true });
            var pages = new List<PdfPageContent>();
            var ocrPages = 0;
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var digital = page.Text?.Trim() ?? string.Empty;
                var ocrText = string.Empty;
                var letterCount = page.Letters.Count;
                var hasImages = page.GetImages().Any();
                if (letterCount < SparseTextLetterThreshold && hasImages && _ocr.IsAvailable)
                {
                    try
                    {
                        var png = _rasterizer.RenderPng(pdfPath, page.Number, cancellationToken);
                        ocrText = _ocr.RecognizePng(png, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(ocrText))
                        {
                            ocrPages++;
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        ocrText = string.Empty;
                    }
                }

                pages.Add(new PdfPageContent(page.Number, digital, ocrText));
            }

            var body = string.Join("\n", pages.Select(p => p.CombinedText).Where(s => !string.IsNullOrWhiteSpace(s)));
            return new PdfExtractedContent(body, pages.Count, ocrPages, pages);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PdfExtractedContent.Empty;
        }
    }
}
