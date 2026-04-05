namespace Steergen.Core.Model;

/// <summary>
/// Auditable purge outcome per target. Reports what was removed, skipped, or not eligible.
/// </summary>
public record PurgeResult
{
    public string TargetId { get; init; } = "";
    public IReadOnlyList<string> RemovedFiles { get; init; } = [];
    public IReadOnlyList<SkippedPurgeFile> SkippedFiles { get; init; } = [];
    public string? NoOpReason { get; init; }
    public bool Success { get; init; } = true;
    public string? SafetyFailureReason { get; init; }
}

/// <summary>
/// Describes a file candidate that was not deleted and the reason why.
/// </summary>
public record SkippedPurgeFile
{
    public string Path { get; init; } = "";
    public SkippedPurgeReason Reason { get; init; }
}

/// <summary>
/// Reason a candidate file was not deleted during a purge operation.
/// </summary>
public enum SkippedPurgeReason
{
    OutsideRoot,
    GlobMiss,
    PermissionDenied,
    SafetyBlocked,
    DryRun,
}
