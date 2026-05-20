using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Targets;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]
/// <summary>
/// Integration tests for external target packs (pack-provided targets).
/// Validates: Requirements 16.3, 16.5, 16.6, 16.8
/// </summary>
public sealed class ExternalTargetPackTests : IDisposable
{
    private static readonly string FixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance"));

    public ExternalTargetPackTests()
    {
        TargetRegistry.Clear();
        TargetRegistry.RegisterBuiltins(new StubTemplateProvider());
    }

    public void Dispose()
    {
        TargetRegistry.Clear();
    }

    private sealed class StubTemplateProvider : ITemplateProvider
    {
        public string GetTemplate(string targetId, string templateName) =>
            string.Empty;
    }

    private static string MakeTempDir() =>
        Directory.CreateTempSubdirectory("extpack-test-").FullName;

    /// <summary>
    /// Creates a template pack directory with a pack.yaml manifest declaring
    /// a provided target, a default layout YAML, and a Scriban document template.
    /// </summary>
    private static string CreateTemplatePack(
        string baseDir,
        string packName = "test-external-pack",
        string targetId = "custom-ext")
    {
        var packDir = Path.Combine(baseDir, "template-pack");
        Directory.CreateDirectory(packDir);

        // Write pack.yaml manifest with providedTargets
        var packYaml = $"""
            name: "{packName}"
            version: "1.0.0"
            minSteergenVersion: "0.1.0"
            providedTargets:
              - targetId: "{targetId}"
                defaultLayout: "{targetId}/default-layout.yaml"
                description: "Integration test external target"
            """;
        File.WriteAllText(Path.Combine(packDir, "pack.yaml"), packYaml);

        // Create target subdirectory with default layout and template
        var targetDir = Path.Combine(packDir, targetId);
        Directory.CreateDirectory(targetDir);

        WriteDefaultLayout(targetDir, targetId);
        WriteDocumentTemplate(targetDir);

        return packDir;
    }

    private static void WriteDefaultLayout(string targetDir, string targetId)
    {
        // Use a simple layout that routes all rules to a single file
        var layoutYaml = @"version: ""1.0""

roots:
  globalRoot: ""${globalRoot}""
  projectRoot: ""${projectRoot}""
  targetRoot: ""${generationRoot}/" + targetId + @"-output""

routes:
  - id: all-rules
    scope: both
    explicit: true
    anchor: core
    order: 10
    match:
      category: ""*""
    destination:
      directory: ""${targetRoot}""
      fileName: ""rules""
      extension: "".md""

fallback:
  mode: other-at-core-anchor
  fileBaseName: rules
  directory: ""${targetRoot}""

purge:
  roots:
    - ""${targetRoot}""
  globs:
    - ""**/*.md""
";
        File.WriteAllText(
            Path.Combine(targetDir, "default-layout.yaml"), layoutYaml);
    }

    private static void WriteDocumentTemplate(string targetDir)
    {
        var template = @"# External Target Output
{{- for rule in rules }}
- {{ rule.id }}: {{ rule.primary_text }}
{{- end }}
";
        File.WriteAllText(
            Path.Combine(targetDir, "document.scriban"), template);
    }

    /// <summary>
    /// Places a template pack in the user's real cache location
    /// (~/.steergen/packs/{owner}/{repo}/{ref}/) so that RunCommand
    /// can discover it via the GitHub source config path.
    /// Returns the source string, ref, and cache path for cleanup.
    /// </summary>
    private static (string Source, string Ref, string CachePath) SetupCachedPack(
        string packName = "test-external-pack",
        string targetId = "custom-ext")
    {
        const string owner = "steergen-inttest";
        const string repo = "ext-target-pack";
        const string refValue = "v1.0.0-test";

        var cacheBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".steergen");
        var cachePath = Path.Combine(
            cacheBase, "packs", owner, repo, refValue);

        // Clean up any previous test run
        if (Directory.Exists(cachePath))
            Directory.Delete(cachePath, recursive: true);

        Directory.CreateDirectory(cachePath);

        // Write pack.yaml manifest with providedTargets
        var packYaml = $"""
            name: "{packName}"
            version: "1.0.0"
            minSteergenVersion: "0.1.0"
            providedTargets:
              - targetId: "{targetId}"
                defaultLayout: "{targetId}/default-layout.yaml"
                description: "Integration test external target"
            """;
        File.WriteAllText(Path.Combine(cachePath, "pack.yaml"), packYaml);

        // Create target subdirectory with layout and template
        var targetDir = Path.Combine(cachePath, targetId);
        Directory.CreateDirectory(targetDir);
        WriteDefaultLayout(targetDir, targetId);
        WriteDocumentTemplate(targetDir);

        return ($"github:{owner}/{repo}", refValue, cachePath);
    }

    private static void CleanupCachedPack(string cachePath)
    {
        if (Directory.Exists(cachePath))
            Directory.Delete(cachePath, recursive: true);
    }

    private static async Task<string> WriteConfigWithGitHubPackAsync(
        string dir,
        string source,
        string refValue,
        IEnumerable<string>? registeredTargets = null)
    {
        var configPath = Path.Combine(dir, "steergen.config.yaml");
        var writer = new SteergenConfigWriter();
        var config = new SteeringConfiguration
        {
            ProjectRoot = Path.Combine(FixturesRoot, "project"),
            RegisteredTargets = (registeredTargets ?? []).ToList(),
            TemplatePack = new TemplatePackConfig
            {
                Source = source,
                Ref = refValue,
            },
        };
        await writer.WriteAsync(configPath, config);
        return configPath;
    }

    private static async Task<string> WriteConfigWithLocalPackAsync(
        string dir,
        string localPackPath,
        IEnumerable<string>? registeredTargets = null)
    {
        var configPath = Path.Combine(dir, "steergen.config.yaml");
        var writer = new SteergenConfigWriter();
        var config = new SteeringConfiguration
        {
            ProjectRoot = Path.Combine(FixturesRoot, "project"),
            RegisteredTargets = (registeredTargets ?? []).ToList(),
            TemplatePack = new TemplatePackConfig
            {
                LocalPath = localPackPath,
            },
        };
        await writer.WriteAsync(configPath, config);
        return configPath;
    }

    // ── Test: steergen target add with pack-provided target succeeds ─────────

    [Fact]
    public async Task TargetAdd_PackProvidedTarget_ReturnsExitCode0()
    {
        var workDir = MakeTempDir();
        try
        {
            var packDir = CreateTemplatePack(workDir);

            // Register pack targets so TargetRegistry.IsAvailable returns true
            var manifestParser = new PackManifestParser();
            var manifest = manifestParser.Parse(packDir)!;
            var templateProvider = new TemplateResolver(
                packDir, null, new StubTemplateProvider());
            TargetRegistry.RegisterPackTargets(manifest, packDir, templateProvider);

            var configPath = await WriteConfigWithLocalPackAsync(
                workDir, packDir);

            var exitCode = await TargetCommand.AddAsync(configPath, "custom-ext");

            Assert.Equal(0, exitCode);

            // Verify target was persisted to config
            var loader = new SteergenConfigLoader();
            var loaded = await loader.LoadAsync(configPath);
            Assert.Contains("custom-ext", loaded.RegisteredTargets);
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    [Fact]
    public async Task TargetAdd_PackProvidedTarget_IsAvailableReturnsTrue()
    {
        var workDir = MakeTempDir();
        try
        {
            var packDir = CreateTemplatePack(workDir);

            var manifestParser = new PackManifestParser();
            var manifest = manifestParser.Parse(packDir)!;
            var templateProvider = new TemplateResolver(
                packDir, null, new StubTemplateProvider());
            TargetRegistry.RegisterPackTargets(manifest, packDir, templateProvider);

            Assert.True(TargetRegistry.IsAvailable("custom-ext"));
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    [Fact]
    public async Task TargetAdd_UnregisteredPackTarget_ReturnsExitCode2()
    {
        var workDir = MakeTempDir();
        try
        {
            // Do NOT register pack targets — the target should not be available
            var configPath = await WriteConfigWithLocalPackAsync(
                workDir, Path.Combine(workDir, "nonexistent-pack"));

            var exitCode = await TargetCommand.AddAsync(
                configPath, "unknown-pack-target");

            Assert.Equal(2, exitCode);
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    // ── Test: steergen run with external target renders via pack templates ───

    [Fact]
    public async Task Run_WithExternalTarget_ReturnsExitCode0()
    {
        var workDir = MakeTempDir();
        var (source, refValue, cachePath) = SetupCachedPack();
        try
        {
            var outputDir = Path.Combine(workDir, "output");
            Directory.CreateDirectory(outputDir);

            var configPath = await WriteConfigWithGitHubPackAsync(
                workDir, source, refValue,
                registeredTargets: ["custom-ext"]);

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: Path.Combine(FixturesRoot, "project"),
                outputBase: outputDir,
                explicitTargets: ["custom-ext"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
            CleanupCachedPack(cachePath);
        }
    }

    [Fact]
    public async Task Run_WithExternalTarget_ProducesOutputFiles()
    {
        var workDir = MakeTempDir();
        var (source, refValue, cachePath) = SetupCachedPack();
        try
        {
            var outputDir = Path.Combine(workDir, "output");
            Directory.CreateDirectory(outputDir);

            var configPath = await WriteConfigWithGitHubPackAsync(
                workDir, source, refValue,
                registeredTargets: ["custom-ext"]);

            await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: Path.Combine(FixturesRoot, "project"),
                outputBase: outputDir,
                explicitTargets: ["custom-ext"],
                quiet: true,
                cancellationToken: default);

            var outputFiles = Directory.GetFiles(
                outputDir, "*.md", SearchOption.AllDirectories);
            Assert.True(outputFiles.Length > 0,
                "External target should produce at least one output file");
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
            CleanupCachedPack(cachePath);
        }
    }

    [Fact]
    public async Task Run_WithExternalTarget_RendersRulesViaPackTemplate()
    {
        var workDir = MakeTempDir();
        var (source, refValue, cachePath) = SetupCachedPack();
        try
        {
            var outputDir = Path.Combine(workDir, "output");
            Directory.CreateDirectory(outputDir);

            var configPath = await WriteConfigWithGitHubPackAsync(
                workDir, source, refValue,
                registeredTargets: ["custom-ext"]);

            await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: Path.Combine(FixturesRoot, "project"),
                outputBase: outputDir,
                explicitTargets: ["custom-ext"],
                quiet: true,
                cancellationToken: default);

            var outputFiles = Directory.GetFiles(
                outputDir, "*.md", SearchOption.AllDirectories);
            Assert.True(outputFiles.Length > 0,
                "Expected output files from external target");

            var content = File.ReadAllText(outputFiles[0]);
            // The template renders "# External Target Output" header
            // followed by rules as "- {id}: {primary_text}"
            Assert.Contains("# External Target Output", content);
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
            CleanupCachedPack(cachePath);
        }
    }

    // ── Test: removal of pack providing registered target emits TP010 ────────

    [Fact]
    public async Task TemplatePackRemove_WithRegisteredPackTarget_ReturnsExitCode2()
    {
        var workDir = MakeTempDir();
        try
        {
            var packDir = CreateTemplatePack(workDir);

            // Register pack targets in the registry
            var manifestParser = new PackManifestParser();
            var manifest = manifestParser.Parse(packDir)!;
            var templateProvider = new TemplateResolver(
                packDir, null, new StubTemplateProvider());
            TargetRegistry.RegisterPackTargets(manifest, packDir, templateProvider);

            // Write config with the template pack AND the target registered
            var configPath = await WriteConfigWithLocalPackAsync(
                workDir, packDir, registeredTargets: ["custom-ext"]);

            // Attempt to remove the template pack — should fail with TP010
            var exitCode = await TemplatePackRemoveCommand.RunAsync(configPath);

            Assert.Equal(2, exitCode);
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    [Fact]
    public async Task TemplatePackRemove_WithRegisteredPackTarget_EmitsOrphanedTargetError()
    {
        var workDir = MakeTempDir();
        try
        {
            var packDir = CreateTemplatePack(workDir);

            // Register pack targets in the registry
            var manifestParser = new PackManifestParser();
            var manifest = manifestParser.Parse(packDir)!;
            var templateProvider = new TemplateResolver(
                packDir, null, new StubTemplateProvider());
            TargetRegistry.RegisterPackTargets(manifest, packDir, templateProvider);

            // Write config with the template pack AND the target registered
            var configPath = await WriteConfigWithLocalPackAsync(
                workDir, packDir, registeredTargets: ["custom-ext"]);

            // Capture stderr output
            var originalErr = Console.Error;
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            try
            {
                await TemplatePackRemoveCommand.RunAsync(configPath);
            }
            finally
            {
                Console.SetError(originalErr);
            }

            var errOutput = errWriter.ToString();
            // TP010 diagnostic message mentions the target and pack name
            Assert.Contains("custom-ext", errOutput);
            Assert.Contains("test-external-pack", errOutput);
            Assert.Contains("steergen target remove", errOutput);
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    [Fact]
    public async Task TemplatePackRemove_AfterTargetRemoved_Succeeds()
    {
        var workDir = MakeTempDir();
        try
        {
            var packDir = CreateTemplatePack(workDir);

            // Register pack targets in the registry
            var manifestParser = new PackManifestParser();
            var manifest = manifestParser.Parse(packDir)!;
            var templateProvider = new TemplateResolver(
                packDir, null, new StubTemplateProvider());
            TargetRegistry.RegisterPackTargets(manifest, packDir, templateProvider);

            // Write config with the template pack AND the target registered
            var configPath = await WriteConfigWithLocalPackAsync(
                workDir, packDir, registeredTargets: ["custom-ext"]);

            // First remove the target from config
            await TargetCommand.RemoveAsync(configPath, "custom-ext");

            // Now remove the template pack — should succeed since target
            // is no longer in registeredTargets
            var exitCode = await TemplatePackRemoveCommand.RunAsync(configPath);

            Assert.Equal(0, exitCode);
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }
}
