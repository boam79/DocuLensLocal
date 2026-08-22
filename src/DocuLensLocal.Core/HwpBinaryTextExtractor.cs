using HwpLib.Reader;
using HwpLib.Tool.TextExtractor;

namespace DocuLensLocal.Core;

public static class HwpBinaryTextExtractor
{
    public static string Extract(string path, CancellationToken cancellationToken = default) =>
        ExtractAll(path, cancellationToken).Text;

    public static IReadOnlyList<byte[]> ReadImages(string path, CancellationToken cancellationToken = default) =>
        ExtractAll(path, cancellationToken).Images;

    public static HwpExtracted ExtractAll(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return HwpExtracted.Empty;
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var stream = OfficeFileAccess.OpenRead(path);
            var hwp = HWPReader.FromStream(stream);
            var option = new TextExtractOption();
            option.SetMethod(TextExtractMethod.InsertControlTextBetweenParagraphText);
            option.SetWithControlChar(false);
            option.SetAppendEndingLF(true);
            var text = (TextExtractor.Extract(hwp, option) ?? string.Empty).Trim();
            var images = new List<byte[]>();
            foreach (var embedded in hwp.BinData.EmbeddedBinaryDataList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var data = embedded.Data;
                if (data is null || data.Length < OfficeBodyExtractor.MinImageBytes || !RasterImageBlobs.LooksLikeImage(data))
                {
                    continue;
                }

                images.Add(data);
                if (images.Count >= OfficeBodyExtractor.MaxImages)
                {
                    break;
                }
            }

            return new HwpExtracted(text, images);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !OfficeFileAccess.IsTransient(ex))
        {
            return HwpExtracted.Empty;
        }
    }
}

public readonly record struct HwpExtracted(string Text, IReadOnlyList<byte[]> Images)
{
    public static HwpExtracted Empty { get; } = new("", []);
}

