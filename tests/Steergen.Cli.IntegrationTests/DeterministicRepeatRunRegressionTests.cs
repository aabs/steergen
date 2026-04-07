using Steergen.Cli.Commands;

namespace Steergen.Cli.IntegrationTests;

/// <summary>
/// Determinism regression harness: runs steergen multiple times on identical inputs and
/// verifies that all generated outputs are byte-identical across runs.
///
/// Supports the 99.9% repeat-run reliability target (NFR repeatability).
/// A test failure here indicates a non-deterministic code path in the generation pipeline.
/// </summary>
[Collection("CliOutput")]
public sealed class DeterministicRepeatRunRegressionTests
{
    private const int RepeatCount = 3;

    private static readonly string FixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance"));

    private static string MakeTempDir() =>
        Directory.CreateTempSubdirectory("determinism-test-").FullName;

    // ── Speckit determinism ───────────────────────────────────────────────────────

    [Fact]
    public async Task SpeckitTarget_RepeatRuns_ProduceIdenticalFileSet()
    {
        var outputDirs = Enumerable.Range(0, RepeatCount)
            .Select(_ => MakeTempDir())
            .ToArray();
        try
        {
            foreach (var outputDir in outputDirs)
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

            AssertOutputsAreIdentical(outputDirs, "speckit");
        }
        finally
        {
            foreach (var dir in outputDirs)
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task KiroTarget_RepeatRuns_ProduceIdenticalFileSet()
    {
        var outputDirs = Enumerable.Range(0, RepeatCount)
            .Select(_ => MakeTempDir())
            .ToArray();
        try
        {
            foreach (var outputDir in outputDirs)
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

            AssertOutputsAreIdentical(outputDirs, "kiro");
        }
        finally
        {
            foreach (var dir in outputDirs)
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AllTargets_RepeatRuns_ProduceIdenticalFileSets()
    {
        var outputDirs = Enumerable.Range(0, RepeatCount)
            .Select(_ => MakeTempDir())
            .ToArray();
        try
        {
            foreach (var outputDir in outputDirs)
            {
                var exitCode = await RunCommand.RunAsync(
                    configPath: null,
                    globalRoot: Path.Combine(FixturesRoot, "global"),
                    projectRoot: Path.Combine(FixturesRoot, "project"),
                    outputBase: outputDir,
                    explicitTargets: ["speckit", "kiro"],
                    quiet: true,
                    cancellationToken: default);

                Assert.Equal(0, exitCode);
            }

            AssertOutputsAreIdentical(outputDirs, "");
        }
        finally
        {
            foreach (var dir in outputDirs)
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static void AssertOutputsAreIdentical(string[] outputDirs, string targetSubdir)
    {
        // With layout routing, files land under target-specific subdirs like .speckit/memory or .kiro/steering
        // Search recursively from the output root for .md files
        var referenceDir = outputDirs[0];
        Assert.True(Directory.Exists(referenceDir),
            $"Reference output directory must exist after run 1");

        var referenceFiles = Directory
            .GetFiles(referenceDir, "*.md", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(referenceDir, f))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        Assert.True(referenceFiles.Count > 0,
            $"Run 1 must produce at least one .md file");

        foreach (var otherOutputDir in outputDirs.Skip(1))
        {
            Assert.True(Directory.Exists(otherOutputDir),
                $"Output directory must exist in every repeat run");

            var otherFiles = Directory
                .GetFiles(otherOutputDir, "*.md", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(otherOutputDir, f))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(referenceFiles, otherFiles);

            foreach (var relativePath in referenceFiles)
            {
                var refContent = File.ReadAllText(Path.Combine(referenceDir, relativePath));
                var otherContent = File.ReadAllText(Path.Combine(otherOutputDir, relativePath));
                Assert.True(
                    refContent == otherContent,
                    $"File '{relativePath}' must be byte-identical across all runs. " +
                    $"Non-determinism detected in repeat run.");
            }
        }
    }
}
