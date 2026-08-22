using System.IO.Compression;
using System.Xml;

namespace DocuLensLocal.Core;

public static class ExcelZipTextExtractor
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
            using var zip = OfficeFileAccess.OpenZip(path);
            var shared = ReadSharedStrings(zip, cancellationToken);
            var parts = new List<string>(shared);
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = entry.FullName.Replace('\\', '/');
                if (name.EndsWith("workbook.xml", StringComparison.OrdinalIgnoreCase))
                {
                    parts.AddRange(ReadSheetNames(entry));
                    continue;
                }

                if (!name.Contains("worksheets/sheet", StringComparison.OrdinalIgnoreCase)
                    || !name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                parts.AddRange(ReadSheetValues(entry, shared));
            }

            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !OfficeFileAccess.IsTransient(ex))
        {
            return string.Empty;
        }
    }

    private static List<string> ReadSharedStrings(ZipArchive zip, CancellationToken cancellationToken)
    {
        var entry = zip.Entries.FirstOrDefault(item =>
            item.FullName.Replace('\\', '/').EndsWith("sharedStrings.xml", StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var stream = entry.Open();
        var document = new XmlDocument { XmlResolver = null };
        document.Load(stream);
        var strings = new List<string>();
        foreach (XmlElement si in document.GetElementsByTagName("si"))
        {
            var runs = si.GetElementsByTagName("t")
                .Cast<XmlElement>()
                .Select(node => node.InnerText ?? string.Empty);
            var text = string.Concat(runs).Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                strings.Add(text);
            }
            else
            {
                strings.Add(string.Empty);
            }
        }

        return strings;
    }

    private static List<string> ReadSheetNames(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        var settings = SafeXml();
        using var reader = XmlReader.Create(stream, settings);
        var names = new List<string>();
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "sheet")
            {
                continue;
            }

            var name = reader.GetAttribute("name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name.Trim());
            }
        }

        return names;
    }

    private static List<string> ReadSheetValues(ZipArchiveEntry entry, IReadOnlyList<string> shared)
    {
        using var stream = entry.Open();
        var settings = SafeXml();
        using var reader = XmlReader.Create(stream, settings);
        var values = new List<string>();
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "c")
            {
                continue;
            }

            var type = reader.GetAttribute("t");
            var cell = ReadCell(reader, type, shared);
            if (!string.IsNullOrWhiteSpace(cell))
            {
                values.Add(cell);
            }
        }

        return values;
    }

    private static string ReadCell(XmlReader reader, string? type, IReadOnlyList<string> shared)
    {
        if (reader.IsEmptyElement)
        {
            return string.Empty;
        }

        var depth = reader.Depth;
        string? inline = null;
        string? raw = null;
        while (reader.Read() && reader.Depth > depth)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.LocalName == "t")
            {
                inline = reader.ReadElementContentAsString();
            }
            else if (reader.LocalName == "v")
            {
                raw = reader.ReadElementContentAsString();
            }
        }

        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(raw, out var index)
            && index >= 0
            && index < shared.Count)
        {
            return shared[index];
        }

        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "str", StringComparison.OrdinalIgnoreCase))
        {
            return (inline ?? raw ?? string.Empty).Trim();
        }

        return (raw ?? inline ?? string.Empty).Trim();
    }

    private static XmlReaderSettings SafeXml() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreWhitespace = false,
    };
}
