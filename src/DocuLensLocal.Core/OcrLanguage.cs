namespace DocuLensLocal.Core;

public static class OcrLanguage
{
    public const int FallbackMinLetters = 12;

    public static string Primary(string? directory)
    {
        if (!string.IsNullOrWhiteSpace(directory)
            && File.Exists(Path.Combine(directory, TessdataLocator.KorFileName)))
        {
            return "kor";
        }

        return "eng";
    }

    public static string? Fallback(string? directory, string primary)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        if (primary == "kor" && File.Exists(Path.Combine(directory, TessdataLocator.EngFileName)))
        {
            return "eng";
        }

        if (primary == "eng" && File.Exists(Path.Combine(directory, TessdataLocator.KorFileName)))
        {
            return "kor";
        }

        return null;
    }

    public static bool ShouldTryFallback(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var letters = 0;
        foreach (var ch in text)
        {
            if (char.IsLetter(ch))
            {
                letters++;
                if (letters >= FallbackMinLetters)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
