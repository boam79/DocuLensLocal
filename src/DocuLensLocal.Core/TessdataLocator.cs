namespace DocuLensLocal.Core;

public static class TessdataLocator
{
    public const string EngFileName = "eng.traineddata";
    public const string KorFileName = "kor.traineddata";

    public static bool HasLanguageData(string? directory) =>
        !string.IsNullOrWhiteSpace(directory)
        && File.Exists(Path.Combine(directory, EngFileName));

    public static string ResolveLanguages(string? directory)
    {
        if (!HasLanguageData(directory))
        {
            return "eng";
        }

        var hasKor = File.Exists(Path.Combine(directory!, KorFileName));
        return hasKor ? "kor+eng" : "eng";
    }

    public static string? FindDirectory(params string?[] extraDirectories)
    {
        foreach (var candidate in Candidates(extraDirectories))
        {
            if (HasLanguageData(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string UserDataDirectory => Path.Combine(AppPaths.UserData, "tessdata");

    public static IEnumerable<string> Candidates(params string?[] extraDirectories)
    {
        foreach (var extra in extraDirectories)
        {
            if (!string.IsNullOrWhiteSpace(extra))
            {
                yield return extra;
            }
        }

        yield return Path.Combine(AppContext.BaseDirectory, "tessdata");

        var prefix = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            yield return prefix;
        }

        yield return UserDataDirectory;
    }
}
