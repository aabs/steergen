namespace Steergen.Core.Model;

/// <summary>Optional metadata describing a template variable declared in a layout definition.</summary>
public record VariableDefinition
{
    public string? Description { get; init; }
    public string? DefaultValue { get; init; }
}
