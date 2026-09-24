using System.Buffers.Binary;
using System.Text;
using OpenMcdf;

namespace DocuLensLocal.Core;

public static class LegacyPptTextExtractor
{
    private const ushort TextCharsAtom = 0x0FA0;
    private const ushort TextBytesAtom = 0x0FA8;

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
            if (!root.TryOpenStream("PowerPoint Document", out var stream) || stream is null)
            {
                return string.Empty;
            }

            using (stream)
            {
                if (stream.Length < 8)
                {
                    return string.Empty;
                }

                var bytes = new byte[stream.Length];
                stream.ReadExactly(bytes);
                var parts = new List<string>();
                Walk(bytes, parts, cancellationToken);
                return Clean(string.Join(" ", parts));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !OfficeFileAccess.IsTransient(ex))
        {
            return string.Empty;
        }
    }

    private static void Walk(ReadOnlySpan<byte> data, List<string> parts, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset + 8 <= data.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var header = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset));
            var recType = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset + 2));
            var recLen = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4));
            var recVer = header & 0xF;
            offset += 8;
            if (recLen > (uint)(data.Length - offset))
            {
                break;
            }

            var body = data.Slice(offset, (int)recLen);
            if (recVer == 0xF)
            {
                Walk(body, parts, cancellationToken);
            }
            else if (recType == TextCharsAtom && recLen >= 2)
            {
                parts.Add(Encoding.Unicode.GetString(body));
            }
            else if (recType == TextBytesAtom && recLen >= 1)
            {
                parts.Add(Encoding.Latin1.GetString(body));
            }

            offset += (int)recLen;
        }
    }

    private static string Clean(string raw)
    {
        var builder = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (ch is '\r' or '\n')
            {
                builder.Append('\n');
                continue;
            }

            if (char.IsControl(ch))
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(ch);
        }

        return string.Join(" ", builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
