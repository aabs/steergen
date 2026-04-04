namespace Steergen.Core.Model;

public record ResolvedSteeringModel
{
    public IReadOnlyList<SteeringDocument> Documents { get; init; } = [];
    public IReadOnlyList<SteeringRule> Rules { get; init; } = [];
    public IReadOnlyList<string> ActiveProfiles { get; init; } = [];
    public IReadOnlyDictionary<string, SteeringDocument> SourceIndex { get; init; } = new Dictionary<string, SteeringDocument>();
}
