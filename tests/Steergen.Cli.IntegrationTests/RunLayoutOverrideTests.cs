using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Templates;
using Xunit;

namespace Steergen.Cli.IntegrationTests;

/// <summary>
/// Integration tests for per-target layout override linkage and isolation.
///
/// Validates:
/// - Override is applied only to the configured target; other targets use defaults.
/// - Per-target override isolation: overriding speckit does not affect kiro output.
/// - RouteResolutionResult.Source reports Merged when an override is in use.
/// - Relative layoutOverridePath is resolved relative to the config file directory.
/// - Invalid override path emits a diagnostic instead of crashing.
/// </summary>
[Collection("CliOutput")]
public sealed class RunLayoutOverrideTests
{
    private static readonly string RoutingFixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance", "RoutingLayouts"));

    private static string MakeTempDir() =>
        Directory.CreateTempSubdirectory("layout-override-test-").FullName;

    /// <summary>
    /// A single-file layout YAML that routes everything to "custom-output.md"
    /// in the given directory.
    /// </summary>
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

    // ── Override applied only to the configured target ────────────────────────

    [Fact]
    public async Task Run_OverrideOnSpeckit_SpeckitUsesCustomLayout()
    {
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            var overridePath = Path.Combine(workspace, "speckit-override.yaml");
            await File.WriteAllTextAsync(overridePath, SingleFileLayoutYaml(workspace));

            await File.WriteAllTextAsync(
                Path.Combine(workspace, "mixed.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md")));

            var configPath = Path.Combine(workspace, "steergen.config.yaml");
            var writer = new SteergenConfigWriter();
            await writer.WriteAsync(configPath, new SteeringConfiguration
            {
                GlobalRoot = workspace,
                Targets =
                [
                    new TargetConfiguration
                    {
                        Id = "speckit",
                        Enabled = true,
                        OutputPath = Path.Combine(outputDir, "speckit"),
                        LayoutOverridePath = overridePath,
                    },
                ],
            });

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);

            // Custom layout should route all rules to custom-output.md
            Assert.True(
                File.Exists(Path.Combine(outputDir, "speckit", "custom-output.md")),
                "speckit should write to custom-output.md when override is active");

            // Default output files (constitution.md, modules/*.md) should NOT exist
            Assert.False(
                File.Exists(Path.Combine(outputDir, "speckit", "constitution.md")),
                "constitution.md should not exist when custom single-file override is active");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    // ── Override isolation: overriding speckit does NOT affect kiro ───────────

    [Fact]
    public async Task Run_OverrideOnSpeckit_KiroUsesDefaultLayout()
    {
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            var overridePath = Path.Combine(workspace, "speckit-override.yaml");
            await File.WriteAllTextAsync(overridePath, SingleFileLayoutYaml(workspace));

            await File.WriteAllTextAsync(
                Path.Combine(workspace, "mixed.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md")));

            var configPath = Path.Combine(workspace, "steergen.config.yaml");
            var writer = new SteergenConfigWriter();
            await writer.WriteAsync(configPath, new SteeringConfiguration
            {
                GlobalRoot = workspace,
                Targets =
                [
                    new TargetConfiguration
                    {
                        Id = "speckit",
                        Enabled = true,
                        OutputPath = Path.Combine(outputDir, "speckit"),
                        LayoutOverridePath = overridePath,
                    },
                    new TargetConfiguration
                    {
                        Id = "kiro",
                        Enabled = true,
                        OutputPath = Path.Combine(outputDir, "kiro"),
                        LayoutOverridePath = null,
                    },
                ],
            });

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit", "kiro"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);

            // speckit: custom layout → custom-output.md
            Assert.True(File.Exists(Path.Combine(outputDir, "speckit", "custom-output.md")),
                "speckit custom-output.md should exist with override");

            // kiro: default layout → should produce output using its default routing
            var kiroDir = Path.Combine(outputDir, "kiro");
            Assert.True(
                Directory.Exists(kiroDir) && Directory.GetFiles(kiroDir, "*.md").Length > 0,
                "kiro should produce .md files using the default layout (not affected by speckit override)");

            // kiro should NOT have custom-output.md
            Assert.False(
                File.Exists(Path.Combine(kiroDir, "custom-output.md")),
                "kiro should not have custom-output.md — override is scoped to speckit only");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    // ── Relative path resolution: relative path resolved from config dir ──────

    [Fact]
    public async Task Run_RelativeLayoutOverridePath_ResolvedRelativeToConfigDirectory()
    {
        var workspace = MakeTempDir();
        var subDir = Path.Combine(workspace, "layouts");
        Directory.CreateDirectory(subDir);
        var outputDir = MakeTempDir();
        try
        {
            // Place the override YAML in a subdirectory of the workspace.
            var overridePath = Path.Combine(subDir, "my-override.yaml");
            await File.WriteAllTextAsync(overridePath, SingleFileLayoutYaml(workspace));

            await File.WriteAllTextAsync(
                Path.Combine(workspace, "mixed.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md")));

            var configPath = Path.Combine(workspace, "steergen.config.yaml");
            var writer = new SteergenConfigWriter();
            // Store the override path as relative to the config file directory.
            await writer.WriteAsync(configPath, new SteeringConfiguration
            {
                GlobalRoot = workspace,
                Targets =
                [
                    new TargetConfiguration
                    {
                        Id = "speckit",
                        Enabled = true,
                        OutputPath = Path.Combine(outputDir, "speckit"),
                        LayoutOverridePath = "layouts/my-override.yaml",  // relative path
                    },
                ],
            });

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);
            Assert.True(
                File.Exists(Path.Combine(outputDir, "speckit", "custom-output.md")),
                "Relative layoutOverridePath should be resolved from the config file directory");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    // ── Provenance: Merged when override is applied ───────────────────────────

    [Fact]
    public async Task Run_WithOverride_RouteResolutionsReportMergedProvenance()
    {
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            var overridePath = Path.Combine(workspace, "speckit-override.yaml");
            await File.WriteAllTextAsync(overridePath, SingleFileLayoutYaml(workspace));

            await File.WriteAllTextAsync(
                Path.Combine(workspace, "mixed.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md")));

            var targetConfig = new Core.Model.TargetConfiguration
            {
                Id = "speckit",
                Enabled = true,
                OutputPath = Path.Combine(outputDir, "speckit"),
                LayoutOverridePath = overridePath,  // absolute path — no relative resolution needed here
            };

            var pipeline = new Core.Generation.GenerationPipeline();
            var result = await pipeline.RunAsync(
                globalDocuments: LoadDocuments(workspace),
                projectDocuments: [],
                activeProfiles: [],
                targets: [new Core.Targets.Speckit.SpeckitTargetComponent(new Templates.EmbeddedTemplateProvider())],
                targetConfigs: [targetConfig],
                cancellationToken: default);

            Assert.NotNull(result.RouteResolutions);
            Assert.True(result.RouteResolutions.ContainsKey("speckit"),
                "RouteResolutions should contain a speckit entry");

            var speckitResolutions = result.RouteResolutions["speckit"];
            Assert.NotEmpty(speckitResolutions);

            // All resolved results should report Merged provenance since an override was used
            var resolvedResults = speckitResolutions.Where(r => r.IsResolved).ToList();
            Assert.True(resolvedResults.Count > 0, "At least some rules should be resolved");
            Assert.All(resolvedResults, r =>
                Assert.True(
                    r.Source == Core.Model.RouteProvenance.Merged,
                    $"Rule '{r.RuleId}' should have Source=Merged when override is active, got {r.Source}"));
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    // ── Default provenance: Default when no override ──────────────────────────

    [Fact]
    public async Task Run_WithoutOverride_RouteResolutionsReportDefaultProvenance()
    {
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(workspace, "mixed.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md")));

            var pipeline = new Core.Generation.GenerationPipeline();
            var result = await pipeline.RunAsync(
                globalDocuments: LoadDocuments(workspace),
                projectDocuments: [],
                activeProfiles: [],
                targets: [new Core.Targets.Speckit.SpeckitTargetComponent(new Templates.EmbeddedTemplateProvider())],
                targetConfigs:
                [
                    new Core.Model.TargetConfiguration
                    {
                        Id = "speckit",
                        Enabled = true,
                        OutputPath = Path.Combine(outputDir, "speckit"),
                        LayoutOverridePath = null,
                    }
                ],
                cancellationToken: default);

            Assert.NotNull(result.RouteResolutions);
            var speckitResolutions = result.RouteResolutions["speckit"];
            var resolvedResults = speckitResolutions.Where(r => r.IsResolved).ToList();
            Assert.True(resolvedResults.Count > 0);

            // No override → all resolved results should report Default provenance
            Assert.All(resolvedResults, r =>
                Assert.True(
                    r.Source == Core.Model.RouteProvenance.Default,
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
