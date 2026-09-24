namespace DocuLensLocal.Core;

public static class OfficeBodyExtractor
{
    public const int SparseLetterThreshold = 80;
    public const int MinImageBytes = 2_000;
    public const int MaxImages = 30;

    public static bool ShouldOcr(string digitalText, int imageCount) =>
        imageCount > 0 && CountLetters(digitalText) < SparseLetterThreshold;

    public static PdfExtractedContent Combine(
        string digitalText,
        IReadOnlyList<byte[]> images,
        IOcrEngine ocr,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(ocr);

        var digital = (digitalText ?? string.Empty).Trim();
        var ocrParts = new List<string>();
        var ocrPages = 0;
        if (ShouldOcr(digital, images.Count) && ocr.IsAvailable)
        {
            foreach (var image in images.Where(item => item.Length >= MinImageBytes).Take(MaxImages))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var text = ocr.RecognizePng(image, cancellationToken);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    ocrParts.Add(text.Trim());
                    ocrPages++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                }
            }
        }

        var ocrText = string.Join("\n", ocrParts);
        var body = string.Join("\n", new[] { digital, ocrText }.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (string.IsNullOrWhiteSpace(body))
        {
            return PdfExtractedContent.Empty;
        }

        return new PdfExtractedContent(body, PageCount: Math.Max(1, ocrPages), ocrPages, [new PdfPageContent(1, digital, ocrText)]);
    }

    public static int CountLetters(string text) =>
        string.IsNullOrEmpty(text) ? 0 : text.Count(char.IsLetter);
}
