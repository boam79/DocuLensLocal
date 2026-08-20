namespace DocuLensLocal.Core;

public static class IndexWatchPolicy
{
    public static readonly TimeSpan Debounce = TimeSpan.FromSeconds(3);

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

        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext))
        {
            return true;
        }

        return IndexableFiles.IsIndexable(path);
    }
}
