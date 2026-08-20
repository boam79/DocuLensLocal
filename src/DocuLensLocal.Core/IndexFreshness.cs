namespace DocuLensLocal.Core;

public static class IndexFreshness
{
    public static bool CanReuse(IndexedDocument? existing, FileInfo info) =>
        existing is not null
        && !string.IsNullOrWhiteSpace(existing.BodyText)
        && IsUnchanged(existing, info);

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
