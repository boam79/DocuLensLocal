using System.Diagnostics;

namespace DocuLensLocal.Core;

public sealed class TesseractCliOcrEngine : IOcrEngine
{
    private readonly string? _executable;
    private readonly string _languages;

    public TesseractCliOcrEngine()
        : this(FindExecutable(), ResolveLanguages(FindExecutable()))
    {
    }

    public TesseractCliOcrEngine(string? executable, string languages)
    {
        _executable = executable;
        _languages = string.IsNullOrWhiteSpace(languages) ? "eng" : languages;
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_executable);

    public static bool IsOnPath => !string.IsNullOrWhiteSpace(FindExecutable());

    public string RecognizePng(byte[] pngBytes, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return string.Empty;
        }

        ArgumentNullException.ThrowIfNull(pngBytes);
        var pngPath = Path.Combine(Path.GetTempPath(), "doculens-ocr-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(pngPath, pngBytes);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = new ProcessStartInfo
            {
                FileName = _executable,
                ArgumentList = { pngPath, "stdout", "-l", _languages, "--psm", "6" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(start);
            if (process is null)
            {
                return string.Empty;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(60_000);
            return Collapse(output);
        }
        finally
        {
            TryDelete(pngPath);
        }
    }

    internal static string? FindExecutable()
    {
        var fromEnv = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
        _ = fromEnv;
        var names = OperatingSystem.IsWindows()
            ? new[] { "tesseract.exe", "tesseract" }
            : new[] { "tesseract" };
        foreach (var name in names)
        {
            var found = FindOnPath(name);
            if (found is not null)
            {
                return found;
            }
        }

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var bundled = Path.Combine(programFiles, "Tesseract-OCR", "tesseract.exe");
            if (File.Exists(bundled))
            {
                return bundled;
            }
        }

        return null;
    }

    internal static string ResolveLanguages(string? executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            return "eng";
        }

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = executable,
                ArgumentList = { "--list-langs" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(start);
            if (process is null)
            {
                return "eng";
            }

            var text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(10_000);
            var hasKor = text.Contains("kor", StringComparison.OrdinalIgnoreCase);
            var hasEng = text.Contains("eng", StringComparison.OrdinalIgnoreCase);
            if (hasKor && hasEng)
            {
                return "kor+eng";
            }

            if (hasKor)
            {
                return "kor";
            }
        }
        catch (Exception)
        {
        }

        return "eng";
    }

    private static string? FindOnPath(string name)
    {
        if (File.Exists(name))
        {
            return Path.GetFullPath(name);
        }

        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in paths)
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string Collapse(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static void TryDelete(string path)
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
