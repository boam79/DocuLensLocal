using System.IO.Compression;

namespace DocuLensLocal.Core;

public static class ZipOfficeMedia
{
    private static readonly string[] ImageExtensions =
    [
        ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".gif",
    ];

    public static IReadOnlyList<byte[]> ReadImages(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            using var zip = OfficeFileAccess.OpenZip(path);
            var images = new List<byte[]>();
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsRasterImage(entry.FullName) || entry.Length < OfficeBodyExtractor.MinImageBytes)
                {
                    continue;
                }

                using var stream = entry.Open();
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                if (memory.Length >= OfficeBodyExtractor.MinImageBytes)
                {
                    images.Add(memory.ToArray());
                }

                if (images.Count >= OfficeBodyExtractor.MaxImages)
                {
                    break;
                }
            }

            return images;
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !OfficeFileAccess.IsTransient(ex))
        {
            return [];
        }
    }

    private static bool IsRasterImage(string name)
    {
        var ext = Path.GetExtension(name);
        return ImageExtensions.Any(item => ext.Equals(item, StringComparison.OrdinalIgnoreCase));
    }
}
