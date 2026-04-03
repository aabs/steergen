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
    /// Always includes shared steering input folders plus a per-target output folder.
    /// </summary>
    public static IReadOnlyList<string> GetLayoutFolders(string projectRoot, string targetId) =>
    [
        Path.Combine(projectRoot, "steering", "global"),
        Path.Combine(projectRoot, "steering", "project"),
        Path.Combine(projectRoot, targetId),
    ];

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

        // Shared steering input folders (created once regardless of target count).
        var sharedDirs = new[]
        {
            Path.Combine(projectRoot, "steering", "global"),
            Path.Combine(projectRoot, "steering", "project"),
        };

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dir in sharedDirs)
        {
            Track(dir, seen, created, alreadyExisted);
        }

        foreach (var targetId in targets)
        {
            var outputDir = Path.Combine(projectRoot, targetId);
            Track(outputDir, seen, created, alreadyExisted);
        }

        return InitResult.Ok(created, alreadyExisted);
    }

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
