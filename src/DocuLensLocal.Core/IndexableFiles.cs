namespace DocuLensLocal.Core;

public enum IndexableFileKind
{
    Unknown,
    Pdf,
    Docx,
    Doc,
    Hwpx,
    Hwp,
    Xlsx,
    Xlsm,
    Xls,
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
        if (name.StartsWith('~') || name.StartsWith('.'))
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

        if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return IndexableFileKind.Xlsx;
        }

        if (ext.Equals(".xlsm", StringComparison.OrdinalIgnoreCase))
        {
            return IndexableFileKind.Xlsm;
        }

        if (ext.Equals(".xls", StringComparison.OrdinalIgnoreCase))
        {
            return IndexableFileKind.Xls;
        }

        return IndexableFileKind.Unknown;
    }

    public static bool IsIndexable(string path) => KindOf(path) != IndexableFileKind.Unknown;

    public static bool Matches(string path, SearchFormatFilter filter)
    {
        var kind = KindOf(path);
        return filter switch
        {
            SearchFormatFilter.All => true,
            SearchFormatFilter.Pdf => kind == IndexableFileKind.Pdf,
            SearchFormatFilter.Word => kind is IndexableFileKind.Docx or IndexableFileKind.Doc,
            SearchFormatFilter.Hangul => kind is IndexableFileKind.Hwp or IndexableFileKind.Hwpx,
            SearchFormatFilter.Excel => kind is IndexableFileKind.Xlsx or IndexableFileKind.Xlsm or IndexableFileKind.Xls,
            _ => false,
        };
    }

    public static string Badge(string path) => KindOf(path) switch
    {
        IndexableFileKind.Pdf => "PDF",
        IndexableFileKind.Docx => "DOCX",
        IndexableFileKind.Doc => "DOC",
        IndexableFileKind.Hwpx => "HWPX",
        IndexableFileKind.Hwp => "HWP",
        IndexableFileKind.Xlsx => "XLSX",
        IndexableFileKind.Xlsm => "XLSM",
        IndexableFileKind.Xls => "XLS",
        _ => "파일",
    };
}
