using Steergen.Core.Model;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Agents;

namespace Steergen.Core.UnitTests.Targets;

public sealed class AgentTargetSemanticParityTests
{
    private const string CopilotTemplate = """
        {{- for rule in rules }}
        {{ rule.primary_text }}
        {{ if rule.explanatory_text -}}
        {{ rule.explanatory_text }}
        {{ end -}}
        {{- end }}
        """;

    private const string KiroAgentTemplate = """
        ---
        {{ if name -}}
        name: {{ name }}
        {{ end -}}
        description: {{ description }}
        ---
        {{- for rule in rules }}
        {{ rule.primary_text }}
        {{ if rule.explanatory_text -}}
        {{ rule.explanatory_text }}
        {{ end -}}
        {{- end }}
        """;

    private static readonly ITemplateProvider FakeTemplates =
        new InlineAgentTemplateProvider(CopilotTemplate, KiroAgentTemplate);

    private static readonly IReadOnlyList<SteeringRule> SampleRules =
    [
        new SteeringRule { Id = "A-001", Severity = "error", PrimaryText = "Write tests for all public APIs." },
        new SteeringRule { Id = "A-002", Severity = "warning", PrimaryText = "Document every module entry point.", ExplanatoryText = "Include examples where helpful." },
        new SteeringRule { Id = "A-003", Severity = "info", PrimaryText = "Old guidance.", Deprecated = true },
    ];

    private static readonly IReadOnlyList<SteeringDocument> SampleDocuments =
    [
        new SteeringDocument
        {
            Id = "guidance",
            Title = "Engineering Guidance",
            SourcePath = "guidance.md",
            Rules = SampleRules,
        },
    ];

    private static ResolvedSteeringModel BuildModel() => new()
    {
        Documents = SampleDocuments,
        Rules = [],
        ActiveProfiles = [],
    };

    [Fact]
    public async Task BothTargets_ContainPrimaryTextOfActiveRules()
    {
        var copilotTarget = new CopilotAgentTargetComponent(FakeTemplates);
        var kiroTarget = new KiroAgentTargetComponent(FakeTemplates);

        var outputDir = Path.Combine(Path.GetTempPath(), $"agent-parity-{Guid.NewGuid():N}");
        var copilotOut = Path.Combine(outputDir, "copilot");
        var kiroOut = Path.Combine(outputDir, "kiro");

        try
        {
            var model = BuildModel();

            await copilotTarget.GenerateAsync(model, new TargetConfiguration
            {
                Id = "copilot-agent", Enabled = true, OutputPath = copilotOut,
            }, CancellationToken.None);

            await kiroTarget.GenerateAsync(model, new TargetConfiguration
            {
                Id = "kiro-agent", Enabled = true, OutputPath = kiroOut,
            }, CancellationToken.None);

            var copilotContent = await File.ReadAllTextAsync(
                Path.Combine(copilotOut, "copilot-instructions.md"));
            var kiroFiles = Directory.GetFiles(kiroOut, "*.md");
            Assert.Single(kiroFiles);
            var kiroContent = await File.ReadAllTextAsync(kiroFiles[0]);

            Assert.Contains("Write tests for all public APIs.", copilotContent);
            Assert.Contains("Document every module entry point.", copilotContent);
            Assert.Contains("Write tests for all public APIs.", kiroContent);
            Assert.Contains("Document every module entry point.", kiroContent);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task BothTargets_ExcludeDeprecatedRules()
    {
        var copilotTarget = new CopilotAgentTargetComponent(FakeTemplates);
        var kiroTarget = new KiroAgentTargetComponent(FakeTemplates);

        var outputDir = Path.Combine(Path.GetTempPath(), $"agent-depr-{Guid.NewGuid():N}");
        var copilotOut = Path.Combine(outputDir, "copilot");
        var kiroOut = Path.Combine(outputDir, "kiro");

        try
        {
            var model = BuildModel();

            await copilotTarget.GenerateAsync(model, new TargetConfiguration
            {
                Id = "copilot-agent", Enabled = true, OutputPath = copilotOut,
            }, CancellationToken.None);

            await kiroTarget.GenerateAsync(model, new TargetConfiguration
            {
                Id = "kiro-agent", Enabled = true, OutputPath = kiroOut,
            }, CancellationToken.None);

            var copilotContent = await File.ReadAllTextAsync(
                Path.Combine(copilotOut, "copilot-instructions.md"));
            var kiroFiles = Directory.GetFiles(kiroOut, "*.md");
            var kiroContent = await File.ReadAllTextAsync(kiroFiles[0]);

            Assert.DoesNotContain("Old guidance.", copilotContent);
            Assert.DoesNotContain("Old guidance.", kiroContent);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task BothTargets_DoNotContainRuleIds()
    {
        var copilotTarget = new CopilotAgentTargetComponent(FakeTemplates);
        var kiroTarget = new KiroAgentTargetComponent(FakeTemplates);

        var outputDir = Path.Combine(Path.GetTempPath(), $"agent-ids-{Guid.NewGuid():N}");
        var copilotOut = Path.Combine(outputDir, "copilot");
        var kiroOut = Path.Combine(outputDir, "kiro");

        try
        {
            var model = BuildModel();

            await copilotTarget.GenerateAsync(model, new TargetConfiguration
            {
                Id = "copilot-agent", Enabled = true, OutputPath = copilotOut,
            }, CancellationToken.None);

            await kiroTarget.GenerateAsync(model, new TargetConfiguration
            {
                Id = "kiro-agent", Enabled = true, OutputPath = kiroOut,
            }, CancellationToken.None);

            var copilotContent = await File.ReadAllTextAsync(
                Path.Combine(copilotOut, "copilot-instructions.md"));
            var kiroFiles = Directory.GetFiles(kiroOut, "*.md");
            var kiroContent = await File.ReadAllTextAsync(kiroFiles[0]);

            Assert.DoesNotMatch(@"\bA-\d{3}\b", copilotContent);
            Assert.DoesNotMatch(@"\bA-\d{3}\b", kiroContent);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task BothTargets_ExplanatoryTextIncludedWhenPresent()
    {
        var copilotTarget = new CopilotAgentTargetComponent(FakeTemplates);
        var kiroTarget = new KiroAgentTargetComponent(FakeTemplates);

        var outputDir = Path.Combine(Path.GetTempPath(), $"agent-expl-{Guid.NewGuid():N}");
        var copilotOut = Path.Combine(outputDir, "copilot");
        var kiroOut = Path.Combine(outputDir, "kiro");

        try
        {
            var model = BuildModel();

            await copilotTarget.GenerateAsync(model, new TargetConfiguration
            {
                Id = "copilot-agent", Enabled = true, OutputPath = copilotOut,
            }, CancellationToken.None);

            await kiroTarget.GenerateAsync(model, new TargetConfiguration
            {
                Id = "kiro-agent", Enabled = true, OutputPath = kiroOut,
            }, CancellationToken.None);

            var copilotContent = await File.ReadAllTextAsync(
                Path.Combine(copilotOut, "copilot-instructions.md"));
            var kiroFiles = Directory.GetFiles(kiroOut, "*.md");
            var kiroContent = await File.ReadAllTextAsync(kiroFiles[0]);

            Assert.Contains("Include examples where helpful.", copilotContent);
            Assert.Contains("Include examples where helpful.", kiroContent);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    private sealed class InlineAgentTemplateProvider(
        string copilotTemplate,
        string kiroAgentTemplate) : ITemplateProvider
    {
        public string GetTemplate(string targetId, string templateName) =>
            (targetId, templateName) switch
            {
                ("agents", "copilot.agent") => copilotTemplate,
                ("agents", "kiro.agent") => kiroAgentTemplate,
                _ => throw new InvalidOperationException($"Unknown template '{targetId}/{templateName}'."),
            };
    }
}
