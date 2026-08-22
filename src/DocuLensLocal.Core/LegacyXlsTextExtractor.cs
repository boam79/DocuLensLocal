using System.Buffers.Binary;
using System.Text;
using OpenMcdf;

namespace DocuLensLocal.Core;

public static class LegacyXlsTextExtractor
{
    private const ushort SstRecord = 0x00FC;
    private const ushort ContinueRecord = 0x003C;

    public static string Extract(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var file = OfficeFileAccess.OpenRead(path);
            using var root = RootStorage.Open(file, StorageModeFlags.LeaveOpen);
            foreach (var name in new[] { "Workbook", "Book" })
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!root.TryOpenStream(name, out var stream) || stream is null)
                {
                    continue;
                }

                using (stream)
                {
                    var bytes = new byte[stream.Length];
                    stream.ReadExactly(bytes);
                    var text = ReadSst(bytes);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }

            return string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !OfficeFileAccess.IsTransient(ex))
        {
            return string.Empty;
        }
    }

    private static string ReadSst(byte[] bytes)
    {
        var parts = new List<string>();
        var offset = 0;
        while (offset + 4 <= bytes.Length)
        {
            var type = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
            var length = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 2));
            offset += 4;
            if (offset + length > bytes.Length)
            {
                break;
            }

            if (type == SstRecord && length >= 8)
            {
                var buffer = new List<byte>(length);
                buffer.AddRange(bytes.AsSpan(offset, length).ToArray());
                offset += length;
                while (offset + 4 <= bytes.Length)
                {
                    var nextType = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
                    var nextLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 2));
                    if (nextType != ContinueRecord)
                    {
                        break;
                    }

                    offset += 4;
                    if (offset + nextLength > bytes.Length)
                    {
                        break;
                    }

                    buffer.AddRange(bytes.AsSpan(offset, nextLength).ToArray());
                    offset += nextLength;
                }

                parts.AddRange(ReadSstStrings(buffer.ToArray()));
                continue;
            }

            offset += length;
        }

        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
    }

    private static List<string> ReadSstStrings(ReadOnlySpan<byte> data)
    {
        var unique = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);
        var index = 8;
        var texts = new List<string>();
        for (var i = 0; i < unique && index < data.Length; i++)
        {
            var text = ReadXlString(data, ref index);
            if (!string.IsNullOrWhiteSpace(text))
            {
                texts.Add(text.Trim());
            }
        }

        return texts;
    }

    private static string ReadXlString(ReadOnlySpan<byte> data, ref int index)
    {
        if (index + 3 > data.Length)
        {
            return string.Empty;
        }

        var chars = BinaryPrimitives.ReadUInt16LittleEndian(data[index..]);
        index += 2;
        var flags = data[index];
        index += 1;
        var compressed = (flags & 0x01) == 0;
        var phonetic = (flags & 0x04) != 0;
        var rich = (flags & 0x08) != 0;
        var richCount = 0;
        if (rich)
        {
            if (index + 2 > data.Length)
            {
                return string.Empty;
            }

            richCount = BinaryPrimitives.ReadUInt16LittleEndian(data[index..]);
            index += 2;
        }

        if (phonetic)
        {
            if (index + 4 > data.Length)
            {
                return string.Empty;
            }

            index += 4;
        }

        var byteCount = compressed ? chars : chars * 2;
        if (index + byteCount > data.Length)
        {
            return string.Empty;
        }

        var text = compressed
            ? Encoding.Latin1.GetString(data.Slice(index, byteCount))
            : Encoding.Unicode.GetString(data.Slice(index, byteCount));
        index += byteCount;
        if (rich)
        {
            index += Math.Min(richCount * 4, Math.Max(0, data.Length - index));
        }

        return text;
    }
}
