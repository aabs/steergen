namespace Steergen.Core.Model;

/// <summary>
/// Template for the destination path of a routed steering rule.
/// Supports <c>${variable}</c> substitution via the routing context.
/// </summary>
public record DestinationTemplate
{
    public string Directory { get; init; } = "";
    public string FileName { get; init; } = "";
    public string? Extension { get; init; }
}
