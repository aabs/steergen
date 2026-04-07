using Steergen.Core.Generation;
using Steergen.Core.Model;
using Steergen.Core.Parsing;
using Steergen.Core.Targets.Kiro;
using Steergen.Templates;

namespace Steergen.Cli.IntegrationTests;

public sealed class RunKiroCommandTests
{
    private static readonly string FixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance"));

    [Fact]
    public async Task Run_WithRealisticFixtures_ProducesOutputFiles()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"kiro-integ-{Guid.NewGuid():N}");
        try
        {
            var service = new KiroGenerationService();
            var result = await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider());

            Assert.True(result.Success,
                $"Generation failed: {string.Join("; ", result.Diagnostics.Select(d => d.Message))}");
            Assert.True(Directory.Exists(outputDir), "Output directory should be created");
            var files = Directory.GetFiles(outputDir, "*.md", SearchOption.AllDirectories);
            Assert.NotEmpty(files);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_PerDocumentOutput_OneFilePerSourceDocument()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"kiro-perdoc-{Guid.NewGuid():N}");
        try
        {
            var globalDir = Path.Combine(FixturesRoot, "global");
            var projectDir = Path.Combine(FixturesRoot, "project");

            var expectedDocCount = Directory.GetFiles(globalDir, "*.md", SearchOption.TopDirectoryOnly).Length
                + Directory.GetFiles(projectDir, "*.md", SearchOption.TopDirectoryOnly).Length;

            var service = new KiroGenerationService();
            await service.GenerateAsync(
                globalRoot: globalDir,
                projectRoot: projectDir,
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider());

            var outputFiles = Directory.GetFiles(outputDir, "*.md", SearchOption.AllDirectories);
            Assert.Equal(expectedDocCount, outputFiles.Length);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_OutputFiles_HaveAlwaysInclusionByDefault()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"kiro-inclusion-{Guid.NewGuid():N}");
        try
        {
            var service = new KiroGenerationService();
            await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider());

            foreach (var file in Directory.GetFiles(outputDir, "*.md", SearchOption.AllDirectories))
            {
                var content = await File.ReadAllTextAsync(file);
                Assert.Contains("inclusion: always", content);
            }
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithFileMatchOption_UsesFileMatchInclusion()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"kiro-filematch-{Guid.NewGuid():N}");
        try
        {
            var service = new KiroGenerationService();
            await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider(),
                formatOptions: new Dictionary<string, string>
                {
                    ["inclusionMode"] = "fileMatch",
                    ["fileMatchPattern"] = "**/*.ts",
                });

            foreach (var file in Directory.GetFiles(outputDir, "*.md", SearchOption.AllDirectories))
            {
                var content = await File.ReadAllTextAsync(file);
                Assert.Contains("inclusion: fileMatch", content);
                Assert.Contains("fileMatchPattern: **/*.ts", content);
            }
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_OutputFiles_ExcludeDeprecatedRules()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"kiro-depr-{Guid.NewGuid():N}");
        try
        {
            var tempGlobal = Path.Combine(Path.GetTempPath(), $"kiro-global-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempGlobal);
            await File.WriteAllTextAsync(Path.Combine(tempGlobal, "constitution.md"), """
                ---
                id: test-constitution
                version: "1.0.0"
                title: Test Constitution
                ---
                :::rule id="CORE-001" severity="error" domain="core"
                Active rule text.
                :::
                :::rule id="CORE-002" severity="warning" domain="core" deprecated="true"
                Deprecated rule text.
                :::
                """);

            var service = new KiroGenerationService();
            await service.GenerateAsync(
                globalRoot: tempGlobal,
                projectRoot: tempGlobal + "-empty",
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider());

            var files = Directory.GetFiles(outputDir, "*.md", SearchOption.AllDirectories);
            Assert.Single(files);
            var content = await File.ReadAllTextAsync(files[0]);
            Assert.Contains("Active rule text", content);
            Assert.DoesNotContain("Deprecated rule text", content);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_OutputIsDeterministic_SameInputProducesSameOutput()
    {
        var dir1 = Path.Combine(Path.GetTempPath(), $"kiro-det1-{Guid.NewGuid():N}");
        var dir2 = Path.Combine(Path.GetTempPath(), $"kiro-det2-{Guid.NewGuid():N}");
        try
        {
            var service = new KiroGenerationService();
            var provider = new EmbeddedTemplateProvider();

            await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: dir1,
                templateProvider: provider);

            await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: dir2,
                templateProvider: provider);

            var files1 = Directory.EnumerateFiles(dir1, "*.md", SearchOption.AllDirectories).OrderBy(Path.GetFileName).ToList();
            var files2 = Directory.EnumerateFiles(dir2, "*.md", SearchOption.AllDirectories).OrderBy(Path.GetFileName).ToList();

            Assert.Equal(files1.Count, files2.Count);
            for (var i = 0; i < files1.Count; i++)
            {
                var content1 = await File.ReadAllTextAsync(files1[i]);
                var content2 = await File.ReadAllTextAsync(files2[i]);
                Assert.Equal(content1, content2);
            }
        }
        finally
        {
            foreach (var dir in new[] { dir1, dir2 })
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
        }
    }
}
