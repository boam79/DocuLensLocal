using System.Buffers.Binary;
using System.Text;
using OpenMcdf;

namespace DocuLensLocal.Core;

public static class LegacyDocTextExtractor
{
    private const ushort WordIdent = 0xA5EC;

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
            using var root = RootStorage.OpenRead(path);
            if (!root.TryOpenStream("WordDocument", out var stream) || stream is null)
            {
                return string.Empty;
            }

            using (stream)
            {
                if (stream.Length < 32)
                {
                    return string.Empty;
                }

                var header = new byte[32];
                stream.ReadExactly(header);
                if (BinaryPrimitives.ReadUInt16LittleEndian(header) != WordIdent)
                {
                    return string.Empty;
                }

                var fcMin = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0x18));
                var fcMac = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0x1C));
                if (fcMin < 0 || fcMac <= fcMin || fcMac > stream.Length)
                {
                    return string.Empty;
                }

                var length = fcMac - fcMin;
                if (length < 2 || length % 2 != 0)
                {
                    return string.Empty;
                }

                stream.Position = fcMin;
                var bytes = new byte[length];
                stream.ReadExactly(bytes);
                return Clean(Encoding.Unicode.GetString(bytes));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return string.Empty;
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

            if (char.IsControl(ch) || ch == '\u0007' || ch == '\u000b' || ch == '\u000c')
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(ch);
        }

        return string.Join(" ", builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
