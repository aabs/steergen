using Steergen.Core.Model;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Kiro;

namespace Steergen.Core.UnitTests.Targets;

public sealed class KiroTargetComponentTests
{
    private const string DocumentTemplate = """
        ---
        description: {{ description }}
        inclusion: {{ inclusion }}
        {{ if file_match_pattern -}}
        fileMatchPattern: {{ file_match_pattern }}
        {{ end -}}
        ---
        {{- for rule in rules }}
        {{ rule.primary_text }}
        {{ if rule.explanatory_text -}}
        {{ rule.explanatory_text }}
        {{ end -}}
        {{- end }}
        """;

    private static readonly ITemplateProvider FakeTemplates =
        new InlineKiroTemplateProvider(DocumentTemplate);

    [Fact]
    public async Task RenderDocument_AlwaysInclusion_FrontmatterContainsAlways()
    {
        var target = new KiroTargetComponent(FakeTemplates);
        var model = new KiroDocumentModel
        {
            Description = "Test document",
            Inclusion = "always",
            Rules = [new KiroRuleProseModel { PrimaryText = "Always follow this rule." }],
        };

        var output = await target.RenderDocumentAsync(model);

        Assert.Contains("inclusion: always", output);
        Assert.DoesNotContain("fileMatchPattern:", output);
    }

    [Fact]
    public async Task RenderDocument_FileMatchInclusion_FrontmatterContainsFileMatchPattern()
    {
        var target = new KiroTargetComponent(FakeTemplates);
        var model = new KiroDocumentModel
        {
            Description = "TypeScript rules",
            Inclusion = "fileMatch",
            FileMatchPattern = "**/*.ts",
            Rules = [new KiroRuleProseModel { PrimaryText = "Use strict mode." }],
        };

        var output = await target.RenderDocumentAsync(model);

        Assert.Contains("inclusion: fileMatch", output);
        Assert.Contains("fileMatchPattern: **/*.ts", output);
    }

    [Fact]
    public async Task RenderDocument_AutoInclusion_FrontmatterContainsAuto()
    {
        var target = new KiroTargetComponent(FakeTemplates);
        var model = new KiroDocumentModel
        {
            Description = "Auto-decided document",
            Inclusion = "auto",
            Rules = [new KiroRuleProseModel { PrimaryText = "Contextual guidance." }],
        };

        var output = await target.RenderDocumentAsync(model);

        Assert.Contains("inclusion: auto", output);
    }

    [Fact]
    public async Task RenderDocument_OutputContainsPrimaryText()
    {
        var target = new KiroTargetComponent(FakeTemplates);
        var model = new KiroDocumentModel
        {
            Description = "My rules",
            Inclusion = "always",
            Rules =
            [
                new KiroRuleProseModel { PrimaryText = "Write clean code." },
                new KiroRuleProseModel { PrimaryText = "Document all APIs." },
            ],
        };

        var output = await target.RenderDocumentAsync(model);

        Assert.Contains("Write clean code.", output);
        Assert.Contains("Document all APIs.", output);
    }

    [Fact]
    public async Task RenderDocument_NoRuleIds_OutputDoesNotContainIdPatterns()
    {
        var target = new KiroTargetComponent(FakeTemplates);
        var model = new KiroDocumentModel
        {
            Description = "Guidelines",
            Inclusion = "always",
            Rules =
            [
                new KiroRuleProseModel { PrimaryText = "Use dependency injection." },
                new KiroRuleProseModel { PrimaryText = "Prefer immutable data." },
            ],
        };

        var output = await target.RenderDocumentAsync(model);

        Assert.DoesNotMatch(@"\b[A-Z]+-\d{3}\b", output);
    }

    [Fact]
    public async Task GenerateWithPlanAsync_DeprecatedRules_AreExcludedFromOutput()
    {
        var target = new KiroTargetComponent(FakeTemplates);
        var outputDir = Path.Combine(Path.GetTempPath(), $"kiro-unit-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(outputDir);
            var model = new ResolvedSteeringModel
            {
                Documents =
                [
                    new SteeringDocument
                    {
                        Id = "test-doc",
                        Title = "Test Document",
                        SourcePath = "test-doc.md",
                        Rules =
                        [
                            new SteeringRule { Id = "R-001", Severity = "error", PrimaryText = "Active rule." },
                            new SteeringRule { Id = "R-002", Severity = "warning", PrimaryText = "Deprecated rule.", Deprecated = true },
                        ],
                    },
                ],
                Rules =
                [
                    new SteeringRule { Id = "R-001", Severity = "error", PrimaryText = "Active rule.", InputFileStem = "test-doc" },
                    new SteeringRule { Id = "R-002", Severity = "warning", PrimaryText = "Deprecated rule.", Deprecated = true, InputFileStem = "test-doc" },
                ],
                ActiveProfiles = [],
            };

            var config = new TargetConfiguration
            {
                Id = "kiro",
                Enabled = true,
                OutputPath = outputDir,
            };

            await target.GenerateWithPlanAsync(model, config, BuildWritePlan(model), CancellationToken.None);

            var files = Directory.GetFiles(outputDir, "*.md");
            Assert.Single(files);
            var content = await File.ReadAllTextAsync(files[0]);
            Assert.Contains("Active rule.", content);
            Assert.DoesNotContain("Deprecated rule.", content);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateWithPlanAsync_EmptyDocumentAfterFiltering_SkipsDocumentOutput()
    {
        var target = new KiroTargetComponent(FakeTemplates);
        var outputDir = Path.Combine(Path.GetTempPath(), $"kiro-empty-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(outputDir);
            var model = new ResolvedSteeringModel
            {
                Documents =
                [
                    new SteeringDocument
                    {
                        Id = "all-deprecated",
                        Title = "All Deprecated",
                        SourcePath = "all-deprecated.md",
                        Rules =
                        [
                            new SteeringRule { Id = "R-001", PrimaryText = "Old rule.", Deprecated = true },
                        ],
                    },
                ],
                Rules =
                [
                    new SteeringRule { Id = "R-001", PrimaryText = "Old rule.", Deprecated = true, InputFileStem = "all-deprecated" },
                ],
                ActiveProfiles = [],
            };

            var config = new TargetConfiguration
            {
                Id = "kiro",
                Enabled = true,
                OutputPath = outputDir,
            };

            await target.GenerateWithPlanAsync(model, config, BuildWritePlan(model), CancellationToken.None);

            var files = Directory.GetFiles(outputDir, "*.md");
            Assert.Empty(files);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public void InclusionMapper_WithFileMatchOption_ReturnsFileMatchInclusion()
    {
        var rules = new List<SteeringRule>
        {
            new() { Id = "R-001", PrimaryText = "Rule text." },
        };
        var options = new KiroTargetOptions
        {
            InclusionMode = KiroInclusionMode.FileMatch,
            FileMatchPattern = "src/**/*.cs",
        };

        var (inclusion, pattern) = KiroInclusionMapper.Map(rules, options);

        Assert.Equal("fileMatch", inclusion);
        Assert.Equal("src/**/*.cs", pattern);
    }

    [Fact]
    public void InclusionMapper_WithAppliesTo_InfersFileMatchInclusion()
    {
        var rules = new List<SteeringRule>
        {
            new() { Id = "R-001", PrimaryText = "Rule text.", AppliesTo = ["**/*.ts", "**/*.tsx"] },
        };
        var options = new KiroTargetOptions { InclusionMode = KiroInclusionMode.Always };

        var (inclusion, pattern) = KiroInclusionMapper.Map(rules, options);

        Assert.Equal("fileMatch", inclusion);
        Assert.NotNull(pattern);
        Assert.Contains("**/*.ts", pattern);
    }

    [Fact]
    public void InclusionMapper_AutoMode_ReturnsAutoInclusion()
    {
        var rules = new List<SteeringRule>
        {
            new() { Id = "R-001", PrimaryText = "Rule text." },
        };
        var options = new KiroTargetOptions { InclusionMode = KiroInclusionMode.Auto };

        var (inclusion, pattern) = KiroInclusionMapper.Map(rules, options);

        Assert.Equal("auto", inclusion);
        Assert.Null(pattern);
    }

    [Fact]
    public void KiroTargetOptions_FromFormatOptions_ParsesInclusionMode()
    {
        var options = KiroTargetOptions.FromFormatOptions(new Dictionary<string, string>
        {
            ["inclusionMode"] = "fileMatch",
            ["fileMatchPattern"] = "**/*.cs",
        });

        Assert.Equal(KiroInclusionMode.FileMatch, options.InclusionMode);
        Assert.Equal("**/*.cs", options.FileMatchPattern);
    }

    private sealed class InlineKiroTemplateProvider(string documentTemplate) : ITemplateProvider
    {
        public string GetTemplate(string targetId, string templateName) =>
            templateName switch
            {
                "document" => documentTemplate,
                _ => throw new InvalidOperationException($"Unknown template '{templateName}'."),
            };
    }

    private static WritePlan BuildWritePlan(ResolvedSteeringModel model) => new()
    {
        TargetId = "kiro",
        Files = model.Documents
            .Select(document => new WritePlanFile
            {
                Path = $"{Path.GetFileNameWithoutExtension(document.SourcePath ?? document.Id ?? "steering")}.md",
                AppendUnits = document.Rules
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
