using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Templates;
using Xunit;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]
public sealed class RunLayoutOverrideTests
{
    private static readonly string RoutingFixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance", "RoutingLayouts"));

    private static string MakeTempDir() =>
        Directory.CreateTempSubdirectory("layout-override-test-").FullName;

    /// Routes everything to "custom-output.md" in the given directory.
    private static string SingleFileLayoutYaml(string dir) => $"""
        version: "1.0"
        roots:
          globalRoot: "{dir}"
          projectRoot: "{dir}"
          targetRoot: "{dir}"
        routes:
          - id: core-anchor
            scope: both
            explicit: true
            anchor: core
            order: 10
            match:
              domain: core
            destination:
              directory: "{dir}"
              fileName: "custom-output"
              extension: ".md"
          - id: catch-all
            scope: both
            explicit: false
            order: 99
            match:
              domain: "*"
            destination:
              directory: "{dir}"
              fileName: "custom-output"
              extension: ".md"
        fallback:
          mode: other-at-core-anchor
          fileBaseName: other
        purge:
          roots: []
          globs: []
        """;

    [Fact]
    public async Task Run_OverrideOnSpeckit_SpeckitUsesCustomLayout()
    {
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            var overridePath = Path.Combine(workspace, "speckit-override.yaml");
            await File.WriteAllTextAsync(overridePath, SingleFileLayoutYaml(workspace));
            await File.WriteAllTextAsync(Path.Combine(workspace, "mixed.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md")));

            var configPath = Path.Combine(workspace, "steergen.config.yaml");
            await new SteergenConfigWriter().WriteAsync(configPath, new SteeringConfiguration
            {
                GlobalRoot = workspace,
                Targets =
                [
                    new TargetConfiguration
                    {
                        Id = "speckit", Enabled = true,
                        OutputPath = outputDir,
                        LayoutOverridePath = overridePath,
                    },
                ],
            });

            var exitCode = await RunCommand.RunAsync(configPath, null, null, outputDir, ["speckit"], quiet: true, cancellationToken: default);

            Assert.Equal(0, exitCode);
            // Override routes to workspace/custom-output.md; stripping workspace prefix → custom-output.md under outputDir
            Assert.True(File.Exists(Path.Combine(outputDir, "custom-output.md")),
                "speckit should write to custom-output.md when override is active");
            Assert.False(File.Exists(Path.Combine(outputDir, "constitution.md")),
                "constitution.md should not exist when custom single-file override is active");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_OverrideOnSpeckit_KiroUsesDefaultLayout()
    {
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            var overridePath = Path.Combine(workspace, "speckit-override.yaml");
            await File.WriteAllTextAsync(overridePath, SingleFileLayoutYaml(workspace));
            await File.WriteAllTextAsync(Path.Combine(workspace, "mixed.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md")));

            var configPath = Path.Combine(workspace, "steergen.config.yaml");
            await new SteergenConfigWriter().WriteAsync(configPath, new SteeringConfiguration
            {
                GlobalRoot = workspace,
                Targets =
                [
                    new TargetConfiguration { Id = "speckit", Enabled = true, OutputPath = outputDir, LayoutOverridePath = overridePath },
                    new TargetConfiguration { Id = "kiro",    Enabled = true, OutputPath = outputDir, LayoutOverridePath = null },
                ],
            });

            var exitCode = await RunCommand.RunAsync(configPath, null, null, outputDir, ["speckit", "kiro"], quiet: true, cancellationToken: default);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(Path.Combine(outputDir, "custom-output.md")),
                "speckit custom-output.md should exist with override");

            // kiro default layout → .kiro/steering/ under outputDir
            var kiroFiles = Directory.GetFiles(Path.Combine(outputDir, ".kiro", "steering"), "*.md");
            Assert.True(kiroFiles.Length > 0,
                "kiro should produce .md files using the default layout (not affected by speckit override)");
            Assert.False(File.Exists(Path.Combine(outputDir, ".kiro", "steering", "custom-output.md")),
                "kiro should not have custom-output.md");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_RelativeLayoutOverridePath_ResolvedRelativeToConfigDirectory()
    {
        var workspace = MakeTempDir();
        var subDir = Path.Combine(workspace, "layouts");
        Directory.CreateDirectory(subDir);
        var outputDir = MakeTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(subDir, "my-override.yaml"), SingleFileLayoutYaml(workspace));
            await File.WriteAllTextAsync(Path.Combine(workspace, "mixed.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md")));

            var configPath = Path.Combine(workspace, "steergen.config.yaml");
            await new SteergenConfigWriter().WriteAsync(configPath, new SteeringConfiguration
            {
                GlobalRoot = workspace,
                Targets =
                [
                    new TargetConfiguration
                    {
                        Id = "speckit", Enabled = true,
                        OutputPath = outputDir,
                        LayoutOverridePath = "layouts/my-override.yaml",
                    },
                ],
            });

            var exitCode = await RunCommand.RunAsync(configPath, null, null, outputDir, ["speckit"], quiet: true, cancellationToken: default);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(Path.Combine(outputDir, "custom-output.md")),
                "Relative layoutOverridePath should be resolved from the config file directory");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithOverride_RouteResolutionsReportMergedProvenance()
    {
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            var overridePath = Path.Combine(workspace, "speckit-override.yaml");
            await File.WriteAllTextAsync(overridePath, SingleFileLayoutYaml(workspace));
            await File.WriteAllTextAsync(Path.Combine(workspace, "mixed.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md")));

            var targetConfig = new Core.Model.TargetConfiguration
            {
                Id = "speckit", Enabled = true,
                OutputPath = outputDir,
                LayoutOverridePath = overridePath,
            };

            var result = await new Core.Generation.GenerationPipeline().RunAsync(
                globalDocuments: LoadDocuments(workspace),
                projectDocuments: [],
                activeProfiles: [],
                targets: [new Core.Targets.Speckit.SpeckitTargetComponent(new EmbeddedTemplateProvider())],
                targetConfigs: [targetConfig],
                cancellationToken: default);

            Assert.NotNull(result.RouteResolutions);
            var speckitResolutions = result.RouteResolutions["speckit"];
            var resolvedResults = speckitResolutions.Where(r => r.IsResolved).ToList();
            Assert.True(resolvedResults.Count > 0);
            Assert.All(resolvedResults, r =>
                Assert.True(r.Source == Core.Model.RouteProvenance.Merged,
                    $"Rule '{r.RuleId}' should have Source=Merged when override is active, got {r.Source}"));
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithoutOverride_RouteResolutionsReportDefaultProvenance()
    {
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(workspace, "mixed.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md")));

            var result = await new Core.Generation.GenerationPipeline().RunAsync(
                globalDocuments: LoadDocuments(workspace),
                projectDocuments: [],
                activeProfiles: [],
                targets: [new Core.Targets.Speckit.SpeckitTargetComponent(new EmbeddedTemplateProvider())],
                targetConfigs:
                [
                    new Core.Model.TargetConfiguration
                    {
                        Id = "speckit", Enabled = true,
                        OutputPath = outputDir,
                        LayoutOverridePath = null,
                    }
                ],
                cancellationToken: default);

            var resolvedResults = result.RouteResolutions!["speckit"].Where(r => r.IsResolved).ToList();
            Assert.True(resolvedResults.Count > 0);
            Assert.All(resolvedResults, r =>
                Assert.True(r.Source == Core.Model.RouteProvenance.Default,
                    $"Rule '{r.RuleId}' should have Source=Default when no override, got {r.Source}"));
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    private static IReadOnlyList<Core.Model.SteeringDocument> LoadDocuments(string? root)
    {
        if (root is null || !Directory.Exists(root)) return [];
        return Directory
            .EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(path => Core.Parsing.SteeringMarkdownParser.Parse(File.ReadAllText(path), path))
            .ToList();
    }
}
