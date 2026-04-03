namespace Steergen.Core.Model;

public record SteeringConfiguration
{
    public string? GlobalRoot { get; init; }
    public string? ProjectRoot { get; init; }
    public IReadOnlyList<string> ActiveProfiles { get; init; } = [];
    public IReadOnlyList<TargetConfiguration> Targets { get; init; } = [];
    public IReadOnlyList<string> RegisteredTargets { get; init; } = [];
    public string? TemplatePackVersion { get; init; }
}

public record TargetConfiguration
{
    public string? Id { get; init; }
    public bool Enabled { get; init; } = true;
    public string? OutputPath { get; init; }
    public Dictionary<string, string> FormatOptions { get; init; } = [];
    public List<string> RequiredMetadata { get; init; } = [];
}
