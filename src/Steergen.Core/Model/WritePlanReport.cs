namespace Steergen.Core.Model;

/// <summary>
/// Lifecycle report produced by <c>WritePlanExecutor</c> after executing a <see cref="WritePlan"/>.
/// Captures which files were truncated and written during the run.
/// </summary>
public record WritePlanReport
{
    public string TargetId { get; init; } = "";
    public IReadOnlyList<string> WrittenFiles { get; init; } = [];
    public IReadOnlyList<string> TruncatedFiles { get; init; } = [];
    public bool Success { get; init; } = true;
    public string? FailureReason { get; init; }
}
