using Steergen.Core.Model;

namespace Steergen.Core.Configuration;

public sealed record CanonicalPackSelector(string Source, string EntryKey, string Raw);

public sealed class PackSelectorResolver
{
    public bool TryParse(string raw, out CanonicalPackSelector selector, out string error)
    {
        selector = default!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Selector is required.";
            return false;
        }

        var splitIndex = FindUnescapedDelimiter(raw, '|');
        if (splitIndex <= 0 || splitIndex >= raw.Length - 1)
        {
            error = "Selector must use the format <source>|<path-or-entry-key>.";
            return false;
        }

        var escapedSource = raw[..splitIndex];
        var escapedEntryKey = raw[(splitIndex + 1)..];

        if (!TryUnescape(escapedSource, out var source) || !TryUnescape(escapedEntryKey, out var entryKey))
        {
            error = "Selector contains an invalid escape sequence. Use \\| for a literal delimiter and \\\\ for a literal backslash.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(entryKey))
        {
            error = "Selector source and entry key must be non-empty.";
            return false;
        }

        selector = new CanonicalPackSelector(source, entryKey, raw);
        return true;
    }

    public bool TryResolveRules(SteeringConfiguration config, CanonicalPackSelector selector, out int index, out string error)
    {
        var matches = config.RulesPacks
            .Select((entry, i) => new { entry, i })
            .Where(x => string.Equals(x.entry.Source, selector.Source, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.entry.Path ?? string.Empty, selector.EntryKey, StringComparison.Ordinal))
            .ToList();

        if (matches.Count == 1)
        {
            index = matches[0].i;
            error = string.Empty;
            return true;
        }

        index = -1;
        error = matches.Count == 0
            ? "Selector does not match any configured rules pack."
            : "Selector is ambiguous and matches multiple configured rules packs.";
        return false;
    }

    public bool TryResolveTemplate(SteeringConfiguration config, CanonicalPackSelector selector, out string error)
    {
        if (config.TemplatePack is null)
        {
            error = "No template pack is configured.";
            return false;
        }

        var entryKey = config.TemplatePack.EntryKey ?? "default";

        if (!string.Equals(config.TemplatePack.Source, selector.Source, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(entryKey, selector.EntryKey, StringComparison.Ordinal))
        {
            error = "Selector does not match the configured template pack.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static int FindUnescapedDelimiter(string text, char delimiter)
    {
        var escaped = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == delimiter)
                return i;
        }

        return -1;
    }

    private static bool TryUnescape(string text, out string value)
    {
        var chars = new List<char>(text.Length);
        var escaped = false;

        foreach (var c in text)
        {
            if (escaped)
            {
                if (c is '\\' or '|')
                {
                    chars.Add(c);
                    escaped = false;
                    continue;
                }

                value = string.Empty;
                return false;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            chars.Add(c);
        }

        if (escaped)
        {
            value = string.Empty;
            return false;
        }

        value = new string(chars.ToArray());
        return true;
    }
}
