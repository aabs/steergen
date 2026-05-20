namespace Steergen.Core.Targets;

/// <summary>
/// Describes a registered target, including its origin (built-in or pack-provided).
/// </summary>
public record TargetDescriptor(string Id, string DisplayName, string Description)
{
    /// <summary>
    /// Alias for <see cref="Id"/> matching the design document naming.
    /// </summary>
    public string TargetId => Id;

    /// <summary>
    /// Indicates whether the target is built-in or provided by a template pack.
    /// </summary>
    public TargetOrigin Origin { get; init; } = TargetOrigin.BuiltIn;

    /// <summary>
    /// The name of the pack that provides this target, if <see cref="Origin"/> is <see cref="TargetOrigin.PackProvided"/>.
    /// </summary>
    public string? PackName { get; init; }
}

/// <summary>
/// Indicates the origin of a registered target.
/// </summary>
public enum TargetOrigin
{
    /// <summary>Target is compiled into the Steergen binary.</summary>
    BuiltIn,

    /// <summary>Target is provided by an external template pack.</summary>
    PackProvided
}
