using Steergen.Core.Model;

namespace Steergen.Core.Targets;

public interface ITargetComponent
{
    string TargetId { get; }
    TargetDescriptor Descriptor { get; }

    Task GenerateWithPlanAsync(
        ResolvedSteeringModel model,
        TargetConfiguration config,
        WritePlan writePlan,
    CancellationToken cancellationToken);
}
