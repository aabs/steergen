using Steergen.Core.Model;
using Steergen.Core.Validation;

namespace Steergen.Core.Packs;

/// <summary>
/// Result of loading all configured rules packs. Contains the merged
/// steering documents and any diagnostics encountered during loading.
/// </summary>
public sealed record RulesPackLoadResult
{
    public IReadOnlyList<SteeringDocument> Documents { get; init; } = [];
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];
}
