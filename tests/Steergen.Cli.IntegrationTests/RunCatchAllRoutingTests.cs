using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Generation;
using Steergen.Core.Model;
using Steergen.Core.Parsing;
using Steergen.Templates;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]
public sealed class RunCatchAllRoutingTests
{
    private static readonly string RoutingFixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance", "RoutingLayouts"));

    private static string MakeTempDir() =>
        Directory.CreateTempSubdirectory("catchall-routing-test-").FullName;

    private static async Task WriteFixtureAsync(string destinationPath, string fixtureFileName)
    {
        await File.WriteAllTextAsync(
            destinationPath,
            await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, fixtureFileName)));
    }

    [Fact]
    public async Task Run_CatchAllFixture_CoreDomainRoutesBeatCatchAll()
    {
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await WriteFixtureAsync(Path.Combine(globalRoot, "catch-all.md"), "catch-all-fixture.md");

            await RunCommand.RunAsync(null, globalRoot, null, outputDir, ["speckit"], quiet: true, cancellationToken: default);

            var constitutionPath = Path.Combine(outputDir, ".specify", "memory", "constitution.md");
            Assert.True(File.Exists(constitutionPath), "constitution.md must exist — domain=core route beats catch-all");
            Assert.Contains("RLAY-001", await File.ReadAllTextAsync(constitutionPath));
        }
        finally
        {
            if (Directory.Exists(globalRoot)) Directory.Delete(globalRoot, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_CatchAllFixture_UnrecognizedDomainsLandInNamedCatchAllFiles()
    {
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await WriteFixtureAsync(Path.Combine(globalRoot, "catch-all.md"), "catch-all-fixture.md");

            await RunCommand.RunAsync(null, globalRoot, null, outputDir, ["speckit"], quiet: true, cancellationToken: default);

            var frontendPath = Path.Combine(outputDir, ".specify", "memory", "frontend.md");
            Assert.True(File.Exists(frontendPath), "frontend.md should exist — catch-all captures domain=frontend rules");
            var content = await File.ReadAllTextAsync(frontendPath);
            Assert.Contains("RLAY-006", content);
            Assert.Contains("RLAY-007", content);
        }
        finally
        {
            if (Directory.Exists(globalRoot)) Directory.Delete(globalRoot, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_CatchAllFixture_NoCatchAllLayout_UnmatchedRulesFallBackToOtherMd()
    {
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await WriteFixtureAsync(Path.Combine(globalRoot, "fallback.md"), "fallback-fixture.md");

            // Custom layout: only domain=core route, no catch-all — unmatched rules fall back to other.md
            var layoutYaml = $"""
                version: "1.0"
                roots:
                  globalRoot: "{globalRoot}"
                  projectRoot: "{globalRoot}"
                  targetRoot: "{globalRoot}"
                routes:
                  - id: core-anchor
                    scope: both
                    explicit: true
                    anchor: core
                    order: 10
                    match:
                      domain: core
                    destination:
                      directory: "{globalRoot}"
                      fileName: "constitution"
                      extension: ".md"
                fallback:
                  mode: other-at-core-anchor
                  fileBaseName: other
                purge:
                  roots: []
                  globs: []
                """;
            var layoutPath = Path.Combine(globalRoot, "test-layout.yaml");
            await File.WriteAllTextAsync(layoutPath, layoutYaml);

            var configPath = Path.Combine(globalRoot, "steergen.config.yaml");
            await new SteergenConfigWriter().WriteAsync(configPath, new SteeringConfiguration
            {
                GlobalRoot = globalRoot,
                Targets =
                [
                    new TargetConfiguration
                    {
                        Id = "speckit",
                        Enabled = true,
                        OutputPath = outputDir,
                        LayoutOverridePath = layoutPath,
                    },
                ],
            });

            await RunCommand.RunAsync(configPath, null, null, outputDir, ["speckit"], quiet: true, cancellationToken: default);

            // With this layout, globalRoot is the destination root; stripping it leaves just "other.md"
            var otherMdPath = Path.Combine(outputDir, "other.md");
            Assert.True(File.Exists(otherMdPath), "other.md should exist — unmatched rules fall back to other-at-core-anchor");
            var otherContent = await File.ReadAllTextAsync(otherMdPath);
            Assert.Contains("FALL-001", otherContent);
            Assert.Contains("FALL-002", otherContent);
            Assert.Contains("FALL-003", otherContent);
        }
        finally
        {
            if (Directory.Exists(globalRoot)) Directory.Delete(globalRoot, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_CatchAllFixture_FallbackNeverActivatesWhenCatchAllPresent()
    {
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await WriteFixtureAsync(Path.Combine(globalRoot, "catch-all.md"), "catch-all-fixture.md");

            await RunCommand.RunAsync(null, globalRoot, null, outputDir, ["speckit"], quiet: true, cancellationToken: default);

            var otherMd = Path.Combine(outputDir, ".specify", "memory", "other.md");
            Assert.False(File.Exists(otherMd),
                "other.md should NOT exist — catch-all routes everything, leaving nothing for fallback");
        }
        finally
        {
            if (Directory.Exists(globalRoot)) Directory.Delete(globalRoot, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task WritePlan_CatchAllLayout_AllRulesResolved()
    {
        var source = await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "catch-all-fixture.md"));
        var doc = SteeringMarkdownParser.Parse(source, "catch-all.md");
        var rules = doc.Rules.ToList();

        var layout = await new LayoutOverrideLoader().LoadAsync("speckit", null, default);
        var resolutions = new RoutePlanner().Plan(rules, layout);

        Assert.All(resolutions, r =>
            Assert.True(r.IsResolved, $"Rule '{r.RuleId}' was not resolved — every rule must have a route."));
    }

    [Fact]
    public async Task Run_KiroCatchAllWithoutOutput_WritesToLayoutPathWithoutTargetPrefix()
    {
        var workspace = MakeTempDir();
        var globalRoot = MakeTempDir();
        try
        {
            await WriteFixtureAsync(Path.Combine(globalRoot, "accessibility-standards.md"), "catch-all-fixture.md");

            using var scope = new CurrentDirectoryScope(workspace);

            var exitCode = await RunCommand.RunAsync(
                configPath: null,
                globalRoot: globalRoot,
                projectRoot: null,
                outputBase: null,
                explicitTargets: ["kiro"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);

            var expectedPath = Path.Combine(workspace, ".kiro", "steering", "accessibility-standards.md");
            var incorrectPath = Path.Combine(workspace, "kiro", ".kiro", "steering", "accessibility-standards.md");

            Assert.True(File.Exists(expectedPath),
                "Kiro catch-all output should be written directly under .kiro/steering when no --output is supplied.");
            Assert.False(File.Exists(incorrectPath),
                "Kiro catch-all output must not be nested under an extra target directory prefix.");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            if (Directory.Exists(globalRoot)) Directory.Delete(globalRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Run_KiroWithLegacyConfiguredOutputPath_IgnoresTargetIdPrefixWhenNoCliOutputIsSupplied()
    {
        var workspace = MakeTempDir();
        var globalRoot = Path.Combine(workspace, "steering", "global");
        var projectRoot = Path.Combine(workspace, "steering", "project");
        Directory.CreateDirectory(globalRoot);
        Directory.CreateDirectory(projectRoot);

        try
        {
            await WriteFixtureAsync(Path.Combine(globalRoot, "accessibility-standards.md"), "catch-all-fixture.md");

            var configPath = Path.Combine(workspace, "steergen.config.yaml");
            await new Steergen.Core.Configuration.SteergenConfigWriter().WriteAsync(configPath, new Steergen.Core.Model.SteeringConfiguration
            {
                GlobalRoot = globalRoot,
                ProjectRoot = projectRoot,
                RegisteredTargets = ["kiro"],
                Targets =
                [
                    new Steergen.Core.Model.TargetConfiguration
                    {
                        Id = "kiro",
                        Enabled = true,
                        OutputPath = "kiro",
                    },
                ],
            });

            using var scope = new CurrentDirectoryScope(workspace);

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: null,
                explicitTargets: [],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);

            var expectedPath = Path.Combine(workspace, ".kiro", "steering", "accessibility-standards.md");
            var incorrectPrefixedPath = Path.Combine(workspace, "kiro", ".kiro", "steering", "accessibility-standards.md");
            var incorrectFlatPath = Path.Combine(workspace, "kiro", "accessibility-standards.md");

            Assert.True(File.Exists(expectedPath));
            Assert.False(File.Exists(incorrectPrefixedPath));
            Assert.False(File.Exists(incorrectFlatPath));
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithConfiguredGenerationRoot_WritesRoutedFilesRelativeToGenerationRoot()
    {
        var workspace = MakeTempDir();
        var docsRoot = Path.Combine(workspace, "docs", "steering");
        var globalRoot = Path.Combine(docsRoot, "global");
        var projectRoot = Path.Combine(docsRoot, "project");
        Directory.CreateDirectory(globalRoot);
        Directory.CreateDirectory(projectRoot);

        try
        {
            await WriteFixtureAsync(Path.Combine(globalRoot, "accessibility-standards.md"), "catch-all-fixture.md");

            var configPath = Path.Combine(workspace, "steergen.config.yaml");
            await new SteergenConfigWriter().WriteAsync(configPath, new SteeringConfiguration
            {
                GlobalRoot = globalRoot,
                ProjectRoot = projectRoot,
                GenerationRoot = workspace,
                RegisteredTargets = ["kiro"],
            });

            using var scope = new CurrentDirectoryScope(projectRoot);

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: null,
                explicitTargets: [],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);

            var expectedPath = Path.Combine(workspace, ".kiro", "steering", "accessibility-standards.md");
            var incorrectPath = Path.Combine(projectRoot, ".kiro", "steering", "accessibility-standards.md");

            Assert.True(File.Exists(expectedPath));
            Assert.False(File.Exists(incorrectPath));
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>
    /// Regression test: when steergen.config.yaml uses RELATIVE paths for projectRoot and
    /// generationRoot (e.g. projectRoot: "docs/steering", generationRoot: "."), the generated
    /// files must land under generationRoot, not nested inside projectRoot.
    /// Previously, the relative plan path "docs/steering/.kiro/..." was not stripped of its
    /// root prefix, causing output to be written inside the source tree.
    /// </summary>
    [Fact]
    public async Task Run_WithRelativeProjectRootAndRelativeGenerationRoot_OutputDoesNotNestUnderProjectRoot()
    {
        var workspace = MakeTempDir();
        var projectDir = Path.Combine(workspace, "docs", "steering");
        Directory.CreateDirectory(projectDir);

        try
        {
            await WriteFixtureAsync(
                Path.Combine(projectDir, "accessibility-standards.md"),
                "catch-all-fixture.md");

            var configPath = Path.Combine(workspace, "steergen.config.yaml");
            // Use relative paths exactly as a user would when running from the solution root.
            await new SteergenConfigWriter().WriteAsync(configPath, new SteeringConfiguration
            {
                ProjectRoot = Path.Combine("docs", "steering"),  // relative
                GenerationRoot = ".",                             // relative — solution root
                RegisteredTargets = ["kiro"],
            });

            // CWD = workspace (solution root), matching how the user would invoke steergen.
            using var scope = new CurrentDirectoryScope(workspace);

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: null,
                explicitTargets: [],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);

            // Output must be at <workspace>/.kiro/steering/, NOT inside docs/steering/.kiro/...
            var expectedPath = Path.Combine(workspace, ".kiro", "steering", "accessibility-standards.md");
            var nestedWrongPath = Path.Combine(workspace, "docs", "steering", ".kiro", "steering", "accessibility-standards.md");

            Assert.True(File.Exists(expectedPath),
                $"File should be at solution root .kiro/steering/, got nothing at {expectedPath}");
            Assert.False(File.Exists(nestedWrongPath),
                "File must not be nested under docs/steering/.kiro/ — root prefix was not stripped");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        }
    }
}
