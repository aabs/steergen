namespace Steergen.Core.Packs;

/// <summary>
/// Configuration for a single rules pack entry, combining the GitHub source
/// with an optional consumer scope override that takes precedence over the
/// pack manifest's declared scope.
/// </summary>
public sealed record RulesPackConfiguration
{
    public required GitHubPackSource Source { get; init; }

    /// <summary>
    /// When set, overrides the scope declared in the pack manifest,
    /// allowing consumers to elevate or demote a pack's merge precedence.
    /// </summary>
    public PackScope? ScopeOverride { get; init; }
}
