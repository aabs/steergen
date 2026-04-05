using Steergen.Core.Model;

namespace Steergen.Core.Targets;

public interface ITargetComponent
{
    string TargetId { get; }
    TargetDescriptor Descriptor { get; }
    Task GenerateAsync(ResolvedSteeringModel model, TargetConfiguration config, CancellationToken cancellationToken);

    /// <summary>
    /// Generates output using a pre-computed <see cref="WritePlan"/> to determine destination paths.
    /// Default implementation falls back to <see cref="GenerateAsync"/> for backward compatibility.
    /// </summary>
    Task GenerateWithPlanAsync(
        ResolvedSteeringModel model,
        TargetConfiguration config,
        WritePlan writePlan,
        CancellationToken cancellationToken)
        => GenerateAsync(model, config, cancellationToken);
}
