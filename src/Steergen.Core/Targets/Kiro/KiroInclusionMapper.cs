using Steergen.Core.Model;

namespace Steergen.Core.Targets.Kiro;

public static class KiroInclusionMapper
{
    /// <summary>
    /// Determines the Kiro frontmatter inclusion value and optional fileMatchPattern for a document.
    /// If rules carry AppliesTo globs and no explicit pattern is set, fileMatch is inferred.
    /// </summary>
    public static (string Inclusion, string? FileMatchPattern) Map(
        IReadOnlyList<SteeringRule> rules,
        KiroTargetOptions options)
    {
        if (options.InclusionMode == KiroInclusionMode.FileMatch)
        {
            var pattern = options.FileMatchPattern ?? InferPattern(rules);
            return ("fileMatch", pattern);
        }

        var inferredPatterns = rules
            .SelectMany(r => r.AppliesTo)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (inferredPatterns.Count > 0)
            return ("fileMatch", string.Join(", ", inferredPatterns));

        return options.InclusionMode switch
        {
            KiroInclusionMode.Auto => ("auto", null),
            _ => ("always", null),
        };
    }

    private static string? InferPattern(IReadOnlyList<SteeringRule> rules)
    {
        var patterns = rules
            .SelectMany(r => r.AppliesTo)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        return patterns.Count > 0 ? string.Join(", ", patterns) : null;
    }
}
