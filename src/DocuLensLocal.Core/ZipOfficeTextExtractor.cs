using System.IO.Compression;
using System.Xml;

namespace DocuLensLocal.Core;

public static class ZipOfficeTextExtractor
{
    public static string Extract(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            using var zip = ZipFile.OpenRead(path);
            var parts = new List<string>();
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var stream = entry.Open();
                parts.AddRange(ReadTextNodes(stream));
            }

            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return string.Empty;
        }
    }

    private static IEnumerable<string> ReadTextNodes(Stream stream)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = false,
        };

        using var reader = XmlReader.Create(stream, settings);
        var texts = new List<string>();
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "t")
            {
                continue;
            }

            var text = reader.ReadElementContentAsString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                texts.Add(text.Trim());
            }
        }

        return texts;
    }
}
