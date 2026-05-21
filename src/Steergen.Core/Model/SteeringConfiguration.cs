using Steergen.Core.Packs;

namespace Steergen.Core.Model;

public record SteeringConfiguration
{
    public string? ProjectRoot { get; init; }
    public string? GenerationRoot { get; init; }
    public IReadOnlyList<string> ActiveProfiles { get; init; } = [];
    public IReadOnlyList<TargetConfiguration> Targets { get; init; } = [];
    public IReadOnlyList<string> RegisteredTargets { get; init; } = [];
    public string? TemplatePackVersion { get; init; }
    public TemplatePackConfig? TemplatePack { get; init; }
    public IReadOnlyList<RulesPackEntry> RulesPacks { get; init; } = [];
}

/// <summary>
/// Configuration for a template pack source. Either <see cref="Source"/> (GitHub)
/// or <see cref="LocalPath"/> should be specified, not both.
/// </summary>
public sealed record TemplatePackConfig
{
    /// <summary>
    /// GitHub source in the format "github:{owner}/{repo}".
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Git tag, branch, or 40-character commit SHA.
    /// </summary>
    public string? Ref { get; init; }

    /// <summary>
    /// Alternative: local filesystem path to a template pack directory.
    /// </summary>
    public string? LocalPath { get; init; }
}

/// <summary>
/// Configuration entry for a rules pack in the <c>rulesPacks</c> list.
/// </summary>
public sealed record RulesPackEntry
{
    /// <summary>
    /// GitHub source in the format "github:{owner}/{repo}".
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Git tag, branch, or 40-character commit SHA.
    /// </summary>
    public string? Ref { get; init; }

    /// <summary>
    /// Subdirectory within the repository when multiple rule sets are published in one repo.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Consumer scope override. When set, overrides the scope declared in the pack manifest.
    /// </summary>
    public PackScope? Scope { get; init; }
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
