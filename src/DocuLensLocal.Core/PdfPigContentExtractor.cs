using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DocuLensLocal.Core;

public sealed class PdfPigContentExtractor : IPdfContentExtractor
{
    public const int SparseTextLetterThreshold = 40;

    private readonly IOcrEngine _ocr;
    private readonly IPdfPageRasterizer _rasterizer;

    public PdfPigContentExtractor()
        : this(CompositeOcrEngine.CreateDefault(), new PdfToImageRasterizer())
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
            var ocrPageNumbers = new List<int>();
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var digital = ReadDigitalText(page);
                var needsOcr = page.Letters.Count < SparseTextLetterThreshold && _ocr.IsAvailable;
                pages.Add(new PdfPageContent(page.Number, digital, ""));
                if (needsOcr)
                {
                    ocrPageNumbers.Add(page.Number);
                }
            }

            var ocrByPage = RecognizeSparsePages(pdfPath, ocrPageNumbers, cancellationToken);
            var ocrPages = 0;
            for (var i = 0; i < pages.Count; i++)
            {
                if (!ocrByPage.TryGetValue(pages[i].PageNumber, out var ocrText) || string.IsNullOrWhiteSpace(ocrText))
                {
                    continue;
                }

                pages[i] = pages[i] with { OcrText = ocrText };
                ocrPages++;
            }

            var body = string.Join("\n", pages.Select(p => p.CombinedText).Where(s => !string.IsNullOrWhiteSpace(s)));
            return new PdfExtractedContent(body, pages.Count, ocrPages, pages);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PdfExtractedContent.Empty;
        }
    }

    private Dictionary<int, string> RecognizeSparsePages(
        string pdfPath,
        List<int> pageNumbers,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<int, string>();
        if (pageNumbers.Count == 0)
        {
            return results;
        }

        using var session = _rasterizer.Open(pdfPath);
        var images = new Dictionary<int, byte[]>();
        foreach (var pageNumber in pageNumbers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                images[pageNumber] = session.RenderPng(pageNumber, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
            }
        }

        if (images.Count == 0)
        {
            return results;
        }

        var bag = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 4),
        };
        Parallel.ForEach(images, options, pair =>
        {
            try
            {
                bag[pair.Key] = _ocr.RecognizePng(pair.Value, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                bag[pair.Key] = string.Empty;
            }
        });

        foreach (var pair in bag)
        {
            results[pair.Key] = pair.Value;
        }

        return results;
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
