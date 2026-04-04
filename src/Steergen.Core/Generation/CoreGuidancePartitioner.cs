using Steergen.Core.Model;

namespace Steergen.Core.Generation;

/// <summary>
/// Splits a resolved rule set into core rules (domain == "core") and domain-specific modules.
/// </summary>
public sealed class CoreGuidancePartitioner
{
    public PartitionResult Partition(IReadOnlyList<SteeringRule> rules)
    {
        var coreRules = rules
            .Where(r => string.Equals(r.Domain, "core", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToList();

        var domainModules = rules
            .Where(r => !string.Equals(r.Domain, "core", StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => r.Domain, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<SteeringRule>)g
                    .OrderBy(r => r.Id, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

        return new PartitionResult(coreRules, domainModules);
    }
}

public sealed record PartitionResult(
    IReadOnlyList<SteeringRule> CoreRules,
    IReadOnlyDictionary<string, IReadOnlyList<SteeringRule>> DomainModules);
