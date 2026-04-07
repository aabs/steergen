using Steergen.Core.Generation;
using Steergen.Core.Model;
using Steergen.Core.Parsing;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Speckit;
using Steergen.Templates;

namespace Steergen.Cli.IntegrationTests;

public sealed class RunSpeckitCommandTests
{
    private static readonly string FixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance"));

    // SpeckitGenerationService sets OutputPath = outputDir directly,
    // so layout produces files under outputDir/.speckit/memory/
    private static string SpeckitMemoryDir(string outputDir) =>
        Path.Combine(outputDir, ".speckit", "memory");

    [Fact]
    public async Task Run_WithRealisticFixtures_ProducesConstitutionFile()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"speckit-integ-{Guid.NewGuid():N}");
        try
        {
            var service = new SpeckitGenerationService();
            var result = await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider());

            Assert.True(result.Success, $"Generation failed: {string.Join("; ", result.Diagnostics.Select(d => d.Message))}");
            Assert.True(
                File.Exists(Path.Combine(SpeckitMemoryDir(outputDir), "constitution.md")),
                "constitution.md should be created under .speckit/memory/");
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithRealisticFixtures_ConstitutionContainsCoreRulesOnly()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"speckit-integ-{Guid.NewGuid():N}");
        try
        {
            var service = new SpeckitGenerationService();
            await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider());

            var constitutionContent = await File.ReadAllTextAsync(
                Path.Combine(SpeckitMemoryDir(outputDir), "constitution.md"));

            Assert.Contains("CORE-001", constitutionContent);
            Assert.Contains("CORE-002", constitutionContent);
            Assert.Contains("CORE-003", constitutionContent);
            Assert.Contains("CORE-004", constitutionContent);
            Assert.Contains("CORE-005", constitutionContent);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithRealisticFixtures_DomainModulesCreatedForNonCoreRules()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"speckit-integ-{Guid.NewGuid():N}");
        try
        {
            var projectDoc = SteeringMarkdownParser.Parse(
                await File.ReadAllTextAsync(Path.Combine(FixturesRoot, "project", "project-steering.md")),
                "project-steering.md");

            var domains = projectDoc.Rules
                .Where(r => !string.Equals(r.Domain, "core", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Domain)
                .Distinct()
                .ToList();

            var service = new SpeckitGenerationService();
            await service.GenerateAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                activeProfiles: [],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider());

            foreach (var domain in domains)
            {
                Assert.True(
                    File.Exists(Path.Combine(SpeckitMemoryDir(outputDir), $"{domain}.md")),
                    $"Expected domain module file for domain '{domain}' under .speckit/memory/");
            }
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
        var dir1 = Path.Combine(Path.GetTempPath(), $"speckit-det1-{Guid.NewGuid():N}");
        var dir2 = Path.Combine(Path.GetTempPath(), $"speckit-det2-{Guid.NewGuid():N}");
        try
        {
            var service = new SpeckitGenerationService();
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

            var files1 = Directory.EnumerateFiles(dir1, "*.md", SearchOption.AllDirectories).OrderBy(p => Path.GetFileName(p)).ToList();
            var files2 = Directory.EnumerateFiles(dir2, "*.md", SearchOption.AllDirectories).OrderBy(p => Path.GetFileName(p)).ToList();

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

    [Fact]
    public async Task Run_ProfileFiltering_ExcludesRulesNotMatchingProfile()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"speckit-profile-{Guid.NewGuid():N}");
        try
        {
            var globalDir = Path.Combine(Path.GetTempPath(), $"speckit-global-{Guid.NewGuid():N}");
            Directory.CreateDirectory(globalDir);
            await File.WriteAllTextAsync(Path.Combine(globalDir, "constitution.md"), """
                ---
                id: test-constitution
                version: "1.0.0"
                title: Test Constitution
                ---
                :::rule id="CORE-001" severity="error" domain="core"
                Always applies.
                :::
                :::rule id="CORE-002" severity="warning" domain="core" profile="strict"
                Only in strict profile.
                :::
                """);

            var service = new SpeckitGenerationService();
            await service.GenerateAsync(
                globalRoot: globalDir,
                projectRoot: globalDir + "-empty",
                activeProfiles: ["default"],
                outputPath: outputDir,
                templateProvider: new EmbeddedTemplateProvider());

            var constitution = await File.ReadAllTextAsync(
                Path.Combine(SpeckitMemoryDir(outputDir), "constitution.md"));
            Assert.Contains("CORE-001", constitution);
            Assert.DoesNotContain("CORE-002", constitution);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }
}
