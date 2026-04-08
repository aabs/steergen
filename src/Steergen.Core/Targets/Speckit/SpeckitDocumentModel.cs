namespace Steergen.Core.Targets.Speckit;

public record SpeckitConstitutionModel
{
    public IReadOnlyList<SpeckitRuleModel> Rules { get; init; } = [];
    public IReadOnlyList<SpeckitRuleSectionModel> Sections { get; init; } = [];
}

public record SpeckitModuleModel
{
    public string Domain { get; init; } = "";
    public IReadOnlyList<SpeckitRuleModel> Rules { get; init; } = [];
    public IReadOnlyList<SpeckitRuleSectionModel> Sections { get; init; } = [];
}

public record SpeckitRuleSectionModel
{
    public string Heading { get; init; } = "General";
    public IReadOnlyList<SpeckitRuleModel> Rules { get; init; } = [];
}

public record SpeckitRuleModel
{
    public string Id { get; init; } = "";
    public string Severity { get; init; } = "info";
    public string? Category { get; init; }
    public bool Deprecated { get; init; }
    public string? Supersedes { get; init; }
    public string PrimaryText { get; init; } = "";
    public string? ExplanatoryText { get; init; }
}
