using Tesseract;

namespace DocuLensLocal.Core;

public sealed class TesseractLibraryOcrEngine : IOcrEngine
{
    private readonly string? _tessdataDirectory;
    private readonly object _gate = new();

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

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var engine = new TesseractEngine(tessdata, TessdataLocator.ResolveLanguages(tessdata), EngineMode.LstmOnly);
                using var pix = LoadPix(pngBytes);
                using var page = engine.Process(pix);
                return Collapse(page.GetText());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return string.Empty;
            }
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
            var path = Path.Combine(Path.GetTempPath(), "doculens-ocr-" + Guid.NewGuid().ToString("N") + ".png");
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
