namespace DocuLensLocal.Core;

public static class IndexFreshness
{
    public static bool CanReuse(IndexedDocument? existing, FileInfo info) =>
        existing is not null
        && !string.IsNullOrWhiteSpace(existing.BodyText)
        && IsUnchanged(existing, info);

    public static bool ShouldSkipOnIncremental(IndexedDocument? existing, FileInfo info, IndexableFileKind kind)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (!IsUnchanged(existing, info))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(existing!.BodyText))
        {
            return true;
        }

        return kind == IndexableFileKind.Pdf;
    }

    public static bool NeedsBodyRetry(IndexedDocument existing, string path)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return string.IsNullOrWhiteSpace(existing.BodyText)
            && IndexableFiles.IsIndexable(path)
            && IndexableFiles.KindOf(path) != IndexableFileKind.Pdf;
    }

    public static bool IsUnchanged(IndexedDocument? existing, FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (existing is null)
        {
            return false;
        }

        var currentMtime = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
        return existing.SizeBytes == info.Length
            && existing.LastWriteTimeUtc.UtcDateTime == currentMtime.UtcDateTime;
    }
}
