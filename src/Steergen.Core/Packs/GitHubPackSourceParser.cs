namespace Steergen.Core.Packs;

/// <summary>
/// Parses and formats the <c>github:{owner}/{repo}</c> pack source notation
/// used in <c>steergen.config.yaml</c>.
/// </summary>
public static class GitHubPackSourceParser
{
    private const string Prefix = "github:";

    /// <summary>
    /// Parses "github:{owner}/{repo}" into a <see cref="GitHubPackSource"/>.
    /// Returns null if the format is invalid (missing prefix, missing slash,
    /// empty owner, or empty repo).
    /// </summary>
    public static GitHubPackSource? Parse(string source, string? refValue = null, string? path = null)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        if (!source.StartsWith(Prefix, StringComparison.Ordinal))
            return null;

        var ownerRepo = source[Prefix.Length..];

        var slashIndex = ownerRepo.IndexOf('/');
        if (slashIndex < 0)
            return null;

        var owner = ownerRepo[..slashIndex];
        var repo = ownerRepo[(slashIndex + 1)..];

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            return null;

        return new GitHubPackSource
        {
            Owner = owner,
            Repo = repo,
            Ref = refValue,
            Path = path
        };
    }

    /// <summary>
    /// Formats a <see cref="GitHubPackSource"/> back to the canonical
    /// <c>github:{owner}/{repo}</c> string representation.
    /// </summary>
    public static string Format(GitHubPackSource source)
    {
        return $"{Prefix}{source.Owner}/{source.Repo}";
    }
}
