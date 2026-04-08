using Steergen.Core.Configuration;

namespace Steergen.Core.Targets;

/// <summary>
/// Creates the folder layout for one or more targets under a project root.
/// All operations are idempotent: existing directories are left unchanged.
/// </summary>
public static class TargetLayoutInitializer
{
    public static readonly IReadOnlySet<string> KnownTargetIds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            TargetRegistry.KnownTargets.Speckit,
            TargetRegistry.KnownTargets.Kiro,
            TargetRegistry.KnownTargets.CopilotAgent,
            TargetRegistry.KnownTargets.KiroAgent,
        };

    public static bool IsValidTargetId(string targetId) =>
        KnownTargetIds.Contains(targetId);

    /// <summary>
    /// Returns the canonical folder list for a given target under <paramref name="projectRoot"/>.
    /// Always includes shared steering input folders plus the target-native output folders
    /// implied by the built-in layout definition.
    /// </summary>
    public static IReadOnlyList<string> GetLayoutFolders(string projectRoot, string targetId)
    {
        var globalDocsRoot = Path.Combine(projectRoot, "steering", "global");
        var projectDocsRoot = Path.Combine(projectRoot, "steering", "project");

        var folders = new List<string>
        {
            globalDocsRoot,
            projectDocsRoot,
        };

        var layout = new LayoutOverrideLoader().LoadDefault(targetId);
        var seen = new HashSet<string>(folders, StringComparer.Ordinal);

        foreach (var candidate in EnumerateBootstrapDirectories(layout, globalDocsRoot, projectDocsRoot, projectRoot))
        {
            if (seen.Add(candidate))
                folders.Add(candidate);
        }

        return folders;
    }

    /// <summary>
    /// Bootstraps the folder structure for all requested targets under <paramref name="projectRoot"/>.
    /// Validates target IDs first; returns a failure result without creating any folders if any ID is unknown.
    /// </summary>
    public static InitResult Initialize(string projectRoot, IEnumerable<string> targetIds)
    {
        var targets = targetIds.ToList();

        var unknown = targets.Where(id => !IsValidTargetId(id)).ToList();
        if (unknown.Count > 0)
            return InitResult.Failure($"Unknown target(s): {string.Join(", ", unknown)}");

        var created = new List<string>();
        var alreadyExisted = new List<string>();

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var targetId in targets)
        {
            foreach (var dir in GetLayoutFolders(projectRoot, targetId))
                Track(dir, seen, created, alreadyExisted);
        }

        if (targets.Count == 0)
        {
            Track(Path.Combine(projectRoot, "steering", "global"), seen, created, alreadyExisted);
            Track(Path.Combine(projectRoot, "steering", "project"), seen, created, alreadyExisted);
        }

        return InitResult.Ok(created, alreadyExisted);
    }

    private static IEnumerable<string> EnumerateBootstrapDirectories(
        Model.TargetLayoutDefinition layout,
        string globalDocsRoot,
        string projectDocsRoot,
        string workspaceRoot)
    {
        foreach (var candidate in EnumerateCandidateDirectories(layout, globalDocsRoot, projectDocsRoot, workspaceRoot))
        {
            var resolved = RebaseToWorkspace(candidate, globalDocsRoot, projectDocsRoot, workspaceRoot);
            if (resolved is not null)
                yield return resolved;
        }
    }

    private static IEnumerable<string> EnumerateCandidateDirectories(
        Model.TargetLayoutDefinition layout,
        string globalDocsRoot,
        string projectDocsRoot,
        string workspaceRoot)
    {
        var resolvedTargetRoot = string.IsNullOrWhiteSpace(layout.Roots.TargetRoot)
            ? string.Empty
            : ResolveRootVariables(layout.Roots.TargetRoot, globalDocsRoot, projectDocsRoot, workspaceRoot, targetRoot: null);

        if (!string.IsNullOrWhiteSpace(layout.Roots.TargetRoot))
            yield return resolvedTargetRoot;

        foreach (var route in layout.Routes)
        {
            if (string.IsNullOrWhiteSpace(route.Destination.Directory))
                continue;

            var resolved = ResolveRootVariables(
                route.Destination.Directory,
                globalDocsRoot,
                projectDocsRoot,
                workspaceRoot,
                resolvedTargetRoot);
            if (!ContainsTemplateVariables(resolved))
                yield return resolved;
        }

        if (layout.Purge is null)
            yield break;

        foreach (var root in layout.Purge.Roots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            var resolved = ResolveRootVariables(root, globalDocsRoot, projectDocsRoot, workspaceRoot, resolvedTargetRoot);
            if (!ContainsTemplateVariables(resolved))
                yield return resolved;
        }
    }

    private static string ResolveRootVariables(
        string path,
        string globalDocsRoot,
        string projectDocsRoot,
        string workspaceRoot,
        string? targetRoot) =>
        path
            .Replace("${globalRoot}", globalDocsRoot, StringComparison.OrdinalIgnoreCase)
            .Replace("${projectRoot}", projectDocsRoot, StringComparison.OrdinalIgnoreCase)
            .Replace("${generationRoot}", workspaceRoot, StringComparison.OrdinalIgnoreCase)
            .Replace("${targetRoot}", targetRoot ?? "${targetRoot}", StringComparison.OrdinalIgnoreCase);

    private static string? RebaseToWorkspace(
        string directory,
        string globalDocsRoot,
        string projectDocsRoot,
        string workspaceRoot)
    {
        if (TryRebase(directory, globalDocsRoot, workspaceRoot, out var rebased))
            return rebased;

        if (TryRebase(directory, projectDocsRoot, workspaceRoot, out rebased))
            return rebased;

        if (Path.IsPathRooted(directory) && directory.StartsWith(workspaceRoot, GetPathComparison()))
            return directory;

        return Path.IsPathRooted(directory)
            ? null
            : Path.GetFullPath(Path.Combine(workspaceRoot, directory));
    }

    private static bool TryRebase(string path, string sourceRoot, string workspaceRoot, out string rebased)
    {
        if (Generation.PlannedOutputPathResolver.TryResolveRelativeToRoot(path, sourceRoot, out var relativePath))
        {
            rebased = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
            return true;
        }

        rebased = string.Empty;
        return false;
    }

    private static bool ContainsTemplateVariables(string path) =>
        path.Contains("${", StringComparison.Ordinal);

    private static StringComparison GetPathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void Track(
        string dir,
        HashSet<string> seen,
        List<string> created,
        List<string> alreadyExisted)
    {
        if (!seen.Add(dir)) return;
        bool existed = Directory.Exists(dir);
        Directory.CreateDirectory(dir);
        (existed ? alreadyExisted : created).Add(dir);
    }
}

/// <summary>
/// Result of a <see cref="TargetLayoutInitializer.Initialize"/> call.
/// </summary>
public record InitResult(
    bool Success,
    IReadOnlyList<string> CreatedFolders,
    IReadOnlyList<string> ExistingFolders,
    string? ErrorMessage)
{
    public static InitResult Ok(IReadOnlyList<string> created, IReadOnlyList<string> existing) =>
        new(true, created, existing, null);

    public static InitResult Failure(string errorMessage) =>
        new(false, [], [], errorMessage);
}
