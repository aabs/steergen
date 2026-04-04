namespace Steergen.Core.Targets.Kiro;

public record KiroDocumentModel
{
    public string Description { get; init; } = "";
    public string Inclusion { get; init; } = "always";
    public string? FileMatchPattern { get; init; }
    public IReadOnlyList<KiroRuleProseModel> Rules { get; init; } = [];
}

public record KiroRuleProseModel
{
    public string PrimaryText { get; init; } = "";
    public string? ExplanatoryText { get; init; }
}
