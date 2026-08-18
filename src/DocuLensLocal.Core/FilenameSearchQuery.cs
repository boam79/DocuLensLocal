namespace DocuLensLocal.Core;

/// <summary>
/// Turns a typed search box string into filename tokens.
/// Korean fillers/trailing verbs are dropped so "버스 광고 찾아줘" becomes 버스 + 광고.
/// Document text is never sent off the PC.
/// </summary>
public static class FilenameSearchQuery
{
    private static readonly HashSet<string> Fillers = new(StringComparer.OrdinalIgnoreCase)
    {
        "찾아줘",
        "찾아주세요",
        "해줘",
        "해주세요",
        "보여줘",
        "알려줘",
        "관련",
        "문서",
        "좀",
        "검색",
        "검색해줘",
    };

    private static readonly string[] TrailingVerbs =
    [
        "찾아주세요",
        "찾아줘",
        "해주세요",
        "해줘",
        "보여줘",
        "알려줘",
        "검색해줘",
    ];

    public static IReadOnlyList<string> ExtractTokens(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var parts = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tokens = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in parts)
        {
            var token = StripTrailingVerb(part);
            if (string.IsNullOrWhiteSpace(token) || Fillers.Contains(token))
            {
                continue;
            }

            if (seen.Add(token))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    private static string StripTrailingVerb(string token)
    {
        foreach (var verb in TrailingVerbs.OrderByDescending(v => v.Length))
        {
            if (token.Length > verb.Length && token.EndsWith(verb, StringComparison.OrdinalIgnoreCase))
            {
                return token[..^verb.Length];
            }
        }

        return token;
    }
}
