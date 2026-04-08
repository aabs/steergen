using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]
public sealed class RunLayoutConventionsAcceptanceTests
{
    private static readonly string RoutingFixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance", "RoutingLayouts"));

    private static string MakeTempDir() =>
        Directory.CreateTempSubdirectory("layout-conventions-test-").FullName;

    /// Routes everything to "override-output.md" in the given directory.
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

    [Fact]
    public async Task WorkspaceLocal_OverrideYamlInProjectDir_AppliesCustomRouting()
    {
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            var overridePath = Path.Combine(workspace, "my-layout.yaml");
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
            Assert.True(File.Exists(Path.Combine(outputDir, "override-output.md")),
                "override-output.md should exist when workspace-local layout override is configured");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task UserHomeGlobal_OverrideYamlInIsolatedHomeDir_AppliesCustomRouting()
    {
        var homeDir = MakeTempDir();
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            var overridePath = Path.Combine(homeDir, "shared-layout.yaml");
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
            Assert.True(File.Exists(Path.Combine(outputDir, "override-output.md")),
                "override-output.md should exist when home-global layout override is referenced by absolute path");
        }
        finally
        {
            if (Directory.Exists(homeDir)) Directory.Delete(homeDir, recursive: true);
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task MixedScope_DifferentOverridePerTarget_EachTargetUsesItsOwnLayout()
    {
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            var speckitOverridePath = Path.Combine(workspace, "speckit-override.yaml");
            await File.WriteAllTextAsync(speckitOverridePath, SingleFileLayoutYaml(workspace));
            await File.WriteAllTextAsync(Path.Combine(workspace, "mixed.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md")));

            var configPath = Path.Combine(workspace, "steergen.config.yaml");
            await new SteergenConfigWriter().WriteAsync(configPath, new SteeringConfiguration
            {
                GlobalRoot = workspace,
                Targets =
                [
                    new TargetConfiguration { Id = "speckit", Enabled = true, OutputPath = outputDir, LayoutOverridePath = speckitOverridePath },
                    new TargetConfiguration { Id = "kiro",    Enabled = true, OutputPath = outputDir, LayoutOverridePath = null },
                ],
            });

            var exitCode = await RunCommand.RunAsync(configPath, null, null, outputDir, ["speckit", "kiro"], quiet: true, cancellationToken: default);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(Path.Combine(outputDir, "override-output.md")),
                "speckit should use its custom layout (override-output.md)");

            var kiroFiles = Directory.GetFiles(Path.Combine(outputDir, ".kiro", "steering"), "*.md");
            Assert.True(kiroFiles.Length > 0, "kiro should use its default layout and produce .md files");
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
        var workspace = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(workspace, "mixed.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md")));

            // No config, no override — RunCommand sets OutputPath = outputDir, layout routes to outputDir/.specify/memory/
            var exitCode = await RunCommand.RunAsync(null, workspace, null, outputDir, ["speckit"], quiet: true, cancellationToken: default);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(Path.Combine(outputDir, ".specify", "memory", "constitution.md")),
                "Default layout should route domain=core rules to .specify/memory/constitution.md");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }
}
