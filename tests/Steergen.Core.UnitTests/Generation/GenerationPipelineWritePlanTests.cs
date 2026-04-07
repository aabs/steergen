using Steergen.Core.Generation;
using Steergen.Core.Model;
using Steergen.Core.Targets;

namespace Steergen.Core.UnitTests.Generation;

public sealed class GenerationPipelineWritePlanTests
{
    [Fact]
    public async Task RunAsync_KiroCatchAllWritePlan_UsesLayoutRootedDestinationWithoutOutputPrefix()
    {
        var globalRoot = Directory.CreateTempSubdirectory("pipeline-global-").FullName;
        var outputPath = Directory.CreateTempSubdirectory("pipeline-output-").FullName;

        try
        {
            var sourcePath = Path.Combine(globalRoot, "accessibility-standards.md");
            var globalDocuments = new[]
            {
                new SteeringDocument
                {
                    Id = "accessibility-doc",
                    SourcePath = sourcePath,
                    Rules =
                    [
                        new SteeringRule
                        {
                            Id = "ACC-001",
                            Domain = "accessibility",
                            Severity = "info",
                            PrimaryText = "Accessibility guidance.",
                        },
                    ],
                },
            };

            var captureTarget = new CaptureWritePlanTargetComponent("kiro");
            var pipeline = new GenerationPipeline();

            var result = await pipeline.RunAsync(
                globalDocuments,
                projectDocuments: [],
                activeProfiles: [],
                targets: [captureTarget],
                targetConfigs:
                [
                    new TargetConfiguration
                    {
                        Id = "kiro",
                        Enabled = true,
                        OutputPath = outputPath,
                    },
                ],
                cancellationToken: default,
                globalRoot: globalRoot,
                projectRoot: null);

            Assert.True(result.Success);
            Assert.NotNull(captureTarget.CapturedWritePlan);

            var writePlan = captureTarget.CapturedWritePlan!;
            Assert.Equal("kiro", writePlan.TargetId);
            Assert.Equal(globalRoot, writePlan.GlobalRoot);
            Assert.Single(writePlan.Files);

            var plannedPath = writePlan.Files[0].Path;
            Assert.Equal(
                Path.Combine(globalRoot, ".kiro", "steering", "accessibility-standards.md"),
                plannedPath);
            Assert.DoesNotContain(
                Path.Combine("kiro", ".kiro", "steering"),
                plannedPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(globalRoot)) Directory.Delete(globalRoot, recursive: true);
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, recursive: true);
        }
    }

    private sealed class CaptureWritePlanTargetComponent(string targetId) : ITargetComponent
    {
        public string TargetId => targetId;
        public TargetDescriptor Descriptor => new(targetId, targetId, "Captures write plans for tests.");
        public WritePlan? CapturedWritePlan { get; private set; }

        public Task GenerateAsync(ResolvedSteeringModel model, TargetConfiguration config, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task GenerateWithPlanAsync(
            ResolvedSteeringModel model,
            TargetConfiguration config,
            WritePlan writePlan,
            CancellationToken cancellationToken)
        {
            CapturedWritePlan = writePlan;
            return Task.CompletedTask;
        }
    }
}