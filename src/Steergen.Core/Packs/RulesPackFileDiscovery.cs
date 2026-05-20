namespace Steergen.Core.Packs;

/// <summary>
/// Discovers Markdown files recursively under a rules pack root directory.
/// Returns all and only files with the <c>.md</c> extension in deterministic
/// ordinal sort order, excluding symbolic links.
/// </summary>
public static class RulesPackFileDiscovery
{
    /// <summary>
    /// Discovers all <c>.md</c> files recursively under <paramref name="rulesRoot"/>,
    /// sorted by full path using ordinal string comparison, excluding symbolic links.
    /// </summary>
    /// <param name="rulesRoot">The root directory to search recursively.</param>
    /// <returns>
    /// An ordered list of absolute file paths for all <c>.md</c> files found,
    /// excluding any that are symbolic links (reparse points).
    /// </returns>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when <paramref name="rulesRoot"/> does not exist.
    /// </exception>
    public static IReadOnlyList<string> DiscoverMarkdownFiles(string rulesRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(rulesRoot);

        if (!Directory.Exists(rulesRoot))
            throw new DirectoryNotFoundException($"Rules root directory does not exist: '{rulesRoot}'");

        return Directory
            .EnumerateFiles(rulesRoot, "*.md", SearchOption.AllDirectories)
            .Where(path => !IsSymbolicLink(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Determines whether the specified file is a symbolic link (reparse point).
    /// </summary>
    private static bool IsSymbolicLink(string filePath)
    {
        var attributes = File.GetAttributes(filePath);
        return attributes.HasFlag(FileAttributes.ReparsePoint);
    }
}
