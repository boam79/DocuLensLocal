using System.IO.Compression;

namespace DocuLensLocal.Core;

public static class OfficeFileAccess
{
    public const FileShare ReadShare = FileShare.ReadWrite | FileShare.Delete;

    public static FileStream OpenRead(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            ReadShare,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
    }

    public static ZipArchive OpenZip(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var stream = OpenRead(path);
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public static bool IsTransient(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or System.Security.SecurityException;
}
