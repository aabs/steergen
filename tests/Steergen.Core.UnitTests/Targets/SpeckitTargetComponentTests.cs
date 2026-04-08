using Steergen.Core.Generation;
using Steergen.Core.Model;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Speckit;

namespace Steergen.Core.UnitTests.Targets;

public sealed class SpeckitTargetComponentTests
{
    private static readonly ITemplateProvider FakeTemplates = new InlineTemplateProvider(
        constitutionTemplate: """
            # Constitution
            {{ for section in sections -}}
            ## {{ section.heading }}
            {{ for rule in section.rules -}}
            - {{ rule.id }}: {{ rule.primary_text }}{{ if rule.supersedes }} [Supersedes: {{ rule.supersedes }}]{{ end }}{{ if rule.deprecated }} (deprecated){{ end }}
            {{ end -}}
            {{ end -}}
            """,
        moduleTemplate: """
            # Module: {{ domain }}
            {{ for section in sections -}}
            ## {{ section.heading }}
            {{ for rule in section.rules -}}
            - {{ rule.id }}: {{ rule.primary_text }}{{ if rule.supersedes }} [Supersedes: {{ rule.supersedes }}]{{ end }}{{ if rule.deprecated }} (deprecated){{ end }}
            {{ end -}}
            {{ end -}}
            """);

    [Fact]
    public async Task RenderConstitution_ContainsOnlyCoreRulesIds()
    {
        var target = new SpeckitTargetComponent(FakeTemplates);
        var model = new SpeckitConstitutionModel
        {
            Rules =
            [
                new SpeckitRuleModel { Id = "CORE-001", Severity = "error", PrimaryText = "Must have tests." },
                new SpeckitRuleModel { Id = "CORE-002", Severity = "warning", PrimaryText = "Document public APIs." },
            ],
        };

        var output = await target.RenderConstitutionAsync(model);

        Assert.Contains("CORE-001", output);
        Assert.Contains("CORE-002", output);
        Assert.Contains("## General", output);
    }

    [Fact]
    public async Task RenderConstitution_DeprecatedRule_EmitsDeprecatedMarker()
    {
        var target = new SpeckitTargetComponent(FakeTemplates);
        var model = new SpeckitConstitutionModel
        {
            Rules =
            [
                new SpeckitRuleModel { Id = "CORE-OLD", Severity = "info", Deprecated = true, PrimaryText = "Old rule." },
            ],
        };

        var output = await target.RenderConstitutionAsync(model);

        Assert.Contains("(deprecated)", output);
    }

    [Fact]
    public async Task RenderConstitution_SupersedesRule_EmitsSupersededReference()
    {
        var target = new SpeckitTargetComponent(FakeTemplates);
        var model = new SpeckitConstitutionModel
        {
            Rules =
            [
                new SpeckitRuleModel { Id = "CORE-002", Severity = "error", Supersedes = "CORE-001", PrimaryText = "New rule." },
            ],
        };

        var output = await target.RenderConstitutionAsync(model);

        Assert.Contains("CORE-001", output);
    }

    [Fact]
    public async Task RenderModule_ContainsDomainName()
    {
        var target = new SpeckitTargetComponent(FakeTemplates);
        var model = new SpeckitModuleModel
        {
            Domain = "api-design",
            Rules = [new SpeckitRuleModel { Id = "API-001", Severity = "error", PrimaryText = "Use versioned URIs." }],
        };

        var output = await target.RenderModuleAsync(model);

        Assert.Contains("api-design", output);
        Assert.Contains("API-001", output);
    }

    [Fact]
    public async Task GenerateWithPlanAsync_WritesConstitutionAndModuleFiles()
    {
        var target = new SpeckitTargetComponent(FakeTemplates);
        var model = new ResolvedSteeringModel
        {
            Rules =
            [
                new SteeringRule { Id = "CORE-001", Domain = "core", Severity = "error", PrimaryText = "Core rule." },
                new SteeringRule { Id = "API-001", Domain = "api", Severity = "warning", PrimaryText = "API rule." },
            ],
        };
        var outputDir = Path.Combine(Path.GetTempPath(), $"speckit-test-{Guid.NewGuid():N}");
        try
        {
            var config = new TargetConfiguration { Id = "speckit", Enabled = true, OutputPath = outputDir };
            await target.GenerateWithPlanAsync(model, config, BuildWritePlan(model), CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(outputDir, "constitution.md")));
            Assert.True(File.Exists(Path.Combine(outputDir, "api.md")));

            var constitutionContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "constitution.md"));
            var moduleContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "api.md"));

            Assert.Contains("CORE-001", constitutionContent);
            Assert.DoesNotContain("API-001", constitutionContent);
            Assert.Contains("API-001", moduleContent);
            Assert.DoesNotContain("CORE-001", moduleContent);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateWithPlanAsync_DeprecatedAndSupersedes_MetadataPreservedInOutput()
    {
        var target = new SpeckitTargetComponent(FakeTemplates);
        var model = new ResolvedSteeringModel
        {
            Rules =
            [
                new SteeringRule { Id = "CORE-002", Domain = "core", Severity = "error", Supersedes = "CORE-001", PrimaryText = "New rule." },
                new SteeringRule { Id = "CORE-001", Domain = "core", Severity = "info", Deprecated = true, PrimaryText = "Old rule." },
            ],
        };
        var outputDir = Path.Combine(Path.GetTempPath(), $"speckit-test-{Guid.NewGuid():N}");
        try
        {
            var config = new TargetConfiguration { Id = "speckit", Enabled = true, OutputPath = outputDir };
            await target.GenerateWithPlanAsync(model, config, BuildWritePlan(model), CancellationToken.None);

            var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "constitution.md"));
            Assert.Contains("(deprecated)", content);
            Assert.Contains("CORE-001", content);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task RenderConstitution_UsesCompactBulletLinesWithoutTitleMetadata()
    {
        var target = new SpeckitTargetComponent(FakeTemplates);
        var model = new SpeckitConstitutionModel
        {
            Rules =
            [
                new SpeckitRuleModel
                {
                    Id = "A11Y-001",
                    Category = "accessibility",
                    PrimaryText = "All UI components shall comply with WCAG 2.1 AA standards.",
                },
            ],
        };

        var output = await target.RenderConstitutionAsync(model);

        Assert.Contains("## Accessibility", output);
        Assert.Contains("- A11Y-001: All UI components shall comply with WCAG 2.1 AA standards.", output);
        Assert.DoesNotContain("title:", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Severity:", output, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InlineTemplateProvider(string constitutionTemplate, string moduleTemplate) : ITemplateProvider
    {
        public string GetTemplate(string targetId, string templateName) =>
            templateName switch
            {
                "constitution" => constitutionTemplate,
                "module" => moduleTemplate,
                _ => throw new InvalidOperationException($"Unknown template '{templateName}'."),
            };
    }

    private static WritePlan BuildWritePlan(ResolvedSteeringModel model) => new()
    {
        TargetId = "speckit",
        Files = model.Rules
            .GroupBy(rule => string.Equals(rule.Domain, "core", StringComparison.OrdinalIgnoreCase)
                ? "constitution.md"
                : $"{rule.Domain}.md",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new WritePlanFile
            {
                Path = group.Key,
                AppendUnits = group
                    .OrderBy(rule => rule.Id, StringComparer.Ordinal)
                    .Select((rule, index) => new ContentUnit
                    {
                        RuleId = rule.Id ?? string.Empty,
                        OrderKey = (0, index, rule.Id ?? string.Empty),
                    })
                    .ToList(),
            })
            .ToList(),
    };
}
