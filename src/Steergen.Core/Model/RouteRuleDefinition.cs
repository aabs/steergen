namespace Steergen.Core.Model;

/// <summary>A single condition-to-destination mapping in a target layout definition.</summary>
public record RouteRuleDefinition
{
    public string Id { get; init; } = "";
    public RouteScope Scope { get; init; } = RouteScope.Both;
    public bool Explicit { get; init; } = false;
    public RouteMatchExpression Match { get; init; } = new();
    public DestinationTemplate Destination { get; init; } = new();
    public RouteAnchor Anchor { get; init; } = RouteAnchor.None;
    public int Order { get; init; }
}
