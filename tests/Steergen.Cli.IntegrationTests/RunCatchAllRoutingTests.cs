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

    [Fact]
    public async Task Run_CatchAllFixture_CoreDomainRoutesBeatCatchAll()
    {
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(globalRoot, "catch-all.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "catch-all-fixture.md")));

            await RunCommand.RunAsync(null, globalRoot, null, outputDir, ["speckit"], quiet: true, cancellationToken: default);

            var constitutionPath = Path.Combine(outputDir, ".speckit", "memory", "constitution.md");
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
            await File.WriteAllTextAsync(
                Path.Combine(globalRoot, "catch-all.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "catch-all-fixture.md")));

            await RunCommand.RunAsync(null, globalRoot, null, outputDir, ["speckit"], quiet: true, cancellationToken: default);

            var frontendPath = Path.Combine(outputDir, ".speckit", "memory", "frontend.md");
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
            await File.WriteAllTextAsync(
                Path.Combine(globalRoot, "fallback.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "fallback-fixture.md")));

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
            await File.WriteAllTextAsync(
                Path.Combine(globalRoot, "catch-all.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "catch-all-fixture.md")));

            await RunCommand.RunAsync(null, globalRoot, null, outputDir, ["speckit"], quiet: true, cancellationToken: default);

            var otherMd = Path.Combine(outputDir, ".speckit", "memory", "other.md");
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
}
