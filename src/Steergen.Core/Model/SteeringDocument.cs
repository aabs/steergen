namespace Steergen.Core.Model;

public record SteeringDocument
{
    public string? Id { get; init; }
    public string? Version { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> Profiles { get; init; } = [];
    public IReadOnlyList<SteeringRule> Rules { get; init; } = [];
    public string? SourcePath { get; init; }
}
