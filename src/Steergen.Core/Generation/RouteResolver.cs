using Steergen.Core.Model;

namespace Steergen.Core.Generation;

/// <summary>
/// Resolves a single <see cref="SteeringRule"/> to a deterministic route in a
/// <see cref="TargetLayoutDefinition"/> using an explicit precedence tuple:
/// (explicit, conditionSpecificity, declarationOrder, routeId).
/// </summary>
public sealed class RouteResolver
{
    /// <summary>
    /// Resolves <paramref name="rule"/> against <paramref name="layout"/>.
    /// Returns an unresolved result if no route matches.
    /// </summary>
    public RouteResolutionResult Resolve(SteeringRule rule, TargetLayoutDefinition layout)
    {
        var candidates = layout.Routes
            .Select((route, index) => (route, index))
            .Where(x => ScopeMatches(x.route.Scope, rule.SourceScope))
            .Where(x => Matches(x.route.Match, rule))
            .ToList();

        if (candidates.Count == 0)
            return new RouteResolutionResult
            {
                RuleId = rule.Id ?? "",
                MatchedRouteIds = [],
                SelectedRouteId = null,
                SelectedDestinationPath = null,
                SelectionReason = "No routes matched the rule's metadata.",
                Source = RouteProvenance.Default,
            };

        var selected = candidates
            .OrderByDescending(x => x.route.Explicit ? 1 : 0)
            .ThenByDescending(x => ConditionSpecificity(x.route.Match))
            .ThenBy(x => x.route.Order)
            .ThenBy(x => x.index)
            .ThenBy(x => x.route.Id, StringComparer.Ordinal)
            .First();

        var destination = ResolveDestination(selected.route.Destination, rule);

        return new RouteResolutionResult
        {
            RuleId = rule.Id ?? "",
            MatchedRouteIds = candidates.Select(x => x.route.Id).ToList(),
            SelectedRouteId = selected.route.Id,
            SelectedDestinationPath = destination,
            SelectionReason =
                $"Selected route '{selected.route.Id}' " +
                $"(explicit={selected.route.Explicit}, " +
                $"specificity={ConditionSpecificity(selected.route.Match)}, " +
                $"order={selected.route.Order}).",
            Source = RouteProvenance.Default,
        };
    }

    // ── Match evaluation ────────────────────────────────────────────────────────

    internal static bool Matches(RouteMatchExpression expr, SteeringRule rule)
    {
        if (expr.IsEmpty) return true;

        if (!MatchesField(expr.Domain, rule.Domain)) return false;
        if (!MatchesField(expr.Category, rule.Category)) return false;
        if (!MatchesField(expr.Severity, rule.Severity)) return false;
        if (!MatchesField(expr.Profile, rule.Profile)) return false;
        if (!MatchesTagsAny(expr.TagsAny, rule.Tags)) return false;

        return true;
    }

    internal static bool ScopeMatches(RouteScope routeScope, RouteScope ruleScope) =>
        routeScope == RouteScope.Both || ruleScope == RouteScope.Both || routeScope == ruleScope;

    private static bool MatchesField(IReadOnlyList<string> filter, string? value)
    {
        if (filter.Count == 0) return true;
        if (filter.Contains("*", StringComparer.Ordinal)) return true;
        if (value is null) return false;
        return filter.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    private static bool MatchesTagsAny(IReadOnlyList<string> filter, IReadOnlyList<string> tags)
    {
        if (filter.Count == 0) return true;
        if (filter.Contains("*", StringComparer.Ordinal)) return true;
        return tags.Any(t => filter.Contains(t, StringComparer.OrdinalIgnoreCase));
    }

    // ── Specificity scoring ─────────────────────────────────────────────────────

    internal static int ConditionSpecificity(RouteMatchExpression expr)
    {
        return FieldSpecificity(expr.Domain)
             + FieldSpecificity(expr.Category)
             + FieldSpecificity(expr.Severity)
             + FieldSpecificity(expr.Profile)
             + FieldSpecificity(expr.TagsAny);
    }

    private static int FieldSpecificity(IReadOnlyList<string> field)
    {
        if (field.Count == 0) return 0;
        if (field.All(v => v == "*")) return 1; // wildcard — matches anything, low specificity
        return 2; // concrete literal constraint
    }

    // ── Destination template resolution ─────────────────────────────────────────

    internal static string ResolveDestination(DestinationTemplate dest, SteeringRule rule)
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["domain"] = rule.Domain ?? "core",
            ["category"] = rule.Category ?? "",
            ["severity"] = rule.Severity ?? "info",
            ["profile"] = rule.Profile ?? "",
            ["ruleId"] = rule.Id ?? "",
            ["inputFileStem"] = rule.InputFileStem ?? rule.Id ?? "",
        };

        var dir = SubstituteVariables(dest.Directory, vars);
        var file = SubstituteVariables(dest.FileName, vars);
        var ext = dest.Extension ?? ".md";

        return string.IsNullOrEmpty(dir) ? $"{file}{ext}" : $"{dir}/{file}{ext}";
    }

    internal static string SubstituteVariables(string template, IReadOnlyDictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(template)) return template;

        // Replace ${key} tokens for known vars; leave unknown tokens intact.
        var result = template;
        foreach (var (key, value) in vars)
            result = result.Replace($"${{{key}}}", value, StringComparison.OrdinalIgnoreCase);
        return result;
    }
}
