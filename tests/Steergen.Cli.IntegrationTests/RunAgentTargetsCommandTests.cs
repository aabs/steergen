using Steergen.Cli.Composition;
using Steergen.Core.Generation;
using Steergen.Core.Model;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Agents;
using Steergen.Templates;

namespace Steergen.Cli.IntegrationTests;

public sealed class RunAgentTargetsCommandTests
{
    private static readonly string FixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance"));

    [Fact]
    public async Task CopilotAgent_WithRealisticFixtures_ProducesCopilotInstructionsFile()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"copilot-agent-integ-{Guid.NewGuid():N}");
        try
        {
            var service = new CopilotAgentGenerationService();
            var result = await service.RunAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider());

            Assert.True(result.Success,
                $"Generation failed: {string.Join("; ", result.Diagnostics.Select(d => d.Message))}");
            Assert.True(Directory.Exists(outputDir), "Output directory should be created");
            var instructionsFile = Path.Combine(outputDir, "copilot-instructions.md");
            if (!File.Exists(instructionsFile))
                instructionsFile = Directory.GetFiles(outputDir, "copilot-instructions.md", SearchOption.AllDirectories).FirstOrDefault();
            Assert.True(File.Exists(instructionsFile), "copilot-instructions.md should exist");
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task CopilotAgent_OutputContainsRuleProse()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"copilot-prose-{Guid.NewGuid():N}");
        try
        {
            var service = new CopilotAgentGenerationService();
            await service.RunAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider());

            var instructionsFile = Path.Combine(outputDir, "copilot-instructions.md");
            if (!File.Exists(instructionsFile))
            {
                var found = Directory.GetFiles(outputDir, "copilot-instructions.md", SearchOption.AllDirectories).FirstOrDefault();
                if (found != null)
                    instructionsFile = found;
            }
            var content = await File.ReadAllTextAsync(instructionsFile);
            Assert.NotEmpty(content.Trim());
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task KiroAgent_WithRealisticFixtures_ProducesPerDocumentFiles()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"kiro-agent-integ-{Guid.NewGuid():N}");
        try
        {
            var service = new KiroAgentGenerationService();
            var result = await service.RunAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider());

            Assert.True(result.Success,
                $"Generation failed: {string.Join("; ", result.Diagnostics.Select(d => d.Message))}");
            Assert.True(Directory.Exists(outputDir), "Output directory should be created");
            var files = Directory.GetFiles(outputDir, "*.md", SearchOption.AllDirectories);
            Assert.NotEmpty(files);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task KiroAgent_OutputFiles_HaveFrontmatterWithDescription()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"kiro-agent-fm-{Guid.NewGuid():N}");
        try
        {
            var service = new KiroAgentGenerationService();
            await service.RunAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider());

            foreach (var file in Directory.GetFiles(outputDir, "*.md", SearchOption.AllDirectories))
            {
                var content = await File.ReadAllTextAsync(file);
                Assert.Contains("description:", content);
            }
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task CopilotAgent_MissingRequiredMetadata_ThrowsTargetGenerationException()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"copilot-meta-{Guid.NewGuid():N}");
        try
        {
            var component = new CopilotAgentTargetComponent(new EmbeddedTemplateProvider());
            var model = new ResolvedSteeringModel
            {
                Documents =
                [
                    new SteeringDocument
                    {
                        Id = "test",
                        Title = "Test",
                        SourcePath = "test.md",
                        Rules =
                        [
                            new SteeringRule { Id = "R-001", Severity = "error", PrimaryText = "Rule text." },
                        ],
                    },
                ],
                Rules = [],
                ActiveProfiles = [],
            };

            var config = new TargetConfiguration
            {
                Id = "copilot-agent",
                Enabled = true,
                OutputPath = outputDir,
                RequiredMetadata = ["description"],
            };

            await Assert.ThrowsAsync<TargetGenerationException>(() =>
                component.GenerateWithPlanAsync(model, config, EmptyPlan("copilot-agent"), CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public void ExitCodeMapper_TargetGenerationException_ReturnsGenerationError()
    {
        var ex = new TargetGenerationException("description");

        var exitCode = ExitCodeMapper.FromException(ex);

        Assert.Equal(ExitCodeMapper.GenerationError, exitCode);
    }

    [Fact]
    public async Task KiroAgent_MissingRequiredMetadata_ThrowsTargetGenerationException()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"kiro-agent-meta-{Guid.NewGuid():N}");
        try
        {
            var component = new KiroAgentTargetComponent(new EmbeddedTemplateProvider());
            var model = new ResolvedSteeringModel
            {
                Documents =
                [
                    new SteeringDocument
                    {
                        Id = "test",
                        Title = "Test",
                        SourcePath = "test.md",
                        Rules =
                        [
                            new SteeringRule { Id = "R-001", Severity = "error", PrimaryText = "Rule text." },
                        ],
                    },
                ],
                Rules = [],
                ActiveProfiles = [],
            };

            var config = new TargetConfiguration
            {
                Id = "kiro-agent",
                Enabled = true,
                OutputPath = outputDir,
                RequiredMetadata = ["description"],
            };

            await Assert.ThrowsAsync<TargetGenerationException>(() =>
                component.GenerateWithPlanAsync(model, config, EmptyPlan("kiro-agent"), CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    private static WritePlan EmptyPlan(string targetId) => new()
    {
        TargetId = targetId,
        Files = [],
    };
}
