namespace DocuLensLocal.Core;

public enum IndexableFileKind
{
    Unknown,
    Pdf,
    Docx,
    Doc,
    Hwpx,
    Hwp,
}

public static class IndexableFiles
{
    public static IndexableFileKind KindOf(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return IndexableFileKind.Unknown;
        }

        var name = Path.GetFileName(path);
        if (name.StartsWith("~$", StringComparison.Ordinal) || name.StartsWith('.'))
        {
            return IndexableFileKind.Unknown;
        }

        var ext = Path.GetExtension(path);
        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return IndexableFileKind.Pdf;
        }

        if (ext.Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            return IndexableFileKind.Docx;
        }

        if (ext.Equals(".doc", StringComparison.OrdinalIgnoreCase))
        {
            return IndexableFileKind.Doc;
        }

        if (ext.Equals(".hwpx", StringComparison.OrdinalIgnoreCase))
        {
            return IndexableFileKind.Hwpx;
        }

        if (ext.Equals(".hwp", StringComparison.OrdinalIgnoreCase))
        {
            return IndexableFileKind.Hwp;
        }

        return IndexableFileKind.Unknown;
    }

    public static bool IsIndexable(string path) => KindOf(path) != IndexableFileKind.Unknown;

    public static string Badge(string path) => KindOf(path) switch
    {
        IndexableFileKind.Pdf => "PDF",
        IndexableFileKind.Docx => "DOCX",
        IndexableFileKind.Doc => "DOC",
        IndexableFileKind.Hwpx => "HWPX",
        IndexableFileKind.Hwp => "HWP",
        _ => "파일",
    };
}
