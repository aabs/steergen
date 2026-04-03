namespace Steergen.Core.Targets.Kiro;

public enum KiroInclusionMode
{
    Always,
    FileMatch,
    Auto,
}

public record KiroTargetOptions
{
    public KiroInclusionMode InclusionMode { get; init; } = KiroInclusionMode.Always;
    public string? FileMatchPattern { get; init; }

    public static KiroTargetOptions FromFormatOptions(IDictionary<string, string> options)
    {
        var mode = KiroInclusionMode.Always;
        if (options.TryGetValue("inclusionMode", out var modeStr))
        {
            mode = modeStr.ToLowerInvariant() switch
            {
                "filematch" => KiroInclusionMode.FileMatch,
                "auto" => KiroInclusionMode.Auto,
                _ => KiroInclusionMode.Always,
            };
        }

        string? pattern = null;
        if (options.TryGetValue("fileMatchPattern", out var patternStr))
            pattern = patternStr;

        return new KiroTargetOptions { InclusionMode = mode, FileMatchPattern = pattern };
    }
}
