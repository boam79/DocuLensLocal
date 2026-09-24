using System.Text.RegularExpressions;

namespace DocuLensLocal.Core;

public static class EvidenceSnippet
{
    public static string From(string? bodyText, IReadOnlyList<string> tokens, int radius = 52)
    {
        var collapsed = Collapse(bodyText);
        if (collapsed.Length == 0)
        {
            return string.Empty;
        }

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var index = collapsed.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            var start = Math.Max(0, index - radius);
            var end = Math.Min(collapsed.Length, index + token.Length + radius);
            var slice = collapsed[start..end];
            if (start > 0)
            {
                slice = "…" + slice;
            }

            if (end < collapsed.Length)
            {
                slice += "…";
            }

            return slice;
        }

        return collapsed.Length <= 120 ? collapsed : collapsed[..117] + "…";
    }

    public static IReadOnlyList<SnippetSpan> Highlight(string? text, IReadOnlyList<string> tokens)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var needles = tokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(token => token.Length)
            .ToList();
        if (needles.Count == 0)
        {
            return [new SnippetSpan(text, false)];
        }

        var covered = new bool[text.Length];
        foreach (var needle in needles)
        {
            var index = 0;
            while (index < text.Length)
            {
                var found = text.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    break;
                }

                for (var i = 0; i < needle.Length; i++)
                {
                    covered[found + i] = true;
                }

                index = found + 1;
            }
        }

        var spans = new List<SnippetSpan>();
        var start = 0;
        while (start < text.Length)
        {
            var hit = covered[start];
            var end = start + 1;
            while (end < text.Length && covered[end] == hit)
            {
                end++;
            }

            spans.Add(new SnippetSpan(text[start..end], hit));
            start = end;
        }

        return spans;
    }

    private static string Collapse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return Regex.Replace(text, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
    }
}

public sealed record SnippetSpan(string Text, bool IsHit);
