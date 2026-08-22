namespace DocuLensLocal.Core;

public enum SearchFormatFilter
{
    All,
    Pdf,
    Word,
    Hangul,
    Excel,
}

public static class SearchFormatFilters
{
    public static SearchFormatFilter Toggle(SearchFormatFilter current, SearchFormatFilter clicked) =>
        current == clicked ? SearchFormatFilter.All : clicked;

    public static string LabelKo(SearchFormatFilter filter) => filter switch
    {
        SearchFormatFilter.Pdf => "PDF",
        SearchFormatFilter.Word => "Word",
        SearchFormatFilter.Hangul => "한글(HWP)",
        SearchFormatFilter.Excel => "Excel",
        _ => "모든 종류",
    };

    public static string Hint(SearchFormatFilter filter) => filter == SearchFormatFilter.All
        ? "종류를 누른 뒤 검색하면 그 파일만 나옵니다"
        : $"{LabelKo(filter)}만 찾습니다. 다시 누르면 모든 종류를 찾습니다.";
}
