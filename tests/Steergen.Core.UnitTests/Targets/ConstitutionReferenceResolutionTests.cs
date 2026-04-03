using Steergen.Core.Model;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Speckit;
using Steergen.Core.Targets.Kiro;

namespace Steergen.Core.UnitTests.Targets;

/// <summary>
/// Tests for FR-042: cross-target constitution modular reference path resolution.
/// Ensures that when targets write constitution.md and modular guidance files to
/// the same output directory, the emitted file paths are valid, consistently named,
/// and resolve correctly within the output tree (i.e., no absolute paths, no parent
/// traversals, and sibling modules are accessible from the output root).
/// </summary>
public sealed class ConstitutionReferenceResolutionTests
{
    private static readonly ITemplateProvider FakeTemplates = new InlineTemplateProvider(
        constitutionTemplate: "# Constitution\n{{- for rule in rules }}\n## {{ rule.id }}\n{{ rule.primary_text }}\n{{- end }}",
        moduleTemplate: "# Module: {{ domain }}\n{{- for rule in rules }}\n## {{ rule.id }}\n{{ rule.primary_text }}\n{{- end }}");

    // ── File naming invariants ────────────────────────────────────────────

    [Fact]
    public async Task Speckit_CoreRulesOnly_WritesConstitutionMdAtOutputRoot()
    {
        var outputDir = Directory.CreateTempSubdirectory("ref-test-").FullName;
        try
        {
            var target = new SpeckitTargetComponent(FakeTemplates);
            var model = BuildModel(
                coreRules: [("CORE-001", "core", "Must have tests.")],
                domainRules: []);

            await target.GenerateAsync(model, new TargetConfiguration { Id = "speckit", OutputPath = outputDir, Enabled = true }, default);

            var constitutionPath = Path.Combine(outputDir, "constitution.md");
            Assert.True(File.Exists(constitutionPath), $"Expected constitution.md at {constitutionPath}");
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Speckit_WithDomainRules_WritesDomainModuleFiles()
    {
        var outputDir = Directory.CreateTempSubdirectory("ref-test-").FullName;
        try
        {
            var target = new SpeckitTargetComponent(FakeTemplates);
            var model = BuildModel(
                coreRules: [("CORE-001", "core", "Core rule.")],
                domainRules: [("API-001", "api", "API rule."), ("SEC-001", "security", "Security rule.")]);

            await target.GenerateAsync(model, new TargetConfiguration { Id = "speckit", OutputPath = outputDir, Enabled = true }, default);

            Assert.True(File.Exists(Path.Combine(outputDir, "api.md")), "Expected api.md domain module");
            Assert.True(File.Exists(Path.Combine(outputDir, "security.md")), "Expected security.md domain module");
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Speckit_DomainModuleFilenames_AreLowercaseDomainNamePlusMd()
    {
        var outputDir = Directory.CreateTempSubdirectory("ref-test-").FullName;
        try
        {
            var target = new SpeckitTargetComponent(FakeTemplates);
            var model = BuildModel(
                coreRules: [],
                domainRules: [("DP-001", "data-platform", "Data rule.")]);

            await target.GenerateAsync(model, new TargetConfiguration { Id = "speckit", OutputPath = outputDir, Enabled = true }, default);

            // Domain name matches the domain attribute of the rule verbatim
            Assert.True(File.Exists(Path.Combine(outputDir, "data-platform.md")));
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    // ── Path safety: no traversal or absolute paths ───────────────────────

    [Fact]
    public async Task Speckit_OutputFiles_AreAllWithinOutputDirectory()
    {
        var outputDir = Directory.CreateTempSubdirectory("ref-test-").FullName;
        try
        {
            var target = new SpeckitTargetComponent(FakeTemplates);
            var model = BuildModel(
                coreRules: [("CORE-001", "core", "Core rule.")],
                domainRules: [("API-001", "api", "API rule.")]);

            await target.GenerateAsync(model, new TargetConfiguration { Id = "speckit", OutputPath = outputDir, Enabled = true }, default);

            var files = Directory.GetFiles(outputDir, "*.md", SearchOption.AllDirectories);
            Assert.All(files, file =>
            {
                var fullFile = Path.GetFullPath(file);
                var fullDir = Path.GetFullPath(outputDir);
                Assert.True(
                    fullFile.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase),
                    $"File '{fullFile}' is outside output directory '{fullDir}'");
            });
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Speckit_OutputFiles_HaveNoParentTraversalSegments()
    {
        var outputDir = Directory.CreateTempSubdirectory("ref-test-").FullName;
        try
        {
            var target = new SpeckitTargetComponent(FakeTemplates);
            var model = BuildModel(
                coreRules: [("CORE-001", "core", "Core rule.")],
                domainRules: [("API-001", "api", "API rule.")]);

            await target.GenerateAsync(model, new TargetConfiguration { Id = "speckit", OutputPath = outputDir, Enabled = true }, default);

            var files = Directory.GetFiles(outputDir, "*.md", SearchOption.AllDirectories);
            Assert.All(files, file =>
            {
                var relativePath = Path.GetRelativePath(outputDir, file);
                Assert.DoesNotContain("..", relativePath, StringComparison.Ordinal);
            });
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    // ── Cross-target sibling isolation ────────────────────────────────────

    [Fact]
    public async Task Speckit_AndKiro_InSeparateOutputDirs_DoNotOverlapFiles()
    {
        var baseDir = Directory.CreateTempSubdirectory("cross-target-").FullName;
        var speckitDir = Path.Combine(baseDir, "speckit");
        var kiroDir = Path.Combine(baseDir, "kiro");
        Directory.CreateDirectory(speckitDir);
        Directory.CreateDirectory(kiroDir);
        try
        {
            var speckitTarget = new SpeckitTargetComponent(FakeTemplates);
            var model = BuildModel(
                coreRules: [("CORE-001", "core", "Core rule.")],
                domainRules: [("API-001", "api", "API rule.")]);

            await speckitTarget.GenerateAsync(
                model,
                new TargetConfiguration { Id = "speckit", OutputPath = speckitDir, Enabled = true },
                default);

            var speckitFiles = Directory.GetFiles(speckitDir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(speckitDir, f))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var kiroFiles = Directory.GetFiles(kiroDir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(kiroDir, f))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Kiro output dir should have no files from Speckit generation (separate output dirs)
            Assert.Empty(kiroFiles);
            Assert.NotEmpty(speckitFiles);
        }
        finally { Directory.Delete(baseDir, recursive: true); }
    }

    [Fact]
    public async Task Speckit_MultipleRuns_SameInputs_ProduceIdenticalFilenames()
    {
        var outputDir1 = Directory.CreateTempSubdirectory("ref-run1-").FullName;
        var outputDir2 = Directory.CreateTempSubdirectory("ref-run2-").FullName;
        try
        {
            var model = BuildModel(
                coreRules: [("CORE-001", "core", "Core rule.")],
                domainRules: [("API-001", "api", "API rule."), ("SEC-001", "security", "Security rule.")]);

            var target = new SpeckitTargetComponent(FakeTemplates);
            await target.GenerateAsync(model, new TargetConfiguration { Id = "speckit", OutputPath = outputDir1, Enabled = true }, default);
            await target.GenerateAsync(model, new TargetConfiguration { Id = "speckit", OutputPath = outputDir2, Enabled = true }, default);

            var files1 = Directory.GetFiles(outputDir1, "*.md")
                .Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            var files2 = Directory.GetFiles(outputDir2, "*.md")
                .Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(files1, files2);
        }
        finally
        {
            Directory.Delete(outputDir1, recursive: true);
            Directory.Delete(outputDir2, recursive: true);
        }
    }

    // ── Reference content: module files referenced from constitution ──────

    [Fact]
    public async Task Speckit_ConstitutionContent_DoesNotContainDomainRuleIds()
    {
        var outputDir = Directory.CreateTempSubdirectory("ref-test-").FullName;
        try
        {
            var target = new SpeckitTargetComponent(FakeTemplates);
            var model = BuildModel(
                coreRules: [("CORE-001", "core", "Core rule text.")],
                domainRules: [("API-001", "api", "API domain text.")]);

            await target.GenerateAsync(model, new TargetConfiguration { Id = "speckit", OutputPath = outputDir, Enabled = true }, default);

            var constitution = await File.ReadAllTextAsync(Path.Combine(outputDir, "constitution.md"));

            // Domain rule IDs must not leak into the core constitution
            Assert.DoesNotContain("API-001", constitution, StringComparison.Ordinal);
            Assert.Contains("CORE-001", constitution, StringComparison.Ordinal);
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Speckit_DomainModuleContent_DoesNotContainCoreRuleIds()
    {
        var outputDir = Directory.CreateTempSubdirectory("ref-test-").FullName;
        try
        {
            var target = new SpeckitTargetComponent(FakeTemplates);
            var model = BuildModel(
                coreRules: [("CORE-001", "core", "Core rule text.")],
                domainRules: [("API-001", "api", "API domain text.")]);

            await target.GenerateAsync(model, new TargetConfiguration { Id = "speckit", OutputPath = outputDir, Enabled = true }, default);

            var apiModule = await File.ReadAllTextAsync(Path.Combine(outputDir, "api.md"));

            Assert.DoesNotContain("CORE-001", apiModule, StringComparison.Ordinal);
            Assert.Contains("API-001", apiModule, StringComparison.Ordinal);
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    // ── Helper: build a minimal ResolvedSteeringModel ────────────────────

    private static ResolvedSteeringModel BuildModel(
        IEnumerable<(string id, string domain, string text)> coreRules,
        IEnumerable<(string id, string domain, string text)> domainRules)
    {
        var rules = coreRules.Concat(domainRules)
            .Select(r => new SteeringRule
            {
                Id = r.id,
                Domain = r.domain,
                Severity = "info",
                PrimaryText = r.text,
            })
            .ToList();

        return new ResolvedSteeringModel { Rules = rules };
    }

    private sealed class InlineTemplateProvider(string constitutionTemplate, string moduleTemplate) : ITemplateProvider
    {
        public string GetTemplate(string targetId, string templateName) =>
            templateName switch
            {
                "constitution" => constitutionTemplate,
                "module" => moduleTemplate,
                _ => throw new InvalidOperationException($"Unknown template '{templateName}'."),
            };
    }
}
