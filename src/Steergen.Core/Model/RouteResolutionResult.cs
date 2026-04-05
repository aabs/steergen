namespace Steergen.Core.Model;

/// <summary>Deterministic resolution output for one steering rule against a target layout.</summary>
public record RouteResolutionResult
{
    public string RuleId { get; init; } = "";
    public IReadOnlyList<string> MatchedRouteIds { get; init; } = [];
    public string? SelectedRouteId { get; init; }
    public string? SelectedDestinationPath { get; init; }
    public string SelectionReason { get; init; } = "";
    public RouteProvenance Source { get; init; } = RouteProvenance.Default;
    public bool IsResolved => SelectedRouteId is not null;
}
