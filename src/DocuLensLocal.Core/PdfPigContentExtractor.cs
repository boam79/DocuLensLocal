using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

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
                var digital = ReadDigitalText(page);
                var ocrText = string.Empty;
                var letterCount = page.Letters.Count;
                // Scan PDFs often hide images in XObjects, so OCR any sparse page when an engine exists.
                if (letterCount < SparseTextLetterThreshold && _ocr.IsAvailable)
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

    private static string ReadDigitalText(UglyToad.PdfPig.Content.Page page)
    {
        try
        {
            var ordered = ContentOrderTextExtractor.GetText(page);
            if (!string.IsNullOrWhiteSpace(ordered))
            {
                return ordered.Trim();
            }
        }
        catch (Exception)
        {
        }

        if (!string.IsNullOrWhiteSpace(page.Text))
        {
            return page.Text.Trim();
        }

        var words = page.GetWords().Select(word => word.Text).Where(text => !string.IsNullOrWhiteSpace(text));
        return string.Join(" ", words).Trim();
    }
}
