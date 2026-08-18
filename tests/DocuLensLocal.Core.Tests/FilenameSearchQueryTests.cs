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
    public void strips_trailing_verb_glued_to_last_token()
    {
        var tokens = FilenameSearchQuery.ExtractTokens("버스광고찾아줘");

        Assert.Equal(["버스광고"], tokens.ToArray());
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
