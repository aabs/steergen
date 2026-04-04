using System.Text.Json;
using Steergen.Core.Generation;
using Steergen.Templates;

namespace Steergen.Cli.IntegrationTests;

/// <summary>
/// Regression tests for CI pipeline integration: validates exit-code contracts,
/// deterministic output guarantees, and generation manifest artifacts.
/// </summary>
public sealed class CiWorkflowRegressionTests
{
    private static readonly string FixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance"));

    // ── Exit-code contract: validate step ──────────────────────────────────

    [Fact]
    public async Task CiValidate_ValidCorpus_ExitsZero()
    {
        var result = await Commands.ValidateCommand.RunAsync(
            globalRoot: Path.Combine(FixturesRoot, "global"),
            projectRoot: Path.Combine(FixturesRoot, "project"),
            quiet: true);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CiValidate_DocumentWithErrors_ExitsOne()
    {
        var dir = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "bad.md"),
                """
                :::rule id="R001" severity="bogus" domain="core"
                Rule with invalid severity.
                :::
                """);

            var result = await Commands.ValidateCommand.RunAsync(
                globalRoot: dir,
                projectRoot: null,
                quiet: true);

            Assert.Equal(1, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task CiValidate_MissingDirectory_ExitsTwo()
    {
        var result = await Commands.ValidateCommand.RunAsync(
            globalRoot: "/nonexistent-ci-path-" + Guid.NewGuid().ToString("N"),
            projectRoot: null,
            quiet: true);

        Assert.Equal(2, result);
    }

    // ── Manifest artifact: run step ────────────────────────────────────────

    [Fact]
    public async Task CiRun_ProducesGenerationManifestJson()
    {
        var outputDir = CreateTempOutputDir();
        try
        {
            var service = new SpeckitGenerationService();
            var result = await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider(),
                writeManifest: true);

            Assert.True(result.Success);
            Assert.True(
                File.Exists(Path.Combine(outputDir, DeterministicOutputManifest.ManifestFileName)),
                "generation-manifest.json should exist after a run");
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task CiRun_ManifestIsValidJson()
    {
        var outputDir = CreateTempOutputDir();
        try
        {
            var service = new SpeckitGenerationService();
            await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider(),
                writeManifest: true);

            var manifestPath = Path.Combine(outputDir, DeterministicOutputManifest.ManifestFileName);
            var json = await File.ReadAllTextAsync(manifestPath);
            var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.TryGetProperty("success", out var success));
            Assert.True(success.GetBoolean());
            Assert.True(doc.RootElement.TryGetProperty("entries", out var entries));
            Assert.True(entries.GetArrayLength() > 0, "Manifest should have at least one file entry");
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task CiRun_ManifestEntriesHaveSha256Hashes()
    {
        var outputDir = CreateTempOutputDir();
        try
        {
            var service = new SpeckitGenerationService();
            var result = await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider(),
                writeManifest: true);

            Assert.NotNull(result.Manifest);
            Assert.All(result.Manifest.Entries, entry =>
            {
                Assert.NotEmpty(entry.Sha256);
                Assert.Equal(64, entry.Sha256.Length); // SHA-256 hex = 64 chars
                Assert.Matches("^[0-9a-f]{64}$", entry.Sha256);
            });
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    // ── Determinism: same inputs → same outputs ───────────────────────────

    [Fact]
    public async Task CiRun_SameInputTwice_ProducesIdenticalManifestHashes()
    {
        var outputDir1 = CreateTempOutputDir();
        var outputDir2 = CreateTempOutputDir();
        try
        {
            var service = new SpeckitGenerationService();
            var globalRoot = Path.Combine(FixturesRoot, "global");
            var projectRoot = Path.Combine(FixturesRoot, "project");
            var provider = new EmbeddedTemplateProvider();

            var result1 = await service.GenerateAsync(
                globalRoot, projectRoot, [], outputDir1, provider, writeManifest: true);

            var result2 = await service.GenerateAsync(
                globalRoot, projectRoot, [], outputDir2, provider, writeManifest: true);

            Assert.True(result1.Success);
            Assert.True(result2.Success);
            Assert.NotNull(result1.Manifest);
            Assert.NotNull(result2.Manifest);
            Assert.True(
                result1.Manifest.HasIdenticalContentTo(result2.Manifest),
                "Two identical runs must produce the same file hashes (determinism)");
        }
        finally
        {
            Directory.Delete(outputDir1, recursive: true);
            Directory.Delete(outputDir2, recursive: true);
        }
    }

    // ── CI-facing failure report ──────────────────────────────────────────

    [Fact]
    public async Task CiRun_FailedValidation_ManifestSuccessIsFalse()
    {
        var dir = CreateTempDir();
        var outputDir = CreateTempOutputDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "bad.md"),
                """
                :::rule id="R001" severity="critical" domain="core"
                Invalid severity.
                :::
                """);

            var pipeline = new GenerationPipeline();
            var result = await pipeline.RunAsync(
                globalDocuments: [Core.Parsing.SteeringMarkdownParser.Parse(
                    await File.ReadAllTextAsync(Path.Combine(dir, "bad.md")),
                    Path.Combine(dir, "bad.md"))],
                projectDocuments: [],
                activeProfiles: [],
                targets: [],
                targetConfigs: [],
                manifestOutputPath: outputDir);

            Assert.False(result.Success);
            Assert.NotNull(result.Manifest);
            Assert.False(result.Manifest.Success);
            Assert.NotEmpty(result.Manifest.Errors);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public void GenerationResult_FormatCiReport_IncludesErrorsAndWarnings()
    {
        var diagnostics = new[]
        {
            new Core.Validation.Diagnostic("ERR-001", "Error message", Core.Validation.DiagnosticSeverity.Error,
                new Core.Model.SourceLocation("file.md", 0)),
            new Core.Validation.Diagnostic("WARN-001", "Warning message", Core.Validation.DiagnosticSeverity.Warning),
        };

        var result = new GenerationResult(false, diagnostics, 0);
        var report = result.FormatCiReport();

        Assert.Equal(2, report.Count);
        Assert.Contains("error", report[0]);
        Assert.Contains("ERR-001", report[0]);
        Assert.Contains("warning", report[1]);
        Assert.Contains("WARN-001", report[1]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string CreateTempDir() =>
        Directory.CreateTempSubdirectory("ci-regr-").FullName;

    private static string CreateTempOutputDir() =>
        Directory.CreateTempSubdirectory("ci-output-").FullName;
}
