namespace Steergen.Core.Model;

/// <summary>
/// Canonical merged layout profile used by routing and write planning for one target.
/// Produced by loading the built-in default layout YAML and optionally deep-merging a user override.
/// </summary>
public record TargetLayoutDefinition
{
    public string TargetId { get; init; } = "";
    public string? Version { get; init; }
    public LayoutRootsDefinition Roots { get; init; } = new();
    public IReadOnlyDictionary<string, VariableDefinition> Variables { get; init; } =
        new Dictionary<string, VariableDefinition>(StringComparer.Ordinal);
    public IReadOnlyList<RouteRuleDefinition> Routes { get; init; } = [];
    public FallbackRuleDefinition Fallback { get; init; } = new();
    public PurgePolicyDefinition? Purge { get; init; }
}
