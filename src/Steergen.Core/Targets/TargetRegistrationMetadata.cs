namespace Steergen.Core.Targets;

/// <summary>
/// Descriptive metadata for an <see cref="ITargetComponent"/> registration.
/// Built-in targets carry this metadata automatically; additive (third-party) targets
/// should supply it when calling <see cref="TargetRegistry.Register"/>.
/// </summary>
public record TargetRegistrationMetadata
{
    /// <summary>Unique, stable identifier that matches <see cref="ITargetComponent.TargetId"/>.</summary>
    public required string TargetId { get; init; }

    /// <summary>Human-readable display name shown in CLI output.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Short description of the target's purpose.</summary>
    public required string Description { get; init; }

    /// <summary>Optional author / organisation name for third-party targets.</summary>
    public string? AuthorName { get; init; }

    /// <summary>Optional SemVer string for the target implementation.</summary>
    public string? Version { get; init; }

    /// <summary>True for targets shipped inside the specgen package.</summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>Creates metadata from a <see cref="TargetDescriptor"/>.</summary>
    public static TargetRegistrationMetadata FromDescriptor(TargetDescriptor descriptor, bool isBuiltIn = false) =>
        new()
        {
            TargetId = descriptor.Id,
            DisplayName = descriptor.DisplayName,
            Description = descriptor.Description,
            IsBuiltIn = isBuiltIn,
        };
}
