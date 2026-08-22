namespace DocuLensLocal.Core;

[Flags]
public enum SearchFormatFilter
{
    All = 0,
    Pdf = 1,
    Word = 2,
    Hangul = 4,
    Excel = 8,
}

public static class SearchFormatFilters
{
    public static SearchFormatFilter Toggle(SearchFormatFilter current, SearchFormatFilter clicked)
    {
        if (clicked is not (SearchFormatFilter.Pdf or SearchFormatFilter.Word or SearchFormatFilter.Hangul or SearchFormatFilter.Excel))
        {
            return current;
        }

        return current ^ clicked;
    }

    public static bool Includes(SearchFormatFilter current, SearchFormatFilter format) =>
        format != SearchFormatFilter.All && current.HasFlag(format);

    public static IReadOnlyList<string> Labels(SearchFormatFilter filter)
    {
        var labels = new List<string>();
        if (Includes(filter, SearchFormatFilter.Pdf))
        {
            labels.Add("PDF");
        }

        if (Includes(filter, SearchFormatFilter.Word))
        {
            labels.Add("Word");
        }

        if (Includes(filter, SearchFormatFilter.Hangul))
        {
            labels.Add("한글(HWP)");
        }

        if (Includes(filter, SearchFormatFilter.Excel))
        {
            labels.Add("Excel");
        }

        return labels;
    }

    public static string LabelKo(SearchFormatFilter filter)
    {
        var labels = Labels(filter);
        return labels.Count == 0 ? "모든 종류" : string.Join(" · ", labels);
    }

    public static string Hint(SearchFormatFilter filter)
    {
        var labels = Labels(filter);
        if (labels.Count == 0)
        {
            return "종류를 누른 뒤 검색하면 그 파일만 나옵니다. 여러 개를 함께 누를 수 있습니다.";
        }

        if (labels.Count == 1)
        {
            return $"{labels[0]}만 찾습니다. 다른 종류도 누르면 함께 찾습니다.";
        }

        return $"{string.Join(" · ", labels)}만 찾습니다. 다시 누르면 빠집니다.";
    }
}
