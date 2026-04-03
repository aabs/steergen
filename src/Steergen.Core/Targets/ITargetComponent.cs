using Steergen.Core.Model;

namespace Steergen.Core.Targets;

public interface ITargetComponent
{
    string TargetId { get; }
    TargetDescriptor Descriptor { get; }
    Task GenerateAsync(ResolvedSteeringModel model, TargetConfiguration config, CancellationToken cancellationToken);
}
