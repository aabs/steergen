using Steergen.Core.Model;

namespace Steergen.Core.Packs;

/// <summary>
/// Groups steering documents from a rules pack by their effective scope.
/// Used by the extended <see cref="Steergen.Core.Merge.SteeringResolver.Resolve"/>
/// signature to apply scope-based merge precedence.
/// </summary>
public sealed record ScopedPackDocuments
{
    public required PackScope Scope { get; init; }
    public IReadOnlyList<SteeringDocument> Documents { get; init; } = [];
}
