namespace Steergen.Core.Model;

public record SteeringRule
{
    public string? Id { get; init; }
    public string Severity { get; init; } = "info";
    public string? Category { get; init; }
    public string Domain { get; init; } = "core";
    public string? Profile { get; init; }
    public IReadOnlyList<string> AppliesTo { get; init; } = [];
    public IReadOnlyList<string> Tags { get; init; } = [];
    public bool Deprecated { get; init; }
    public string? Supersedes { get; init; }
    public string? PrimaryText { get; init; }
    public string? ExplanatoryText { get; init; }
}
