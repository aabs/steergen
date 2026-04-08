namespace Steergen.Core.Generation;

/// <summary>
/// Resolves a routed write-plan path into the concrete path used under a caller-provided output base.
/// Absolute plan paths are rebased relative to the matching configured root; relative plan paths are
/// preserved under <paramref name="outputPath"/>.
/// </summary>
public static class PlannedOutputPathResolver
{
    public static string Resolve(
        string planPath,
        string outputPath,
        string? globalRoot,
        string? projectRoot)
    {
        // Always attempt root-stripping first — TryResolveRelativeToRoot normalises via
        // Path.GetFullPath so it correctly handles both absolute and relative inputs.
        // The early relative-path short-circuit that previously lived here would skip this
        // step and leave the root directory prefix embedded in the output path.
        foreach (var root in new[] { globalRoot, projectRoot }.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            if (TryResolveRelativeToRoot(planPath, root!, out var relativePath))
                return Path.Combine(outputPath, relativePath);
        }

        // No root matched: preserve relative paths as-is under outputPath;
        // absolute paths that match no known root fall back to filename only.
        if (!Path.IsPathRooted(planPath))
            return Path.Combine(outputPath, planPath);

        return Path.Combine(outputPath, Path.GetFileName(planPath));
    }

    internal static bool TryResolveRelativeToRoot(string path, string root, out string relativePath)
    {
        var normalizedPath = Normalize(path);
        var normalizedRoot = Normalize(root);
        var relativeCandidate = Path.GetRelativePath(normalizedRoot, normalizedPath);

        if (relativeCandidate == "."
            || relativeCandidate == ".."
            || relativeCandidate.StartsWith($"..{Path.DirectorySeparatorChar}", GetPathComparison())
            || relativeCandidate.StartsWith($"..{Path.AltDirectorySeparatorChar}", GetPathComparison())
            || Path.IsPathRooted(relativeCandidate))
        {
            relativePath = string.Empty;
            return false;
        }

        relativePath = relativeCandidate;
        return true;
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static StringComparison GetPathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}