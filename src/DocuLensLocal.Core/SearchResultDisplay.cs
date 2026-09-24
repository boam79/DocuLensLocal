namespace DocuLensLocal.Core;

public static class SearchResultDisplay
{
    public static string FolderName(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return string.Empty;
        }

        var name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? directory : name;
    }

    public static string DateLabel(DateTimeOffset lastWriteUtc) =>
        lastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd");

    public static string LocationLine(string filePath, DateTimeOffset lastWriteUtc)
    {
        var folder = FolderName(filePath);
        var date = DateLabel(lastWriteUtc);
        return string.IsNullOrWhiteSpace(folder) ? date : folder + " · " + date;
    }

    /// <summary>Left-pane rows stay one line so many files fit on screen.</summary>
    public const int ListRowMaxLines = 1;

    public const string DetailHint = "왼쪽에서 고른 파일의 근거입니다. 열기로 엽니다.";

    public static SearchFormatFilter KindGroup(string path)
    {
        var kind = IndexableFiles.KindOf(path);
        return kind switch
        {
            IndexableFileKind.Pdf => SearchFormatFilter.Pdf,
            IndexableFileKind.Docx or IndexableFileKind.Doc => SearchFormatFilter.Word,
            IndexableFileKind.Hwp or IndexableFileKind.Hwpx => SearchFormatFilter.Hangul,
            IndexableFileKind.Xlsx or IndexableFileKind.Xlsm or IndexableFileKind.Xls => SearchFormatFilter.Excel,
            IndexableFileKind.Pptx or IndexableFileKind.Pptm or IndexableFileKind.Ppt => SearchFormatFilter.Ppt,
            _ => SearchFormatFilter.All,
        };
    }
}
