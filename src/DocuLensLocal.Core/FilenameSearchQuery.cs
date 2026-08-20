namespace DocuLensLocal.Core;

/// <summary>
/// Turns a typed search box string into filename/body tokens.
/// Korean fillers/trailing verbs are dropped so "버스 광고 찾아줘" and "버스광고 찾아줘"
/// both become 버스 + 광고. Document text is never sent off the PC.
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

            foreach (var piece in ExpandHangulCompound(token))
            {
                if (Fillers.Contains(piece))
                {
                    continue;
                }

                if (seen.Add(piece))
                {
                    tokens.Add(piece);
                }
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

    /// <summary>
    /// "버스광고" (typed without a space) must become 버스 + 광고, same as the spaced query.
    /// </summary>
    internal static IReadOnlyList<string> ExpandHangulCompound(string token)
    {
        if (token.Length < 4 || !token.All(IsHangulSyllable))
        {
            return [token];
        }

        var chunks = new List<string>();
        var index = 0;
        while (index < token.Length)
        {
            var remaining = token.Length - index;
            var take = remaining == 3 ? 3 : 2;
            chunks.Add(token.Substring(index, take));
            index += take;
        }

        return chunks.Count >= 2 ? chunks : [token];
    }

    private static bool IsHangulSyllable(char value) => value is >= '\uAC00' and <= '\uD7A3';
}
