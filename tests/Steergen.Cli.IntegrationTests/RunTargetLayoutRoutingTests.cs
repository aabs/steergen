using Steergen.Cli.Commands;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]
public sealed class RunTargetLayoutRoutingTests
{
    private static readonly string RoutingFixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance", "RoutingLayouts"));

    private static string MakeTempDir() =>
        Directory.CreateTempSubdirectory("routing-layout-test-").FullName;

    private static async Task WriteMixedDomainsFixtureToDirAsync(string dir)
    {
        var sourcePath = Path.Combine(RoutingFixturesRoot, "mixed-domains-fixture.md");
        await File.WriteAllTextAsync(Path.Combine(dir, "mixed-domains-fixture.md"),
            await File.ReadAllTextAsync(sourcePath));
    }

    private static string MemoryDir(string outputDir) =>
        Path.Combine(outputDir, ".specify", "memory");

    [Fact]
    public async Task Run_MixedDomainsFixture_ExitCode0()
    {
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await WriteMixedDomainsFixtureToDirAsync(globalRoot);

            var exitCode = await RunCommand.RunAsync(
                configPath: null, globalRoot: globalRoot, projectRoot: null,
                outputBase: outputDir, explicitTargets: ["speckit"], quiet: true, cancellationToken: default);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            if (Directory.Exists(globalRoot)) Directory.Delete(globalRoot, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_MixedDomainsFixture_CoreDomainRulesRouteToConstitution()
    {
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await WriteMixedDomainsFixtureToDirAsync(globalRoot);

            await RunCommand.RunAsync(
                configPath: null, globalRoot: globalRoot, projectRoot: null,
                outputBase: outputDir, explicitTargets: ["speckit"], quiet: true, cancellationToken: default);

            var constitutionPath = Path.Combine(MemoryDir(outputDir), "constitution.md");
            Assert.True(File.Exists(constitutionPath), "constitution.md should exist for domain=core rules");

            var content = await File.ReadAllTextAsync(constitutionPath);
            Assert.Contains("MIX-001", content);
            Assert.Contains("MIX-004", content);
        }
        finally
        {
            if (Directory.Exists(globalRoot)) Directory.Delete(globalRoot, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_MixedDomainsFixture_NonCoreDomainRulesRouteToDomainModules()
    {
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await WriteMixedDomainsFixtureToDirAsync(globalRoot);

            await RunCommand.RunAsync(
                configPath: null, globalRoot: globalRoot, projectRoot: null,
                outputBase: outputDir, explicitTargets: ["speckit"], quiet: true, cancellationToken: default);

            var memDir = MemoryDir(outputDir);
            Assert.True(File.Exists(Path.Combine(memDir, "security.md")),
                "security.md should exist for MIX-002 (domain=security)");
            Assert.True(File.Exists(Path.Combine(memDir, "operations.md")),
                "operations.md should exist for MIX-003 (domain=operations)");
            Assert.True(File.Exists(Path.Combine(memDir, "quality.md")),
                "quality.md should exist for MIX-005 (domain=quality)");
        }
        finally
        {
            if (Directory.Exists(globalRoot)) Directory.Delete(globalRoot, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_MixedDomainsFixture_CatchAllRoutesUnknownDomainsToNamedModules()
    {
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await WriteMixedDomainsFixtureToDirAsync(globalRoot);

            await RunCommand.RunAsync(
                configPath: null, globalRoot: globalRoot, projectRoot: null,
                outputBase: outputDir, explicitTargets: ["speckit"], quiet: true, cancellationToken: default);

            var memDir = MemoryDir(outputDir);
            Assert.True(File.Exists(Path.Combine(memDir, "cloud.md")),
                "cloud.md should exist for MIX-006 (domain=cloud, routed via catch-all)");
            Assert.True(File.Exists(Path.Combine(memDir, "cicd.md")),
                "cicd.md should exist for MIX-007 (domain=cicd, routed via catch-all)");
            Assert.True(File.Exists(Path.Combine(memDir, "data-platform.md")),
                "data-platform.md should exist for MIX-008 (domain=data-platform, routed via catch-all)");
        }
        finally
        {
            if (Directory.Exists(globalRoot)) Directory.Delete(globalRoot, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_MixedDomainsFixture_DeterministicOutputOnRepeatedRuns()
    {
        var globalRoot = MakeTempDir();
        var outputDir1 = MakeTempDir();
        var outputDir2 = MakeTempDir();
        try
        {
            await WriteMixedDomainsFixtureToDirAsync(globalRoot);

            await RunCommand.RunAsync(null, globalRoot, null, outputDir1, ["speckit"], true, cancellationToken: default);
            await RunCommand.RunAsync(null, globalRoot, null, outputDir2, ["speckit"], true, cancellationToken: default);

            var files1 = Directory.GetFiles(MemoryDir(outputDir1), "*.md").Select(Path.GetFileName).OrderBy(f => f).ToArray();
            var files2 = Directory.GetFiles(MemoryDir(outputDir2), "*.md").Select(Path.GetFileName).OrderBy(f => f).ToArray();

            Assert.Equal(files1, files2);

            foreach (var fileName in files1)
            {
                var content1 = await File.ReadAllTextAsync(Path.Combine(MemoryDir(outputDir1), fileName!));
                var content2 = await File.ReadAllTextAsync(Path.Combine(MemoryDir(outputDir2), fileName!));
                Assert.True(content1 == content2, $"File '{fileName}' content differs between runs.");
            }
        }
        finally
        {
            if (Directory.Exists(globalRoot)) Directory.Delete(globalRoot, recursive: true);
            if (Directory.Exists(outputDir1)) Directory.Delete(outputDir1, recursive: true);
            if (Directory.Exists(outputDir2)) Directory.Delete(outputDir2, recursive: true);
        }
    }

    [Fact]
    public async Task Run_MixedDomainsFixture_EachRuleAppearsInExactlyOneOutputFile()
    {
        var globalRoot = MakeTempDir();
        var outputDir = MakeTempDir();
        try
        {
            await WriteMixedDomainsFixtureToDirAsync(globalRoot);

            await RunCommand.RunAsync(
                configPath: null, globalRoot: globalRoot, projectRoot: null,
                outputBase: outputDir, explicitTargets: ["speckit"], quiet: true, cancellationToken: default);

            var allFiles = Directory.GetFiles(MemoryDir(outputDir), "*.md");
            var ruleIdsToFileCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var file in allFiles)
            {
                var content = await File.ReadAllTextAsync(file);
                foreach (var ruleId in new[] { "MIX-001", "MIX-002", "MIX-003", "MIX-004", "MIX-005", "MIX-006", "MIX-007", "MIX-008" })
                {
                    if (content.Contains(ruleId))
                        ruleIdsToFileCounts[ruleId] = ruleIdsToFileCounts.GetValueOrDefault(ruleId, 0) + 1;
                }
            }

            foreach (var (ruleId, count) in ruleIdsToFileCounts)
                Assert.True(count == 1, $"Rule '{ruleId}' appears in {count} output files — expected exactly 1.");
        }
        finally
        {
            if (Directory.Exists(globalRoot)) Directory.Delete(globalRoot, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }
}
