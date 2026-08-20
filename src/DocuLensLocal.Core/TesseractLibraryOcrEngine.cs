using Tesseract;

namespace DocuLensLocal.Core;

public sealed class TesseractLibraryOcrEngine : IOcrEngine
{
    private readonly string? _tessdataDirectory;
    private readonly ThreadLocal<Dictionary<string, TesseractEngine>> _engines = new(
        () => new Dictionary<string, TesseractEngine>(StringComparer.Ordinal));

    public TesseractLibraryOcrEngine(string? tessdataDirectory = null)
    {
        _tessdataDirectory = tessdataDirectory;
    }

    public bool IsAvailable
    {
        get
        {
            try
            {
                return NativeFilesPresent() && TessdataLocator.HasLanguageData(ResolveTessdata());
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public string RecognizePng(byte[] pngBytes, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return string.Empty;
        }

        ArgumentNullException.ThrowIfNull(pngBytes);
        cancellationToken.ThrowIfCancellationRequested();

        var tessdata = ResolveTessdata();
        if (string.IsNullOrWhiteSpace(tessdata))
        {
            return string.Empty;
        }

        try
        {
            var primary = OcrLanguage.Primary(tessdata);
            var text = RecognizeWith(tessdata, primary, pngBytes, cancellationToken);
            var fallback = OcrLanguage.Fallback(tessdata, primary);
            if (fallback is not null && OcrLanguage.ShouldTryFallback(text))
            {
                var extra = RecognizeWith(tessdata, fallback, pngBytes, cancellationToken);
                if ((extra?.Length ?? 0) > (text?.Length ?? 0))
                {
                    return extra ?? string.Empty;
                }
            }

            return text ?? string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return string.Empty;
        }
    }

    internal static bool NativeFilesPresent()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var baseDir = AppContext.BaseDirectory;
        var arch = Environment.Is64BitProcess ? "x64" : "x86";
        return File.Exists(Path.Combine(baseDir, arch, "tesseract50.dll"))
            || File.Exists(Path.Combine(baseDir, arch, "tesseract41.dll"))
            || File.Exists(Path.Combine(baseDir, "tesseract50.dll"))
            || File.Exists(Path.Combine(baseDir, "tesseract41.dll"));
    }

    private string RecognizeWith(string tessdata, string language, byte[] imageBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var engine = EngineFor(tessdata, language);
        using var pix = LoadPix(imageBytes);
        using var page = engine.Process(pix);
        return Collapse(page.GetText());
    }

    private TesseractEngine EngineFor(string tessdata, string language)
    {
        var map = _engines.Value!;
        var key = tessdata + "\0" + language;
        if (map.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var engine = new TesseractEngine(tessdata, language, EngineMode.LstmOnly)
        {
            DefaultPageSegMode = PageSegMode.SingleBlock,
        };
        try
        {
            engine.SetVariable("tessedit_do_invert", "0");
        }
        catch (Exception)
        {
        }

        map[key] = engine;
        return engine;
    }

    private string? ResolveTessdata()
    {
        if (_tessdataDirectory is not null)
        {
            return TessdataLocator.HasLanguageData(_tessdataDirectory) ? _tessdataDirectory : null;
        }

        return TessdataLocator.FindDirectory();
    }

    private static Pix LoadPix(byte[] pngBytes)
    {
        try
        {
            return Pix.LoadFromMemory(pngBytes);
        }
        catch (Exception)
        {
            var path = Path.Combine(Path.GetTempPath(), "doculens-ocr-" + Guid.NewGuid().ToString("N") + ".jpg");
            File.WriteAllBytes(path, pngBytes);
            try
            {
                return Pix.LoadFromFile(path);
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private static string Collapse(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
