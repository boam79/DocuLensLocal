using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class SearchFormatFilterTests
{
    [Fact]
    public void clicking_the_same_format_again_clears_the_filter()
    {
        Assert.Equal(SearchFormatFilter.Excel, SearchFormatFilters.Toggle(SearchFormatFilter.All, SearchFormatFilter.Excel));
        Assert.Equal(SearchFormatFilter.All, SearchFormatFilters.Toggle(SearchFormatFilter.Excel, SearchFormatFilter.Excel));
        Assert.Equal(SearchFormatFilter.Pdf, SearchFormatFilters.Toggle(SearchFormatFilter.Excel, SearchFormatFilter.Pdf));
    }

    [Fact]
    public void hint_tells_the_user_which_format_is_selected()
    {
        Assert.Contains("종류를 누른 뒤", SearchFormatFilters.Hint(SearchFormatFilter.All), StringComparison.Ordinal);
        Assert.Contains("PDF만", SearchFormatFilters.Hint(SearchFormatFilter.Pdf), StringComparison.Ordinal);
        Assert.Contains("Word만", SearchFormatFilters.Hint(SearchFormatFilter.Word), StringComparison.Ordinal);
        Assert.Contains("한글(HWP)만", SearchFormatFilters.Hint(SearchFormatFilter.Hangul), StringComparison.Ordinal);
        Assert.Contains("Excel만", SearchFormatFilters.Hint(SearchFormatFilter.Excel), StringComparison.Ordinal);
    }

    [Fact]
    public void empty_results_name_the_selected_format()
    {
        var text = SearchStatusFormatter.EmptyResults(276, 276, indexingNow: false, SearchFormatFilter.Excel);

        Assert.Contains("Excel", text, StringComparison.Ordinal);
        Assert.Contains("다시 누르면", text, StringComparison.Ordinal);
        Assert.DoesNotContain("다시 인덱싱", text, StringComparison.Ordinal);
    }
}
