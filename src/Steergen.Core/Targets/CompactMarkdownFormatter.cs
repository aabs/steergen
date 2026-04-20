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
        if (!string.IsNullOrEmpty(primaryText) && !string.IsNullOrEmpty(explanatoryText))
        {
            return string.Concat(primaryText, "\n\n", explanatoryText);
        }

        if (!string.IsNullOrEmpty(primaryText))
        {
            return primaryText;
        }

        return explanatoryText ?? string.Empty;
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