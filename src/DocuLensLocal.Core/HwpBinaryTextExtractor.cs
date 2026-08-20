using HwpLib.Reader;
using HwpLib.Tool.TextExtractor;

namespace DocuLensLocal.Core;

public static class HwpBinaryTextExtractor
{
    public static string Extract(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var hwp = HWPReader.FromStream(stream);
            var option = new TextExtractOption();
            option.SetMethod(TextExtractMethod.InsertControlTextBetweenParagraphText);
            option.SetWithControlChar(false);
            option.SetAppendEndingLF(true);
            return (TextExtractor.Extract(hwp, option) ?? string.Empty).Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return string.Empty;
        }
    }
}
