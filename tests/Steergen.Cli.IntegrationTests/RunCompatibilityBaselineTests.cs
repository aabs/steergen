using Steergen.Cli.Commands;

namespace Steergen.Cli.IntegrationTests;

/// <summary>
/// Compatibility regression tests verifying that existing targets produce the same
/// output files when no layout override is configured.
/// These tests guard against regressions introduced by the dynamic layout engine.
/// </summary>
[Collection("CliOutput")]
public sealed class RunCompatibilityBaselineTests
{
    private static readonly string FixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance"));

    private static string MakeTempDir() =>
        Directory.CreateTempSubdirectory("compat-baseline-test-").FullName;

    // ── Speckit backward compatibility ───────────────────────────────────────────

    [Fact]
    public async Task Run_SpeckitTarget_NoOverride_ExitCode0()
    {
        var outputDir = MakeTempDir();
        try
        {
            var exitCode = await RunCommand.RunAsync(
                configPath: null,
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Run_SpeckitTarget_NoOverride_ConstitutionMdExists()
    {
        var outputDir = MakeTempDir();
        try
        {
            await RunCommand.RunAsync(
                configPath: null,
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            Assert.True(File.Exists(Path.Combine(outputDir, "speckit", "constitution.md")),
                "constitution.md must still be produced for domain=core rules");
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Run_SpeckitTarget_NoOverride_CoreRulesInConstitution()
    {
        var outputDir = MakeTempDir();
        try
        {
            await RunCommand.RunAsync(
                configPath: null,
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            var content = await File.ReadAllTextAsync(
                Path.Combine(outputDir, "speckit", "constitution.md"));

            Assert.Contains("CORE-001", content);
            Assert.Contains("CORE-002", content);
            Assert.Contains("CORE-003", content);
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Run_SpeckitTarget_NoOverride_DomainModulesProducedForNonCoreRules()
    {
        var outputDir = MakeTempDir();
        try
        {
            await RunCommand.RunAsync(
                configPath: null,
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            var speckitDir = Path.Combine(outputDir, "speckit");
            var outputFiles = Directory.GetFiles(speckitDir, "*.md")
                .Select(Path.GetFileName)
                .OrderBy(f => f)
                .ToArray();

            // At least constitution.md plus one or more domain modules
            Assert.True(outputFiles.Length >= 2,
                $"Expected constitution.md plus domain modules; got: {string.Join(", ", outputFiles)}");
            Assert.Contains("constitution.md", outputFiles);
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    // ── Kiro backward compatibility ──────────────────────────────────────────────

    [Fact]
    public async Task Run_KiroTarget_NoOverride_ExitCode0()
    {
        var outputDir = MakeTempDir();
        try
        {
            var exitCode = await RunCommand.RunAsync(
                configPath: null,
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                outputBase: outputDir,
                explicitTargets: ["kiro"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Run_KiroTarget_NoOverride_ProducesMarkdownFiles()
    {
        var outputDir = MakeTempDir();
        try
        {
            await RunCommand.RunAsync(
                configPath: null,
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                outputBase: outputDir,
                explicitTargets: ["kiro"],
                quiet: true,
                cancellationToken: default);

            var kiroDir = Path.Combine(outputDir, "kiro");
            var mdFiles = Directory.GetFiles(kiroDir, "*.md");
            Assert.True(mdFiles.Length > 0,
                "Kiro target should produce at least one .md file from realistic fixtures");
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    // ── Repeated run stability ───────────────────────────────────────────────────

    [Fact]
    public async Task Run_SpeckitTarget_NoOverride_OutputIsStableAcrossRuns()
    {
        var outputDir1 = MakeTempDir();
        var outputDir2 = MakeTempDir();
        try
        {
            var opts = (
                configPath: (string?)null,
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                explicitTargets: (IReadOnlyList<string>)["speckit"]);

            await RunCommand.RunAsync(
                configPath: opts.configPath,
                globalRoot: opts.globalRoot,
                projectRoot: opts.projectRoot,
                outputBase: outputDir1,
                explicitTargets: opts.explicitTargets,
                quiet: true,
                cancellationToken: default);

            await RunCommand.RunAsync(
                configPath: opts.configPath,
                globalRoot: opts.globalRoot,
                projectRoot: opts.projectRoot,
                outputBase: outputDir2,
                explicitTargets: opts.explicitTargets,
                quiet: true,
                cancellationToken: default);

            var files1 = Directory.GetFiles(Path.Combine(outputDir1, "speckit"), "*.md")
                .Select(Path.GetFileName).OrderBy(f => f).ToArray();
            var files2 = Directory.GetFiles(Path.Combine(outputDir2, "speckit"), "*.md")
                .Select(Path.GetFileName).OrderBy(f => f).ToArray();

            Assert.Equal(files1, files2);
        }
        finally
        {
            if (Directory.Exists(outputDir1)) Directory.Delete(outputDir1, recursive: true);
            if (Directory.Exists(outputDir2)) Directory.Delete(outputDir2, recursive: true);
        }
    }
}
