namespace Steergen.Core.Targets.Kiro;

public record KiroDocumentModel
{
    public string Description { get; init; } = "";
    public string Inclusion { get; init; } = "always";
    public string? FileMatchPattern { get; init; }
    public IReadOnlyList<KiroRuleProseModel> Rules { get; init; } = [];
    public IReadOnlyList<KiroRuleSectionModel> Sections { get; init; } = [];
}

public record KiroRuleSectionModel
{
    public string Heading { get; init; } = "General";
    public IReadOnlyList<KiroRuleProseModel> Rules { get; init; } = [];
}

public record KiroRuleProseModel
{
    public string? Id { get; init; }
    public string? Category { get; init; }
    public bool Deprecated { get; init; }
    public string? Supersedes { get; init; }
    public string PrimaryText { get; init; } = "";
    public string? ExplanatoryText { get; init; }
}
