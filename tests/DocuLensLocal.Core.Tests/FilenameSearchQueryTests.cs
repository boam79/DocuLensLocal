using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class FilenameSearchQueryTests
{
    [Fact]
    public void strips_korean_fillers_and_trailing_verbs()
    {
        var tokens = FilenameSearchQuery.ExtractTokens("버스 광고 찾아줘");

        Assert.Equal(["버스", "광고"], tokens.ToArray());
    }

    [Fact]
    public void splits_concatenated_hangul_the_same_as_spaced_query()
    {
        Assert.Equal(["버스", "광고"], FilenameSearchQuery.ExtractTokens("버스광고 찾아줘").ToArray());
        Assert.Equal(["버스", "광고"], FilenameSearchQuery.ExtractTokens("버스광고찾아줘").ToArray());
    }

    [Fact]
    public void keeps_english_keyword_like_mou()
    {
        var tokens = FilenameSearchQuery.ExtractTokens("mou");

        Assert.Equal(["mou"], tokens.ToArray());
    }

    [Fact]
    public void filler_only_query_yields_no_tokens()
    {
        Assert.Empty(FilenameSearchQuery.ExtractTokens("찾아줘"));
        Assert.Empty(FilenameSearchQuery.ExtractTokens("문서"));
    }
}
