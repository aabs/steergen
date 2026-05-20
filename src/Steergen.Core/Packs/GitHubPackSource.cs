namespace Steergen.Core.Packs;

/// <summary>
/// Identifies a pack published in a GitHub repository.
/// </summary>
public sealed record GitHubPackSource
{
    public required string Owner { get; init; }
    public required string Repo { get; init; }

    /// <summary>
    /// Git tag, branch, or 40-character commit SHA.
    /// When null, the repository default branch is used.
    /// </summary>
    public string? Ref { get; init; }

    /// <summary>
    /// Subdirectory within the repository containing the pack.
    /// Used when multiple packs are published in a single repo.
    /// </summary>
    public string? Path { get; init; }
}
