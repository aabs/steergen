namespace Steergen.Core.Model;

/// <summary>Ordered file-write actions for a complete target generation run.</summary>
public record WritePlan
{
    public string TargetId { get; init; } = "";
    public IReadOnlyList<WritePlanFile> Files { get; init; } = [];
    /// <summary>Resolved global root used for stripping layout-rooted absolute paths.</summary>
    public string? GlobalRoot { get; init; }
    /// <summary>Resolved project root used for stripping layout-rooted absolute paths.</summary>
    public string? ProjectRoot { get; init; }
}

/// <summary>Represents one destination file and its ordered content units.</summary>
public record WritePlanFile
{
    public string Path { get; init; } = "";
    public bool TruncateAtStart { get; init; } = true;
    public IReadOnlyList<ContentUnit> AppendUnits { get; init; } = [];
}

/// <summary>A single rendered rule block to be appended to a destination file.</summary>
public record ContentUnit
{
    public string RuleId { get; init; } = "";
    public string RenderedContent { get; init; } = "";
    public (int Scope, int Order, string RuleId) OrderKey { get; init; }
}
