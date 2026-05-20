namespace Steergen.Core.Packs;

/// <summary>
/// Declares a complete target definition provided by a template pack.
/// The pack supplies templates and a default layout; Steergen supplies
/// the generic PackTargetComponent that delegates rendering.
/// </summary>
public sealed record ProvidedTargetDefinition
{
    public required string TargetId { get; init; }

    /// <summary>
    /// Relative path to layout YAML within the pack directory.
    /// </summary>
    public required string DefaultLayout { get; init; }

    public string? Description { get; init; }
}
