namespace Steergen.Core.Model;

/// <summary>Defines how unmatched rules are handled after route/catch-all evaluation fails.</summary>
public record FallbackRuleDefinition
{
    public FallbackMode Mode { get; init; } = FallbackMode.OtherAtCoreAnchor;
    public string FileBaseName { get; init; } = "other";
}
