using OpenMcdf;

namespace DocuLensLocal.Core;

public static class OleEmbeddedImages
{
    public static IReadOnlyList<byte[]> Read(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            using var stream = OfficeFileAccess.OpenRead(path);
            using var root = RootStorage.Open(stream, StorageModeFlags.LeaveOpen);
            var images = new List<byte[]>();
            Collect(root, images, cancellationToken);
            return images;
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !OfficeFileAccess.IsTransient(ex))
        {
            return [];
        }
    }

    private static void Collect(Storage storage, List<byte[]> images, CancellationToken cancellationToken)
    {
        foreach (var entry in storage.EnumerateEntries())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (images.Count >= OfficeBodyExtractor.MaxImages)
            {
                return;
            }

            if (entry.Type == EntryType.Storage)
            {
                Collect(storage.OpenStorage(entry.Name), images, cancellationToken);
                continue;
            }

            if (entry.Type != EntryType.Stream)
            {
                continue;
            }

            using var stream = storage.OpenStream(entry.Name);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            foreach (var image in RasterImageBlobs.Extract(memory.ToArray()))
            {
                if (image.Length < OfficeBodyExtractor.MinImageBytes)
                {
                    continue;
                }

                images.Add(image);
                if (images.Count >= OfficeBodyExtractor.MaxImages)
                {
                    return;
                }
            }
        }
    }
}

internal static class RasterImageBlobs
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] PngIend = [0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82];
    private static readonly byte[] JpegSoi = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] JpegEoi = [0xFF, 0xD9];

    public static bool LooksLikeImage(byte[] data)
    {
        if (data.Length < 8)
        {
            return false;
        }

        return StartsWith(data, PngSignature)
            || StartsWith(data, JpegSoi)
            || (data[0] == (byte)'B' && data[1] == (byte)'M')
            || StartsWith(data, "GIF8"u8);
    }

    public static IReadOnlyList<byte[]> Extract(byte[] data)
    {
        if (LooksLikeImage(data) && data.Length >= OfficeBodyExtractor.MinImageBytes)
        {
            return [data];
        }

        var results = new List<byte[]>();
        for (var i = 0; i < data.Length && results.Count < OfficeBodyExtractor.MaxImages; i++)
        {
            if (MatchAt(data, i, PngSignature))
            {
                var end = IndexOf(data, PngIend, i + PngSignature.Length);
                if (end < 0)
                {
                    continue;
                }

                var length = end + PngIend.Length - i;
                if (length >= OfficeBodyExtractor.MinImageBytes)
                {
                    results.Add(data.AsSpan(i, length).ToArray());
                }

                i = end + PngIend.Length - 1;
                continue;
            }

            if (MatchAt(data, i, JpegSoi))
            {
                var end = IndexOf(data, JpegEoi, i + JpegSoi.Length);
                if (end < 0)
                {
                    continue;
                }

                var length = end + JpegEoi.Length - i;
                if (length >= OfficeBodyExtractor.MinImageBytes)
                {
                    results.Add(data.AsSpan(i, length).ToArray());
                }

                i = end + JpegEoi.Length - 1;
            }
        }

        return results;
    }

    private static bool StartsWith(byte[] data, ReadOnlySpan<byte> prefix) =>
        data.Length >= prefix.Length && data.AsSpan(0, prefix.Length).SequenceEqual(prefix);

    private static bool MatchAt(byte[] data, int index, ReadOnlySpan<byte> value)
    {
        if (index < 0 || index + value.Length > data.Length)
        {
            return false;
        }

        return data.AsSpan(index, value.Length).SequenceEqual(value);
    }

    private static int IndexOf(byte[] data, ReadOnlySpan<byte> value, int start)
    {
        var span = data.AsSpan(start);
        var found = span.IndexOf(value);
        return found < 0 ? -1 : start + found;
    }
}
