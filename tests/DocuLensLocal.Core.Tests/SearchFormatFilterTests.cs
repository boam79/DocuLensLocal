using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class SearchFormatFilterTests
{
    [Fact]
    public void clicking_formats_adds_and_removes_them()
    {
        var excel = SearchFormatFilters.Toggle(SearchFormatFilter.All, SearchFormatFilter.Excel);
        Assert.Equal(SearchFormatFilter.Excel, excel);

        var excelAndPdf = SearchFormatFilters.Toggle(excel, SearchFormatFilter.Pdf);
        Assert.Equal(SearchFormatFilter.Excel | SearchFormatFilter.Pdf, excelAndPdf);
        Assert.True(SearchFormatFilters.Includes(excelAndPdf, SearchFormatFilter.Excel));
        Assert.True(SearchFormatFilters.Includes(excelAndPdf, SearchFormatFilter.Pdf));
        Assert.False(SearchFormatFilters.Includes(excelAndPdf, SearchFormatFilter.Word));

        var pdfOnly = SearchFormatFilters.Toggle(excelAndPdf, SearchFormatFilter.Excel);
        Assert.Equal(SearchFormatFilter.Pdf, pdfOnly);

        Assert.Equal(SearchFormatFilter.All, SearchFormatFilters.Toggle(pdfOnly, SearchFormatFilter.Pdf));
    }

    [Fact]
    public void hint_tells_the_user_which_formats_are_selected()
    {
        Assert.Contains("여러 개", SearchFormatFilters.Hint(SearchFormatFilter.All), StringComparison.Ordinal);
        Assert.Contains("PDF만", SearchFormatFilters.Hint(SearchFormatFilter.Pdf), StringComparison.Ordinal);
        Assert.Contains("Word만", SearchFormatFilters.Hint(SearchFormatFilter.Word), StringComparison.Ordinal);
        Assert.Contains("한글(HWP)만", SearchFormatFilters.Hint(SearchFormatFilter.Hangul), StringComparison.Ordinal);
        Assert.Contains("Excel만", SearchFormatFilters.Hint(SearchFormatFilter.Excel), StringComparison.Ordinal);

        var combined = SearchFormatFilters.Hint(SearchFormatFilter.Pdf | SearchFormatFilter.Hangul);
        Assert.Contains("PDF", combined, StringComparison.Ordinal);
        Assert.Contains("한글(HWP)", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Excel", combined, StringComparison.Ordinal);
    }

    [Fact]
    public void empty_results_name_the_selected_formats()
    {
        var one = SearchStatusFormatter.EmptyResults(276, 276, indexingNow: false, SearchFormatFilter.Excel);
        Assert.Contains("Excel", one, StringComparison.Ordinal);
        Assert.Contains("다시 누르면", one, StringComparison.Ordinal);
        Assert.DoesNotContain("다시 인덱싱", one, StringComparison.Ordinal);

        var two = SearchStatusFormatter.EmptyResults(
            276,
            276,
            indexingNow: false,
            SearchFormatFilter.Pdf | SearchFormatFilter.Excel);
        Assert.Contains("PDF", two, StringComparison.Ordinal);
        Assert.Contains("Excel", two, StringComparison.Ordinal);
    }
}
