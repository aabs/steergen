namespace Steergen.Core.Model;

public record SteeringConfiguration
{
    public string? GlobalRoot { get; init; }
    public string? ProjectRoot { get; init; }
    public string? GenerationRoot { get; init; }
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
    /// <summary>
    /// Optional path to a user-provided layout override YAML for this target.
    /// When set, the override is deep-merged on top of the built-in default layout.
    /// </summary>
    public string? LayoutOverridePath { get; init; }
    /// <summary>
    /// Target-specific format options. For the Kiro target, recognised keys are:
    /// <list type="bullet">
    ///   <item><c>inclusionMode</c>: "always" | "fileMatch" | "auto" (default: "always")</item>
    ///   <item><c>fileMatchPattern</c>: glob pattern used when inclusionMode is "fileMatch"</item>
    /// </list>
    /// </summary>
    public Dictionary<string, string> FormatOptions { get; init; } = [];
    public List<string> RequiredMetadata { get; init; } = [];
}
