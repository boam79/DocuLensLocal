namespace DocuLensLocal.Core;

public static class IndexFolderList
{
    public static IReadOnlyList<string> FromSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var raw = new List<string?>();
        if (!string.IsNullOrWhiteSpace(settings.IndexFolder))
        {
            raw.Add(settings.IndexFolder);
        }

        if (settings.IndexFolders is not null)
        {
            raw.AddRange(settings.IndexFolders);
        }

        return Normalize(raw);
    }

    public static void Apply(AppSettings settings, IEnumerable<string?> folders)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = Normalize(folders);
        settings.IndexFolders = normalized.ToList();
        settings.IndexFolder = normalized.Count > 0 ? normalized[0] : null;
    }

    public static IReadOnlyList<string> Add(IEnumerable<string?> existing, params string?[] added) =>
        Normalize((existing ?? []).Concat(added ?? []));

    public static IReadOnlyList<string> Remove(IEnumerable<string?> existing, string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return Normalize(existing);
        }

        return Normalize((existing ?? []).Where(path =>
            !string.IsNullOrWhiteSpace(path)
            && !path.Trim().Equals(folder.Trim(), StringComparison.OrdinalIgnoreCase)
            && !SamePath(path, folder)));
    }

    public static IReadOnlyList<string> Normalize(params string?[] folders) =>
        Normalize((IEnumerable<string?>)folders);

    public static IReadOnlyList<string> Normalize(IEnumerable<string?>? folders)
    {
        var unique = new List<string>();
        if (folders is null)
        {
            return unique;
        }

        foreach (var raw in folders)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var path = Canonical(raw);
            if (unique.Any(existing => SamePath(existing, path)))
            {
                continue;
            }

            unique.Add(path);
        }

        return CollapseNested(unique);
    }

    public static IReadOnlyList<string> Existing(IEnumerable<string?>? folders) =>
        Normalize(folders).Where(Directory.Exists).ToList();

    public static bool AnyExists(IEnumerable<string?>? folders) =>
        Existing(folders).Count > 0;

    public static bool SamePath(string left, string right) =>
        string.Equals(TrimSep(Canonical(left)), TrimSep(Canonical(right)), StringComparison.OrdinalIgnoreCase);

    public static bool IsInside(string path, string folder)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        var root = TrimSep(WithCommonSeparators(Canonical(folder)));
        var full = TrimSep(WithCommonSeparators(Canonical(path)));
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = root + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string HeaderLine(IReadOnlyList<string> folders)
    {
        if (folders is null || folders.Count == 0)
        {
            return InfoStatusCopy.NoFolder;
        }

        if (folders.Count == 1)
        {
            return folders[0];
        }

        return $"{folders[0]} 외 {folders.Count - 1}개";
    }

    public static string ListLine(IReadOnlyList<string> folders)
    {
        if (folders is null || folders.Count == 0)
        {
            return InfoStatusCopy.NoFolder;
        }

        return string.Join(Environment.NewLine, folders);
    }

    private static List<string> CollapseNested(List<string> folders)
    {
        return folders.Where(path =>
            !folders.Any(other => !SamePath(path, other) && IsInside(path, other))).ToList();
    }

    private static string Canonical(string path)
    {
        var trimmed = path.Trim();
        try
        {
            if (Directory.Exists(trimmed))
            {
                return Path.GetFullPath(trimmed);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return trimmed;
        }

        return trimmed;
    }

    private static string WithCommonSeparators(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    private static string TrimSep(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\');
}
