using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class SearchResultDisplayTests
{
    [Theory]
    [InlineData("/docs/계약서_스캔/내부문서.pdf", "계약서_스캔")]
    [InlineData("/docs/인수인계/견적.xlsx", "인수인계")]
    [InlineData("내부문서.pdf", "")]
    public void folder_name_is_the_parent_directory(string path, string expected)
    {
        Assert.Equal(expected, SearchResultDisplay.FolderName(path));
    }

    [Fact]
    public void location_line_joins_folder_and_local_date()
    {
        var written = new DateTimeOffset(2026, 8, 12, 3, 0, 0, TimeSpan.Zero);

        var line = SearchResultDisplay.LocationLine("/docs/계약서_스캔/내부문서.pdf", written);

        Assert.StartsWith("계약서_스캔 · ", line, StringComparison.Ordinal);
        Assert.Contains(written.ToLocalTime().ToString("yyyy-MM-dd"), line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/docs/a.pdf", SearchFormatFilter.Pdf)]
    [InlineData("/docs/a.docx", SearchFormatFilter.Word)]
    [InlineData("/docs/a.doc", SearchFormatFilter.Word)]
    [InlineData("/docs/a.hwp", SearchFormatFilter.Hangul)]
    [InlineData("/docs/a.hwpx", SearchFormatFilter.Hangul)]
    [InlineData("/docs/a.xlsx", SearchFormatFilter.Excel)]
    [InlineData("/docs/a.xlsm", SearchFormatFilter.Excel)]
    [InlineData("/docs/a.xls", SearchFormatFilter.Excel)]
    public void kind_group_matches_the_format_buttons(string path, SearchFormatFilter expected)
    {
        Assert.Equal(expected, SearchResultDisplay.KindGroup(path));
    }
}

public class LocalFileActionsTests
{
    [Fact]
    public void windows_reveal_selects_the_file_in_explorer()
    {
        var info = LocalFileActions.Reveal("/docs/계약서_스캔/내부문서.pdf", LocalFileOs.Windows);

        Assert.Equal("explorer", info.FileName);
        Assert.Contains("/select,", info.Arguments, StringComparison.Ordinal);
        Assert.Contains("내부문서.pdf", info.Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void mac_reveal_uses_open_r()
    {
        var info = LocalFileActions.Reveal("/docs/내부문서.pdf", LocalFileOs.Mac);

        Assert.Equal("open", info.FileName);
        Assert.Contains("-R", info.ArgumentList);
        Assert.Contains("/docs/내부문서.pdf", info.ArgumentList);
    }

    [Fact]
    public void linux_reveal_opens_the_parent_folder()
    {
        var info = LocalFileActions.Reveal("/docs/계약서_스캔/내부문서.pdf", LocalFileOs.Linux);

        Assert.Equal("xdg-open", info.FileName);
        Assert.Contains("/docs/계약서_스캔", info.ArgumentList);
    }

    [Fact]
    public void open_uses_the_os_file_association()
    {
        var info = LocalFileActions.Open("/docs/내부문서.pdf");

        Assert.Equal("/docs/내부문서.pdf", info.FileName);
        Assert.True(info.UseShellExecute);
    }
}
