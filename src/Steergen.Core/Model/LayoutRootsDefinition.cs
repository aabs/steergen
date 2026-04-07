namespace Steergen.Core.Model;

/// <summary>Root path templates for a target layout, supporting <c>${variable}</c> substitution.</summary>
public record LayoutRootsDefinition
{
    public string GlobalRoot { get; init; } = "${globalRoot}";
    public string ProjectRoot { get; init; } = "${projectRoot}";
    public string TargetRoot { get; init; } = "${projectRoot}";
}
