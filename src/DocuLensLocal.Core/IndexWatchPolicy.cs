namespace DocuLensLocal.Core;

public static class IndexWatchPolicy
{
    public static readonly TimeSpan Debounce = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Watch every file, including Excel temps with no extension. The default
    /// FileSystemWatcher filter <c>*.*</c> misses those save-as files.
    /// </summary>
    public const string FileWatcherFilter = "*";

    private static readonly string[] StagingExtensions =
    [
        ".tmp",
        ".crdownload",
        ".partial",
        ".download",
    ];

    public static bool ShouldWatchFolder(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.IndexCompleted
            && !string.IsNullOrWhiteSpace(settings.IndexFolder)
            && Directory.Exists(settings.IndexFolder);
    }

    public static bool ShouldWatchPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        if (Directory.Exists(path))
        {
            return true;
        }

        var name = Path.GetFileName(path);
        if (name.StartsWith("~$", StringComparison.Ordinal))
        {
            return true;
        }

        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext) || StagingExtensions.Any(item => ext.Equals(item, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return IndexableFiles.IsIndexable(path);
    }
}
