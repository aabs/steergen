using Steergen.Core.Model;

namespace Steergen.Core.Validation;

public sealed class SteeringValidator
{
    private static readonly HashSet<string> ValidSeverities =
        new(StringComparer.OrdinalIgnoreCase) { "error", "warning", "info", "hint" };

    /// <summary>
    /// Validates a single document in isolation.
    /// </summary>
    public IReadOnlyList<Diagnostic> Validate(SteeringDocument document)
    {
        var diagnostics = new List<Diagnostic>();
        ValidateDocument(document, diagnostics);
        return diagnostics;
    }

    /// <summary>
    /// Validates a corpus of documents, including cross-document checks (duplicate IDs, supersedes references).
    /// Diagnostics are returned in deterministic order: sorted by source path then diagnostic code.
    /// </summary>
    public IReadOnlyList<Diagnostic> ValidateCorpus(IEnumerable<SteeringDocument> documents)
    {
        var docList = documents.ToList();
        var diagnostics = new List<Diagnostic>();

        foreach (var doc in docList)
            ValidateDocument(doc, diagnostics);

        CheckDuplicateRuleIds(docList, diagnostics);
        CheckSupersededRuleReferences(docList, diagnostics);

        return diagnostics
            .OrderBy(d => d.Location?.FilePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(d => d.Code, StringComparer.Ordinal)
            .ToList();
    }

    private static void ValidateDocument(SteeringDocument document, List<Diagnostic> diagnostics)
    {
        var docLocation = document.SourcePath is not null ? new SourceLocation(document.SourcePath, 0) : null;

        if (string.IsNullOrWhiteSpace(document.Id))
            diagnostics.Add(new Diagnostic("V001", "Document is missing an 'id'.", DiagnosticSeverity.Error, docLocation));

        foreach (var rule in document.Rules)
            ValidateRule(rule, document.SourcePath, diagnostics);
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

    private static void CheckDuplicateRuleIds(IReadOnlyList<SteeringDocument> documents, List<Diagnostic> diagnostics)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var doc in documents)
        {
            foreach (var rule in doc.Rules)
            {
                if (rule.Id is null) continue;
                var location = doc.SourcePath is not null ? new SourceLocation(doc.SourcePath, 0) : null;
                if (seen.TryGetValue(rule.Id, out var firstPath))
                {
                    diagnostics.Add(new Diagnostic("V007",
                        $"Rule ID '{rule.Id}' is declared more than once (first in '{firstPath}').",
                        DiagnosticSeverity.Error, location));
                }
                else
                {
                    seen[rule.Id] = doc.SourcePath ?? string.Empty;
                }
            }
        }
    }

    private static void CheckSupersededRuleReferences(IReadOnlyList<SteeringDocument> documents, List<Diagnostic> diagnostics)
    {
        var allIds = documents
            .SelectMany(d => d.Rules)
            .Where(r => r.Id is not null)
            .Select(r => r.Id!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var doc in documents)
        {
            foreach (var rule in doc.Rules)
            {
                if (rule.Supersedes is null) continue;
                var location = doc.SourcePath is not null ? new SourceLocation(doc.SourcePath, 0) : null;
                if (!allIds.Contains(rule.Supersedes))
                {
                    diagnostics.Add(new Diagnostic("V008",
                        $"Rule '{rule.Id}' supersedes '{rule.Supersedes}', but that rule ID does not exist in the corpus.",
                        DiagnosticSeverity.Warning, location));
                }
            }
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
