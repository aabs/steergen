using Steergen.Core.Generation;

namespace Steergen.Cli.IntegrationTests;

public sealed class RunFixtureTargetCommandTests
{
    private static readonly string FixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance"));

    [Fact]
    public async Task Run_WithRealisticFixtures_Succeeds()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"fixture-integ-{Guid.NewGuid():N}");
        try
        {
            var service = new FixtureGenerationService();
            var result = await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir);

            Assert.True(result.Success,
                $"Generation failed: {string.Join("; ", result.Diagnostics.Select(d => d.Message))}");
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithRealisticFixtures_CreatesManifestFile()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"fixture-manifest-{Guid.NewGuid():N}");
        try
        {
            var service = new FixtureGenerationService();
            await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir);

            var manifestPath = Path.Combine(outputDir, "fixture-manifest.txt");
            if (!File.Exists(manifestPath))
                manifestPath = Directory.GetFiles(outputDir, "fixture-manifest.txt", SearchOption.AllDirectories).FirstOrDefault();
            Assert.True(File.Exists(manifestPath), "fixture-manifest.txt should exist in output directory");
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_ManifestContainsKnownRuleIds()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"fixture-ids-{Guid.NewGuid():N}");
        try
        {
            var service = new FixtureGenerationService();
            await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir);

            var manifestPath = Path.Combine(outputDir, "fixture-manifest.txt");
            if (!File.Exists(manifestPath))
            {
                var found = Directory.GetFiles(outputDir, "fixture-manifest.txt", SearchOption.AllDirectories).FirstOrDefault();
                if (found != null)
                    manifestPath = found;
            }

            var lines = await File.ReadAllLinesAsync(manifestPath);
            Assert.Contains("CORE-001", lines);
            Assert.Contains("CORE-002", lines);
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_ManifestIsAlphabeticallySorted()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"fixture-sorted-{Guid.NewGuid():N}");
        try
        {
            var service = new FixtureGenerationService();
            await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir);

            var manifestPath = Path.Combine(outputDir, "fixture-manifest.txt");
            if (!File.Exists(manifestPath))
            {
                var found = Directory.GetFiles(outputDir, "fixture-manifest.txt", SearchOption.AllDirectories).FirstOrDefault();
                if (found != null)
                    manifestPath = found;
            }

            var lines = await File.ReadAllLinesAsync(manifestPath);
            var sorted = lines.OrderBy(l => l, StringComparer.Ordinal).ToArray();
            Assert.Equal(sorted, lines);
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_ManifestHasNoEmptyLines()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"fixture-empty-{Guid.NewGuid():N}");
        try
        {
            var service = new FixtureGenerationService();
            await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir);

            var manifestPath = Path.Combine(outputDir, "fixture-manifest.txt");
            if (!File.Exists(manifestPath))
            {
                var found = Directory.GetFiles(outputDir, "fixture-manifest.txt", SearchOption.AllDirectories).FirstOrDefault();
                if (found != null)
                    manifestPath = found;
            }

            var lines = await File.ReadAllLinesAsync(manifestPath);
            Assert.DoesNotContain(lines, string.IsNullOrWhiteSpace);
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_MissingGlobalDir_ReturnsSuccessWithEmptyManifest()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"fixture-noglobal-{Guid.NewGuid():N}");
        var nonExistentDir = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid():N}");
        try
        {
            var service = new FixtureGenerationService();
            var result = await service.GenerateAsync(
                globalRoot: nonExistentDir,
                projectRoot: nonExistentDir,
                activeProfiles: [],
                outputPath: outputDir);

            Assert.True(result.Success);
            var lines = await File.ReadAllLinesAsync(Path.Combine(outputDir, "fixture-manifest.txt"));
            Assert.DoesNotContain(lines, l => !string.IsNullOrEmpty(l));
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_OutputIsOnlyManifestFile()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"fixture-only-{Guid.NewGuid():N}");
        try
        {
            var service = new FixtureGenerationService();
            await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir);

            var files = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();
            Assert.Single(files);
            Assert.Equal("fixture-manifest.txt", files[0]);
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }
}
