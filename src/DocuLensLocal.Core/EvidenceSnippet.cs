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

    private static string Collapse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return Regex.Replace(text, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
    }
}
