using Steergen.Core.Validation;

namespace Steergen.Core.Packs;

/// <summary>
/// Result of a pack download operation from GitHub.
/// </summary>
public sealed record PackDownloadResult
{
    public bool Success { get; init; }

    /// <summary>
    /// Local filesystem path where the pack was cached on success.
    /// </summary>
    public string? CachePath { get; init; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];
}
