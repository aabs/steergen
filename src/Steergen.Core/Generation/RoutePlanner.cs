using Steergen.Core.Model;

namespace Steergen.Core.Generation;

/// <summary>
/// Resolves all steering rules against a target layout, applying the fallback
/// <c>other.*</c> strategy for any rule that no route matches.
/// </summary>
public sealed class RoutePlanner
{
    private readonly RouteResolver _resolver = new();

    /// <summary>
    /// Resolves each rule in <paramref name="rules"/> and returns one
    /// <see cref="RouteResolutionResult"/> per rule, in the same order.
    /// Rules with no matching route are sent to the <c>other.*</c> fallback
    /// destination colocated with the core-anchor route.
    /// </summary>
    public IReadOnlyList<RouteResolutionResult> Plan(
        IReadOnlyList<SteeringRule> rules,
        TargetLayoutDefinition layout)
    {
        return rules
            .Select(rule => ResolveWithFallback(rule, layout))
            .ToList();
    }

    private RouteResolutionResult ResolveWithFallback(SteeringRule rule, TargetLayoutDefinition layout)
    {
        var result = _resolver.Resolve(rule, layout);
        if (result.IsResolved) return result;

        return ApplyFallback(rule, layout, result);
    }

    private static RouteResolutionResult ApplyFallback(
        SteeringRule rule,
        TargetLayoutDefinition layout,
        RouteResolutionResult unresolved)
    {
        var coreAnchor = layout.Routes.FirstOrDefault(r => r.Anchor == RouteAnchor.Core);
        if (coreAnchor is null)
        {
            // Missing core anchor is a validation error; propagate the unresolved result.
            return unresolved with
            {
                SelectionReason = "No routes matched and no core-anchor route found. " +
                                  "Ensure the layout has at least one route with anchor: core.",
            };
        }

        var fallbackName = layout.Fallback.FileBaseName; // e.g. "other"
        var vars = BuildRuleVariables(rule);
        var coreDir = RouteResolver.SubstituteVariables(coreAnchor.Destination.Directory, vars);
        var ext = coreAnchor.Destination.Extension ?? ".md";

        var fallbackPath = string.IsNullOrEmpty(coreDir)
            ? $"{fallbackName}{ext}"
            : $"{coreDir}/{fallbackName}{ext}";

        return unresolved with
        {
            SelectedRouteId = $"<fallback:{coreAnchor.Id}>",
            SelectedDestinationPath = fallbackPath,
            SelectionReason =
                $"No routes matched. Fallback to '{fallbackName}{ext}' " +
                $"at core-anchor '{coreAnchor.Id}' directory.",
            Source = RouteProvenance.Default,
        };
    }

    private static IReadOnlyDictionary<string, string> BuildRuleVariables(SteeringRule rule) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["domain"] = rule.Domain ?? "core",
            ["category"] = rule.Category ?? "",
            ["severity"] = rule.Severity ?? "info",
            ["profile"] = rule.Profile ?? "",
            ["ruleId"] = rule.Id ?? "",
        };
}
