namespace Steergen.Core.Targets;

internal static class CompactMarkdownFormatter
{
    private static readonly HashSet<string> Acronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "api",
        "ci",
        "css",
        "csv",
        "grpc",
        "html",
        "http",
        "https",
        "json",
        "jwt",
        "oauth",
        "oidc",
        "pii",
        "sdk",
        "siem",
        "sli",
        "slo",
        "sql",
        "tls",
        "ui",
        "uri",
        "url",
        "ux",
        "wcag",
        "xml",
        "yaml",
    };

    public static string FormatRuleText(string? primaryText, string? explanatoryText)
    {
        var primaryLines = ReadLines(primaryText).ToList();
        string? title = null;

        if (primaryLines.Count > 0 && primaryLines[0].StartsWith("title:", StringComparison.OrdinalIgnoreCase))
        {
            title = primaryLines[0]["title:".Length..].Trim();
            primaryLines.RemoveAt(0);
        }

        var bodyLines = primaryLines
            .Concat(ReadLines(explanatoryText))
            .ToList();

        if (bodyLines.Count > 0)
        {
            return string.Join(" ", bodyLines);
        }

        return title ?? string.Empty;
    }

    public static string FormatSectionHeading(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "General";
        }

        var tokens = category
            .Split(['-', '_', '/', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(FormatToken)
            .ToList();

        return tokens.Count == 0 ? "General" : string.Join(" ", tokens);
    }

    private static IEnumerable<string> ReadLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(rawLine))
            {
                yield return rawLine.Trim();
            }
        }
    }

    private static string FormatToken(string token)
    {
        if (Acronyms.Contains(token) || token.Any(char.IsDigit))
        {
            return token.ToUpperInvariant();
        }

        var lower = token.ToLowerInvariant();
        return char.ToUpperInvariant(lower[0]) + lower[1..];
    }
}