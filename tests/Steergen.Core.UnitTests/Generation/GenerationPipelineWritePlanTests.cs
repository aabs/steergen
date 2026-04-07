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

    [Fact]
    public async Task RunAsync_KiroProjectCatchAllWritePlan_UsesProjectLayoutRootWithoutOutputPrefix()
    {
        var projectRoot = Directory.CreateTempSubdirectory("pipeline-project-").FullName;
        var outputPath = Directory.CreateTempSubdirectory("pipeline-output-").FullName;

        try
        {
            var sourcePath = Path.Combine(projectRoot, "testing-standards.md");
            var projectDocuments = new[]
            {
                new SteeringDocument
                {
                    Id = "testing-doc",
                    SourcePath = sourcePath,
                    Rules =
                    [
                        new SteeringRule
                        {
                            Id = "TEST-001",
                            Domain = "testing",
                            Severity = "info",
                            PrimaryText = "Testing guidance.",
                        },
                    ],
                },
            };

            var captureTarget = new CaptureWritePlanTargetComponent("kiro");
            var pipeline = new GenerationPipeline();

            var result = await pipeline.RunAsync(
                globalDocuments: [],
                projectDocuments,
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
                globalRoot: null,
                projectRoot: projectRoot);

            Assert.True(result.Success);
            Assert.NotNull(captureTarget.CapturedWritePlan);

            var writePlan = captureTarget.CapturedWritePlan!;
            Assert.Equal("kiro", writePlan.TargetId);
            Assert.Equal(projectRoot, writePlan.ProjectRoot);
            Assert.Single(writePlan.Files);

            var plannedPath = writePlan.Files[0].Path;
            Assert.Equal(
                Path.Combine(projectRoot, ".kiro", "steering", "testing-standards.md"),
                plannedPath);
            Assert.DoesNotContain(
                Path.Combine("kiro", ".kiro", "steering"),
                plannedPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(projectRoot)) Directory.Delete(projectRoot, recursive: true);
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_KiroProjectCatchAllWritePlan_DoesNotUseGlobalRouteForProjectDocuments()
    {
        var globalRoot = Directory.CreateTempSubdirectory("pipeline-global-").FullName;
        var projectRoot = Directory.CreateTempSubdirectory("pipeline-project-").FullName;
        var outputPath = Directory.CreateTempSubdirectory("pipeline-output-").FullName;

        try
        {
            var projectDocuments = new[]
            {
                new SteeringDocument
                {
                    Id = "testing-doc",
                    SourcePath = Path.Combine(projectRoot, "testing-standards.md"),
                    Rules =
                    [
                        new SteeringRule
                        {
                            Id = "TEST-001",
                            Domain = "testing",
                            Severity = "info",
                            PrimaryText = "Testing guidance.",
                        },
                    ],
                },
            };

            var captureTarget = new CaptureWritePlanTargetComponent("kiro");
            var pipeline = new GenerationPipeline();

            var result = await pipeline.RunAsync(
                globalDocuments: [],
                projectDocuments,
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
                projectRoot: projectRoot);

            Assert.True(result.Success);
            var plannedPath = Assert.Single(captureTarget.CapturedWritePlan!.Files).Path;
            Assert.DoesNotContain("${globalRoot}", plannedPath, StringComparison.Ordinal);
            Assert.StartsWith(projectRoot, plannedPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(globalRoot)) Directory.Delete(globalRoot, recursive: true);
            if (Directory.Exists(projectRoot)) Directory.Delete(projectRoot, recursive: true);
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