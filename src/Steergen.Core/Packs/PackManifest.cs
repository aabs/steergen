namespace Steergen.Core.Packs;

/// <summary>
/// Shared manifest model for both template packs and rules packs.
/// Parsed from <c>pack.yaml</c> at the root of a pack directory.
/// </summary>
public sealed record PackManifest
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string MinSteergenVersion { get; init; }

    /// <summary>
    /// Required for rules packs. Determines merge precedence.
    /// </summary>
    public PackScope? Scope { get; init; }

    /// <summary>
    /// Target IDs that this template pack overrides (template packs only).
    /// When null, the pack applies to all targets.
    /// </summary>
    public IReadOnlyList<string>? Targets { get; init; }

    /// <summary>
    /// Complete target definitions provided by this template pack (external targets).
    /// </summary>
    public IReadOnlyList<ProvidedTargetDefinition>? ProvidedTargets { get; init; }

    /// <summary>
    /// Subdirectory containing steering documents (rules packs only).
    /// Defaults to the pack root directory when null.
    /// </summary>
    public string? RulesRoot { get; init; }
}
