using Scriban;
using Steergen.Core.Model;
using Steergen.Core.Validation;

namespace Steergen.Core.Packs;

/// <summary>
/// Validates template pack content: Scriban syntax correctness and template file name
/// conformance against known template names for declared target IDs.
/// </summary>
public sealed class TemplatePackValidator
{
    /// <summary>
    /// Known template names per built-in target ID.
    /// External (pack-provided) targets use "document" as the default template name.
    /// </summary>
    private static readonly Dictionary<string, IReadOnlySet<string>> KnownTemplateNames =
        new(StringComparer.Ordinal)
        {
            ["kiro"] = new HashSet<string>(StringComparer.Ordinal) { "document" },
            ["speckit"] = new HashSet<string>(StringComparer.Ordinal) { "constitution", "module" },
            ["agents"] = new HashSet<string>(StringComparer.Ordinal) { "copilot.agent", "kiro.agent" },
            ["copilot-agent"] = new HashSet<string>(StringComparer.Ordinal) { "copilot.agent" },
            ["kiro-agent"] = new HashSet<string>(StringComparer.Ordinal) { "kiro.agent" },
        };

    /// <summary>
    /// Validates that the given template content is parseable by the Scriban template engine.
    /// Returns diagnostics for any syntax errors found.
    /// </summary>
    /// <param name="content">The template content string to validate.</param>
    /// <param name="filePath">The file path for diagnostic reporting.</param>
    /// <returns>A list of diagnostics. Empty if the template is valid.</returns>
    public IReadOnlyList<Diagnostic> ValidateTemplateContent(string content, string filePath)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var template = Template.Parse(content, filePath);

        if (!template.HasErrors)
            return [];

        return template.Messages
            .Where(m => m.Type == Scriban.Parsing.ParserMessageType.Error)
            .Select(m => new Diagnostic(
                "TP003",
                $"Scriban syntax error in '{filePath}' at line {m.Span.Start.Line}: {m.Message}",
                DiagnosticSeverity.Error,
                new SourceLocation(filePath, m.Span.Start.Line, m.Span.Start.Column)))
            .ToList();
    }

    /// <summary>
    /// Validates that a template file name matches a known template name for the given target ID.
    /// Returns a warning diagnostic if the file name is not recognized.
    /// </summary>
    /// <param name="templateName">The template name (without .scriban extension).</param>
    /// <param name="targetId">The target ID the template is declared for.</param>
    /// <returns>A list of diagnostics. Empty if the template name is known.</returns>
    public IReadOnlyList<Diagnostic> ValidateTemplateName(string templateName, string targetId)
    {
        ArgumentException.ThrowIfNullOrEmpty(templateName);
        ArgumentException.ThrowIfNullOrEmpty(targetId);

        if (IsKnownTemplateName(templateName, targetId))
            return [];

        return
        [
            new Diagnostic(
                "TP006",
                $"Template file '{templateName}.scriban' does not match a known template name for target '{targetId}'.",
                DiagnosticSeverity.Warning)
        ];
    }

    /// <summary>
    /// Returns true if the template name is a known template name for the given target ID.
    /// Unknown target IDs (e.g., pack-provided targets) accept "document" as the default.
    /// </summary>
    public bool IsKnownTemplateName(string templateName, string targetId)
    {
        if (KnownTemplateNames.TryGetValue(targetId, out var knownNames))
            return knownNames.Contains(templateName);

        // For unknown/external targets, "document" is the conventional default
        return string.Equals(templateName, "document", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the set of known template names for a given target ID.
    /// Returns a set containing only "document" for unknown target IDs.
    /// </summary>
    public IReadOnlySet<string> GetKnownTemplateNames(string targetId)
    {
        if (KnownTemplateNames.TryGetValue(targetId, out var knownNames))
            return knownNames;

        return new HashSet<string>(StringComparer.Ordinal) { "document" };
    }
}
