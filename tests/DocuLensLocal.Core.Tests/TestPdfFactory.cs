using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocuLensLocal.Core.Tests;

internal static class TestPdfFactory
{
    public static string WriteDigitalPdf(string directory, string fileName, string text)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText(text, 12, new PdfPoint(50, 750), font);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    public static string WriteKoreanPdf(string directory, string fileName, string text)
    {
        var fontPath = FindKoreanFont();
        if (fontPath is null)
        {
            return WriteDigitalPdf(directory, fileName, "bus advertising contract clause");
        }

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddTrueTypeFont(File.ReadAllBytes(fontPath));
        page.AddText(text, 16, new PdfPoint(50, 750), font);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    public static byte[] RenderOpaquePng(string text)
    {
        using var bitmap = new SkiaSharp.SKBitmap(640, 160);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.White);
        using var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.Default, 48);
        using var paint = new SkiaSharp.SKPaint
        {
            Color = SkiaSharp.SKColors.Black,
            IsAntialias = true,
        };
        canvas.DrawText(text, 24, 90, SkiaSharp.SKTextAlign.Left, font, paint);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string? FindKoreanFont()
    {
        string[] candidates =
        [
            "/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf",
            "/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc",
            "/System/Library/Fonts/AppleSDGothicNeo.ttc",
            "/System/Library/Fonts/Supplemental/AppleGothic.ttf",
        ];
        return candidates.FirstOrDefault(File.Exists);
    }
}
