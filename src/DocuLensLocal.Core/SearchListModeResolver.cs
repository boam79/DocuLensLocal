namespace DocuLensLocal.Core;

public enum SearchListMode
{
    Idle,
    Hits,
    Empty,
}

public static class SearchListModeResolver
{
    public static SearchListMode Resolve(string? query, bool searchSubmitted, int hitCount)
    {
        if (!searchSubmitted || string.IsNullOrWhiteSpace(query))
        {
            return SearchListMode.Idle;
        }

        return hitCount > 0 ? SearchListMode.Hits : SearchListMode.Empty;
    }
}
