using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class SearchListModeResolverTests
{
    [Fact]
    public void initial_or_reset_screen_is_idle_even_when_documents_exist()
    {
        Assert.Equal(SearchListMode.Idle, SearchListModeResolver.Resolve("", searchSubmitted: false, hitCount: 276));
        Assert.Equal(SearchListMode.Idle, SearchListModeResolver.Resolve("부대", searchSubmitted: false, hitCount: 10));
        Assert.Equal(SearchListMode.Idle, SearchListModeResolver.Resolve("   ", searchSubmitted: true, hitCount: 276));
    }

    [Fact]
    public void submitted_query_shows_hits_or_empty()
    {
        Assert.Equal(SearchListMode.Hits, SearchListModeResolver.Resolve("부대", searchSubmitted: true, hitCount: 3));
        Assert.Equal(SearchListMode.Empty, SearchListModeResolver.Resolve("부대", searchSubmitted: true, hitCount: 0));
    }
}
