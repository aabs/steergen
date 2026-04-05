using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Generation;
using Steergen.Core.Model;
using Steergen.Core.Parsing;
using Steergen.Templates;

namespace Steergen.Cli.IntegrationTests;

/// <summary>
/// Integration tests verifying that catch-all routes capture rules before the
/// <c>other-at-core-anchor</c> fallback is applied, and that specific routes
/// take precedence over catch-all routes.
/// </summary>
[Collection("CliOutput")]
public sealed class RunCatchAllRoutingTests
{
    private static readonly string RoutingFixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance", "RoutingLayouts"));

    private static string MakeTempDir() =>
        Directory.CreateTempSubdirectory("catchall-routing-test-").FullName;

    // ── Catch-all routing: all rules land in named destination files ─────────────

    [Fact]
    public async Task Run_CatchAllFixture_CoreDomainRoutesBeatCatchAll()
    {
        // default speckit layout has domain=core → constitution.md (explicit, anchor=core)
        // AND domain=* catch-all. Explicit route must win for domain=core.
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(globalRoot, "catch-all.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "catch-all-fixture.md")));

            await RunCommand.RunAsync(
                configPath: null,
                globalRoot: globalRoot,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            var constitutionPath = Path.Combine(outputDir, "speckit", "constitution.md");
            Assert.True(File.Exists(constitutionPath),
                "constitution.md must exist — domain=core route beats catch-all");
            var content = await File.ReadAllTextAsync(constitutionPath);
            Assert.Contains("RLAY-001", content);
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
        // RLAY-006 (domain=frontend) and RLAY-007 (domain=frontend) have no explicit
        // route — the default speckit layout routes them via domain-module-global (domain=*)
        // catch-all to frontend.md.
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(globalRoot, "catch-all.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "catch-all-fixture.md")));

            await RunCommand.RunAsync(
                configPath: null,
                globalRoot: globalRoot,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            var frontendPath = Path.Combine(outputDir, "speckit", "frontend.md");
            Assert.True(File.Exists(frontendPath),
                "frontend.md should exist — catch-all (domain=*) captures domain=frontend rules");
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
        // Use a custom layout with no wildcard catch-all route.
        // Only domain=core has a specific route; all other rules fall back to other.md.
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(globalRoot, "fallback.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "fallback-fixture.md")));

            var layoutYaml = """
                version: "1.0"
                roots:
                  globalRoot: "${globalRoot}"
                  projectRoot: "${projectRoot}"
                  targetRoot: "${globalRoot}"
                routes:
                  - id: core-anchor
                    scope: both
                    explicit: true
                    anchor: core
                    order: 10
                    match:
                      domain: core
                    destination:
                      directory: "${globalRoot}"
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
            var writer = new SteergenConfigWriter();
            await writer.WriteAsync(configPath, new SteeringConfiguration
            {
                GlobalRoot = globalRoot,
                Targets =
                [
                    new TargetConfiguration
                    {
                        Id = "speckit",
                        Enabled = true,
                        OutputPath = Path.Combine(outputDir, "speckit"),
                        LayoutOverridePath = layoutPath,
                    },
                ],
            });

            await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            var otherMdPath = Path.Combine(outputDir, "speckit", "other.md");
            Assert.True(File.Exists(otherMdPath),
                "other.md should exist — unmatched rules fall back to other-at-core-anchor");

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
        // With the default layout (domain=* catch-all), no rule should fall back to other.md.
        // Every rule gets a named destination from the catch-all route.
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(globalRoot, "catch-all.md"),
                await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "catch-all-fixture.md")));

            await RunCommand.RunAsync(
                configPath: null,
                globalRoot: globalRoot,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            var speckitDir = Path.Combine(outputDir, "speckit");
            var otherMd = Path.Combine(speckitDir, "other.md");
            Assert.True(!File.Exists(otherMd),
                "other.md should NOT exist — catch-all (domain=*) routes everything, leaving nothing for fallback");
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
        // Direct pipeline-level test: all rules from catch-all-fixture get a resolved path.
        var source = await File.ReadAllTextAsync(Path.Combine(RoutingFixturesRoot, "catch-all-fixture.md"));
        var doc = SteeringMarkdownParser.Parse(source, "catch-all.md");
        var rules = doc.Rules.ToList();

        var layoutLoader = new LayoutOverrideLoader();
        var layout = await layoutLoader.LoadAsync("speckit", null, default);

        var planner = new RoutePlanner();
        var resolutions = planner.Plan(rules, layout);

        Assert.All(resolutions, r =>
            Assert.True(r.IsResolved,
                $"Rule '{r.RuleId}' was not resolved — every rule must have a route (specific or catch-all)."));
    }
}
