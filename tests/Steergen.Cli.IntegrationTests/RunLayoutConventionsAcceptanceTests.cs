using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;

namespace Steergen.Cli.IntegrationTests;

/// <summary>
/// Acceptance tests validating three layout override conventions:
/// - Workspace-local: layout YAML in the project working directory, referenced by relative path.
/// - User-home global: layout YAML stored in an isolated "home" directory, referenced by absolute path.
/// - Mixed-scope: per-target overrides mixing workspace-local and home-directory paths.
///
/// All tests verify that the override affects routing output without requiring code changes —
/// pure configuration-driven behavior.
/// </summary>
[Collection("CliOutput")]
public sealed class RunLayoutConventionsAcceptanceTests
{
    private static readonly string RoutingFixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance", "RoutingLayouts"));

    private static string MakeTempDir() =>
        Directory.CreateTempSubdirectory("layout-conventions-test-").FullName;

    /// <summary>
    /// A minimal override layout that routes everything to a single file named "override-output.md"
    /// to make it easy to assert that the custom layout was applied.
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
              fileName: "override-output"
              extension: ".md"
          - id: catch-all
            scope: both
            explicit: false
            order: 99
            match:
              domain: "*"
            destination:
              directory: "{dir}"
              fileName: "override-output"
              extension: ".md"
        fallback:
          mode: other-at-core-anchor
          fileBaseName: other
        purge:
          roots: []
          globs: []
        """;

    // ── Convention 1: workspace-local override ────────────────────────────────────

    [Fact]
    public async Task WorkspaceLocal_OverrideYamlInProjectDir_AppliesCustomRouting()
    {
        // Arrange: place override.yaml next to the config file in the workspace dir.
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            var layoutYaml = SingleFileLayoutYaml(workspace);
            var overridePath = Path.Combine(workspace, "my-layout.yaml");
            await File.WriteAllTextAsync(overridePath, layoutYaml);

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
                        LayoutOverridePath = overridePath,   // workspace-local convention
                    },
                ],
            });

            // Act
            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            // Assert: custom layout was applied (all rules routed to override-output.md)
            Assert.Equal(0, exitCode);
            var overrideOutput = Path.Combine(outputDir, "speckit", "override-output.md");
            Assert.True(File.Exists(overrideOutput),
                "override-output.md should exist when workspace-local layout override is configured");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    // ── Convention 2: user-home global override ───────────────────────────────────

    [Fact]
    public async Task UserHomeGlobal_OverrideYamlInIsolatedHomeDir_AppliesCustomRouting()
    {
        // Arrange: simulate a "home" directory holding the shared layout YAML.
        var homeDir = MakeTempDir();
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            // Place override YAML in the simulated home directory (absolute path)
            var layoutYaml = SingleFileLayoutYaml(workspace);
            var overridePath = Path.Combine(homeDir, "shared-layout.yaml");
            await File.WriteAllTextAsync(overridePath, layoutYaml);

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
                        LayoutOverridePath = overridePath,   // absolute path — home-global convention
                    },
                ],
            });

            // Act
            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            // Assert
            Assert.Equal(0, exitCode);
            var overrideOutput = Path.Combine(outputDir, "speckit", "override-output.md");
            Assert.True(File.Exists(overrideOutput),
                "override-output.md should exist when home-global layout override is referenced by absolute path");
        }
        finally
        {
            if (Directory.Exists(homeDir)) Directory.Delete(homeDir, recursive: true);
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    // ── Convention 3: mixed-scope (per-target overrides) ─────────────────────────

    [Fact]
    public async Task MixedScope_DifferentOverridePerTarget_EachTargetUsesItsOwnLayout()
    {
        // Arrange: speckit gets an override; kiro uses the default.
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            var layoutYaml = SingleFileLayoutYaml(workspace);
            var speckitOverridePath = Path.Combine(workspace, "speckit-override.yaml");
            await File.WriteAllTextAsync(speckitOverridePath, layoutYaml);

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
                        LayoutOverridePath = speckitOverridePath,   // custom layout
                    },
                    new TargetConfiguration
                    {
                        Id = "kiro",
                        Enabled = true,
                        OutputPath = Path.Combine(outputDir, "kiro"),
                        LayoutOverridePath = null,   // default layout
                    },
                ],
            });

            // Act
            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit", "kiro"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);

            // Speckit: custom layout → override-output.md should exist
            Assert.True(File.Exists(Path.Combine(outputDir, "speckit", "override-output.md")),
                "speckit should use its custom layout (override-output.md)");

            // Kiro: default layout → should produce .md files via default behaviour
            var kiroDir = Path.Combine(outputDir, "kiro");
            Assert.True(Directory.Exists(kiroDir) && Directory.GetFiles(kiroDir, "*.md").Length > 0,
                "kiro should use its default layout and produce .md files");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task WorkspaceLocal_WithoutOverride_DefaultLayoutApplied()
    {
        // Ensures that when layoutOverridePath is null, the built-in default layout is used.
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(workspace, "mixed.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md")));

            // Act — no config file, no override, pure defaults
            var exitCode = await RunCommand.RunAsync(
                configPath: null,
                globalRoot: workspace,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);
            // Default layout routes domain=core → constitution.md
            Assert.True(File.Exists(Path.Combine(outputDir, "speckit", "constitution.md")),
                "Default layout should route domain=core rules to constitution.md when no override is set");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }
}
