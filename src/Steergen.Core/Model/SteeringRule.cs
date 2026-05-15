namespace Steergen.Core.Model;

public record SteeringRule
{
    public string? Id { get; init; }
    public RouteScope SourceScope { get; init; } = RouteScope.Both;
    public bool Mandatory { get; init; } = false;
    public string? Category { get; init; }
    public IReadOnlyList<string> AppliesTo { get; init; } = [];
    public IReadOnlyList<string> Tags { get; init; } = [];
    public bool Deprecated { get; init; }
    public string? PrimaryText { get; init; }
    public string? ExplanatoryText { get; init; }
    /// <summary>
    /// The file stem of the source document from which this rule originated.
    /// Set during model resolution; used for <c>${inputFileStem}</c> route substitution.
    /// </summary>
    public string? InputFileStem { get; init; }
}
