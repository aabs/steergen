using System.Text;
using System.Text.RegularExpressions;
using Steergen.Core.Model;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Steergen.Core.Parsing;

public static class SteeringMarkdownParser
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly Regex RuleOpenRegex = new(
        @"^:::rule\s+(.*?)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex AttributeRegex = new(
        @"(\w+)=""([^""]*)""",
        RegexOptions.Compiled);

    public static SteeringDocument Parse(string content, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new SteeringDocument { SourcePath = sourcePath };

        var (frontmatter, body) = SplitFrontmatter(content);

        SteeringDocumentYaml? yaml = null;
        if (!string.IsNullOrWhiteSpace(frontmatter))
        {
            try
            {
                yaml = YamlDeserializer.Deserialize<SteeringDocumentYaml>(frontmatter);
            }
            catch
            {
                yaml = null;
            }
        }

        var rules = ParseRules(body);

        return new SteeringDocument
        {
            Id = yaml?.Id,
            Version = yaml?.Version,
            Title = yaml?.Title,
            Description = yaml?.Description,
            Tags = yaml?.Tags ?? [],
            Profiles = yaml?.Profiles ?? [],
            Rules = rules,
            SourcePath = sourcePath,
        };
    }

    private static (string frontmatter, string body) SplitFrontmatter(string content)
    {
        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith("---", StringComparison.Ordinal))
            return (string.Empty, content);

        var firstEnd = trimmed.IndexOf('\n');
        if (firstEnd < 0) return (string.Empty, content);

        var afterFirst = trimmed[(firstEnd + 1)..];
        var secondDash = afterFirst.IndexOf("\n---", StringComparison.Ordinal);
        if (secondDash < 0) return (string.Empty, content);

        var frontmatter = afterFirst[..secondDash];
        var body = afterFirst[(secondDash + 4)..];
        if (body.Length > 0 && body[0] == '\n')
            body = body[1..];
        return (frontmatter, body);
    }

    private static IReadOnlyList<SteeringRule> ParseRules(string body)
    {
        var rules = new List<SteeringRule>();
        var lines = body.Split('\n');
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            var match = RuleOpenRegex.Match(line);
            if (match.Success)
            {
                var attrString = match.Groups[1].Value;
                var bodyBuilder = new StringBuilder();
                i++;
                while (i < lines.Length && lines[i].TrimEnd() != ":::")
                {
                    bodyBuilder.AppendLine(lines[i]);
                    i++;
                }
                var primaryText = bodyBuilder.ToString().Trim();
                var rule = ParseRuleAttributes(attrString, primaryText);
                rules.Add(rule);
            }
            i++;
        }
        return rules;
    }

    private static SteeringRule ParseRuleAttributes(string attrString, string primaryText)
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in AttributeRegex.Matches(attrString))
            attrs[m.Groups[1].Value] = m.Groups[2].Value;

        attrs.TryGetValue("appliesTo", out var appliesToRaw);
        attrs.TryGetValue("tags", out var tagsRaw);

        var appliesTo = string.IsNullOrWhiteSpace(appliesToRaw)
            ? (IReadOnlyList<string>)[]
            : appliesToRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var tags = string.IsNullOrWhiteSpace(tagsRaw)
            ? (IReadOnlyList<string>)[]
            : tagsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        attrs.TryGetValue("deprecated", out var deprecatedRaw);
        var deprecated = string.Equals(deprecatedRaw, "true", StringComparison.OrdinalIgnoreCase);

        return new SteeringRule
        {
            Id = attrs.TryGetValue("id", out var id) ? id : null,
            Severity = attrs.TryGetValue("severity", out var sev) ? sev : "info",
            Category = attrs.TryGetValue("category", out var cat) ? cat : null,
            Domain = attrs.TryGetValue("domain", out var dom) ? dom : "core",
            Profile = attrs.TryGetValue("profile", out var prof) ? prof : null,
            AppliesTo = appliesTo,
            Tags = tags,
            Deprecated = deprecated,
            Supersedes = attrs.TryGetValue("supersedes", out var sup) ? sup : null,
            PrimaryText = primaryText,
        };
    }

    private sealed class SteeringDocumentYaml
    {
        public string? Id { get; set; }
        public string? Version { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<string> Tags { get; set; } = [];
        public List<string> Profiles { get; set; } = [];
    }
}
