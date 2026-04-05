namespace Steergen.Core.Model;

/// <summary>
/// Declares which generated files are eligible for deletion by the purge command.
/// An empty or absent <c>Globs</c> list makes purge a deterministic no-op for the target.
/// </summary>
public record PurgePolicyDefinition
{
    public bool Enabled { get; init; } = true;
    public IReadOnlyList<string> Roots { get; init; } = [];
    public IReadOnlyList<string> Globs { get; init; } = [];
}
