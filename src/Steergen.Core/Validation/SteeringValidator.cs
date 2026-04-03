using Steergen.Core.Model;

namespace Steergen.Core.Validation;

public sealed class SteeringValidator
{
    private static readonly HashSet<string> ValidSeverities =
        new(StringComparer.OrdinalIgnoreCase) { "error", "warning", "info", "hint" };

    public IReadOnlyList<Diagnostic> Validate(SteeringDocument document)
    {
        var diagnostics = new List<Diagnostic>();

        if (string.IsNullOrWhiteSpace(document.Id))
            diagnostics.Add(new Diagnostic("V001", "Document is missing an 'id'.", DiagnosticSeverity.Error));

        foreach (var rule in document.Rules)
        {
            ValidateRule(rule, document.SourcePath, diagnostics);
        }

        return diagnostics;
    }

    private static void ValidateRule(SteeringRule rule, string? sourcePath, List<Diagnostic> diagnostics)
    {
        var location = sourcePath is not null ? new SourceLocation(sourcePath, 0) : null;

        if (string.IsNullOrWhiteSpace(rule.Id))
        {
            diagnostics.Add(new Diagnostic("V002", "Rule is missing an 'id'.", DiagnosticSeverity.Error, location));
        }

        if (!ValidSeverities.Contains(rule.Severity))
        {
            diagnostics.Add(new Diagnostic("V003",
                $"Rule '{rule.Id}' has invalid severity '{rule.Severity}'. Valid: error, warning, info, hint.",
                DiagnosticSeverity.Error, location));
        }

        if (string.IsNullOrWhiteSpace(rule.Domain))
        {
            diagnostics.Add(new Diagnostic("V004",
                $"Rule '{rule.Id}' is missing a 'domain'.",
                DiagnosticSeverity.Error, location));
        }

        if (string.IsNullOrWhiteSpace(rule.PrimaryText))
        {
            diagnostics.Add(new Diagnostic("V005",
                $"Rule '{rule.Id}' has no body text.",
                DiagnosticSeverity.Warning, location));
        }

        if (rule.PrimaryText is not null && ContainsControlCharacters(rule.PrimaryText))
        {
            diagnostics.Add(new Diagnostic("V006",
                $"Rule '{rule.Id}' contains null bytes or control characters in primary text.",
                DiagnosticSeverity.Error, location));
        }
    }

    private static bool ContainsControlCharacters(string text)
    {
        foreach (var ch in text)
        {
            if (ch == '\0' || (ch < 32 && ch != '\n' && ch != '\r' && ch != '\t'))
                return true;
        }
        return false;
    }
}
