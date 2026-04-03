namespace Steergen.Core.Targets.Agents;

public record AgentRuleProseModel
{
    public string PrimaryText { get; init; } = "";
    public string? ExplanatoryText { get; init; }
}

public record CopilotAgentDocumentModel
{
    public IReadOnlyList<AgentRuleProseModel> Rules { get; init; } = [];
}

public record KiroAgentDocumentModel
{
    public string? Name { get; init; }
    public string Description { get; init; } = "";
    public IReadOnlyList<AgentRuleProseModel> Rules { get; init; } = [];
}
