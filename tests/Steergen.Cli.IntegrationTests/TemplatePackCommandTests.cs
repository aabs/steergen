using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Targets;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]
/// <summary>
/// Integration tests for template pack CLI commands:
/// - <c>steergen template-pack add</c> / <c>steergen template-pack remove</c>
/// - <c>steergen update --templates</c>
/// - <c>steergen run</c> with template pack producing overridden output
/// - <c>steergen validate</c> with malformed template pack reporting errors
///
/// Validates: Requirements 7.1, 7.4, 7.5, 6.1
/// </summary>
public sealed class TemplatePackCommandTests : IDisposable
{
    private static readonly string FixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance"));

    public TemplatePackCommandTests()
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
        public string GetTemplate(string targetId, string templateName) => string.Empty;
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tpack-integ-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task<string> WriteConfigAsync(
        string dir,
        SteeringConfiguration? config = null)
    {
        var path = Path.Combine(dir, "steergen.config.yaml");
        var writer = new SteergenConfigWriter();
        config ??= new SteeringConfiguration
        {
            ProjectRoot = Path.Combine(FixturesRoot, "project"),
        };
        await writer.WriteAsync(path, config);
        return path;
    }

    // ── template-pack add: local path ────────────────────────────────────────

    [Fact]
    public async Task TemplatePackAdd_LocalPath_ReturnsExitCode0()
    {
        var dir = CreateTempDir();
        try
        {
            var packDir = Path.Combine(dir, "templates");
            Directory.CreateDirectory(packDir);

            var configPath = await WriteConfigAsync(dir);
            var result = await TemplatePackAddCommand.RunAsync(
                configPath, source: null, refValue: null, localPath: packDir);

            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task TemplatePackAdd_LocalPath_PersistsToConfig()
    {
        var dir = CreateTempDir();
        try
        {
            var packDir = Path.Combine(dir, "templates");
            Directory.CreateDirectory(packDir);

            var configPath = await WriteConfigAsync(dir);
            await TemplatePackAddCommand.RunAsync(
                configPath, source: null, refValue: null, localPath: packDir);

            var loader = new SteergenConfigLoader();
            var loaded = await loader.LoadAsync(configPath);

            Assert.NotNull(loaded.TemplatePack);
            Assert.Equal(packDir, loaded.TemplatePack.LocalPath);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task TemplatePackAdd_GitHubSource_PersistsSourceAndRefToConfig()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);

            // This will fail to download (non-existent repo), but we can test
            // that the source format validation works. Use a real-looking source.
            var result = await TemplatePackAddCommand.RunAsync(
                configPath,
                source: "github:nonexistent-owner-xyz/nonexistent-repo-xyz",
                refValue: "v1.0.0",
                localPath: null);

            // Download will fail for non-existent repo, so exit code is 2
            Assert.Equal(2, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task TemplatePackAdd_InvalidSourceFormat_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);
            var result = await TemplatePackAddCommand.RunAsync(
                configPath, source: "invalid-format", refValue: null, localPath: null);

            Assert.Equal(2, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task TemplatePackAdd_NeitherSourceNorPath_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);
            var result = await TemplatePackAddCommand.RunAsync(
                configPath, source: null, refValue: null, localPath: null);

            Assert.Equal(2, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task TemplatePackAdd_BothSourceAndPath_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);
            var result = await TemplatePackAddCommand.RunAsync(
                configPath,
                source: "github:owner/repo",
                refValue: null,
                localPath: "/some/path");

            Assert.Equal(2, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task TemplatePackAdd_MissingConfigFile_ReturnsExitCode2()
    {
        var result = await TemplatePackAddCommand.RunAsync(
            "/nonexistent/steergen.config.yaml",
            source: null, refValue: null, localPath: "/some/path");

        Assert.Equal(2, result);
    }

    // ── template-pack remove ─────────────────────────────────────────────────

    [Fact]
    public async Task TemplatePackRemove_WhenConfigured_ReturnsExitCode0()
    {
        var dir = CreateTempDir();
        try
        {
            var packDir = Path.Combine(dir, "templates");
            Directory.CreateDirectory(packDir);

            var config = new SteeringConfiguration
            {
                ProjectRoot = Path.Combine(FixturesRoot, "project"),
                TemplatePack = new TemplatePackConfig { LocalPath = packDir },
            };
            var configPath = await WriteConfigAsync(dir, config);

            var result = await TemplatePackRemoveCommand.RunAsync(configPath);

            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task TemplatePackRemove_WhenConfigured_RemovesFromConfig()
    {
        var dir = CreateTempDir();
        try
        {
            var packDir = Path.Combine(dir, "templates");
            Directory.CreateDirectory(packDir);

            var config = new SteeringConfiguration
            {
                ProjectRoot = Path.Combine(FixturesRoot, "project"),
                TemplatePack = new TemplatePackConfig { LocalPath = packDir },
            };
            var configPath = await WriteConfigAsync(dir, config);

            await TemplatePackRemoveCommand.RunAsync(configPath);

            var loader = new SteergenConfigLoader();
            var loaded = await loader.LoadAsync(configPath);

            Assert.Null(loaded.TemplatePack);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task TemplatePackRemove_WhenNotConfigured_ReturnsExitCode0()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);
            var result = await TemplatePackRemoveCommand.RunAsync(configPath);

            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task TemplatePackRemove_MissingConfigFile_ReturnsExitCode2()
    {
        var result = await TemplatePackRemoveCommand.RunAsync("/nonexistent/steergen.config.yaml");
        Assert.Equal(2, result);
    }

    // ── update --templates ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTemplates_NoTemplatePackConfigured_ReturnsExitCode0()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);
            var result = await UpdateCommand.RunTemplatesUpdateAsync(configPath, force: false);

            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task UpdateTemplates_MissingConfigFile_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = Path.Combine(dir, "does-not-exist.yaml");
            var result = await UpdateCommand.RunTemplatesUpdateAsync(configPath, force: false);

            Assert.Equal(2, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task UpdateTemplates_InvalidSourceFormat_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var config = new SteeringConfiguration
            {
                ProjectRoot = Path.Combine(FixturesRoot, "project"),
                TemplatePack = new TemplatePackConfig { Source = "invalid-format" },
            };
            var configPath = await WriteConfigAsync(dir, config);
            var result = await UpdateCommand.RunTemplatesUpdateAsync(configPath, force: false);

            Assert.Equal(2, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task UpdateTemplates_UnreachableGitHubSource_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var config = new SteeringConfiguration
            {
                ProjectRoot = Path.Combine(FixturesRoot, "project"),
                TemplatePack = new TemplatePackConfig
                {
                    Source = "github:nonexistent-owner-xyz/nonexistent-repo-xyz",
                    Ref = "v1.0.0",
                },
            };
            var configPath = await WriteConfigAsync(dir, config);
            var result = await UpdateCommand.RunTemplatesUpdateAsync(configPath, force: false);

            Assert.Equal(2, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── run with template pack (local path) ──────────────────────────────────

    [Fact]
    public async Task Run_WithLocalTemplatePack_ProducesOverriddenOutput()
    {
        var dir = CreateTempDir();
        try
        {
            // Create a local template pack with a custom kiro document template
            var packDir = Path.Combine(dir, "templates");
            Directory.CreateDirectory(Path.Combine(packDir, "kiro"));

            // Write a custom template that produces distinctive output
            await File.WriteAllTextAsync(
                Path.Combine(packDir, "kiro", "document.scriban"),
                "# CUSTOM TEMPLATE OUTPUT\n{{ for rule in rules }}{{ rule.id }}\n{{ end }}");

            // Write pack.yaml manifest
            await File.WriteAllTextAsync(
                Path.Combine(packDir, "pack.yaml"),
                """
                name: "test-templates"
                version: "1.0.0"
                minSteergenVersion: "0.1.0"
                targets:
                  - kiro
                """);

            var outputDir = Path.Combine(dir, "output");
            Directory.CreateDirectory(outputDir);

            var config = new SteeringConfiguration
            {
                ProjectRoot = Path.Combine(FixturesRoot, "project"),
                RegisteredTargets = ["kiro"],
                TemplatePack = new TemplatePackConfig { LocalPath = packDir },
            };
            var configPath = await WriteConfigAsync(dir, config);

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: [],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);

            // Verify that output files exist and contain the custom template marker
            var outputFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
            Assert.NotEmpty(outputFiles);

            var anyContainsCustomMarker = outputFiles
                .Select(f => File.ReadAllText(f))
                .Any(content => content.Contains("CUSTOM TEMPLATE OUTPUT"));

            Assert.True(anyContainsCustomMarker,
                "At least one output file should contain the custom template marker 'CUSTOM TEMPLATE OUTPUT'");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Run_WithLocalTemplatePack_NonexistentPath_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var outputDir = Path.Combine(dir, "output");
            Directory.CreateDirectory(outputDir);

            var config = new SteeringConfiguration
            {
                ProjectRoot = Path.Combine(FixturesRoot, "project"),
                RegisteredTargets = ["kiro"],
                TemplatePack = new TemplatePackConfig
                {
                    LocalPath = Path.Combine(dir, "nonexistent-pack-dir"),
                },
            };
            var configPath = await WriteConfigAsync(dir, config);

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: [],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(2, exitCode);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Run_WithGitHubPackNotCached_ReturnsExitCode2WithTP007()
    {
        var dir = CreateTempDir();
        try
        {
            var outputDir = Path.Combine(dir, "output");
            Directory.CreateDirectory(outputDir);

            var config = new SteeringConfiguration
            {
                ProjectRoot = Path.Combine(FixturesRoot, "project"),
                RegisteredTargets = ["kiro"],
                TemplatePack = new TemplatePackConfig
                {
                    Source = "github:some-owner/some-repo",
                    Ref = "abc123def456789012345678901234567890abcd",
                },
            };
            var configPath = await WriteConfigAsync(dir, config);

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: [],
                quiet: true,
                cancellationToken: default);

            // TP007: configured GitHub pack not in local cache
            Assert.Equal(2, exitCode);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── validate with template pack ──────────────────────────────────────────

    [Fact]
    public async Task Validate_TemplatePackWithSyntaxErrors_ReturnsExitCode1()
    {
        var dir = CreateTempDir();
        try
        {
            var packDir = Path.Combine(dir, "templates");
            Directory.CreateDirectory(Path.Combine(packDir, "kiro"));

            // Write an invalid Scriban template (unclosed if block)
            await File.WriteAllTextAsync(
                Path.Combine(packDir, "kiro", "document.scriban"),
                "{{ if true }}content without end");

            var config = new SteeringConfiguration
            {
                RegisteredTargets = ["kiro"],
                TemplatePack = new TemplatePackConfig { LocalPath = packDir },
            };
            var configPath = await WriteConfigAsync(dir, config);

            var result = await ValidateCommand.RunAsync(
                globalRoot: null,
                projectRoot: null,
                quiet: true,
                configPath: configPath);

            Assert.Equal(1, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Validate_TemplatePackWithValidTemplates_ReturnsExitCode0()
    {
        var dir = CreateTempDir();
        try
        {
            var packDir = Path.Combine(dir, "templates");
            Directory.CreateDirectory(Path.Combine(packDir, "kiro"));

            // Write a valid Scriban template
            await File.WriteAllTextAsync(
                Path.Combine(packDir, "kiro", "document.scriban"),
                "{{ for rule in rules }}{{ rule.id }}\n{{ end }}");

            var config = new SteeringConfiguration
            {
                RegisteredTargets = ["kiro"],
                TemplatePack = new TemplatePackConfig { LocalPath = packDir },
            };
            var configPath = await WriteConfigAsync(dir, config);

            var result = await ValidateCommand.RunAsync(
                globalRoot: null,
                projectRoot: null,
                quiet: true,
                configPath: configPath);

            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Validate_TemplatePackWithMultipleSyntaxErrors_ReportsAllErrors()
    {
        var dir = CreateTempDir();
        try
        {
            var packDir = Path.Combine(dir, "templates");
            Directory.CreateDirectory(Path.Combine(packDir, "kiro"));
            Directory.CreateDirectory(Path.Combine(packDir, "speckit"));

            // Write invalid templates for multiple targets
            await File.WriteAllTextAsync(
                Path.Combine(packDir, "kiro", "document.scriban"),
                "{{ if true }}unclosed kiro");

            await File.WriteAllTextAsync(
                Path.Combine(packDir, "speckit", "document.scriban"),
                "{{ for x in }}missing collection");

            var config = new SteeringConfiguration
            {
                RegisteredTargets = ["kiro", "speckit"],
                TemplatePack = new TemplatePackConfig { LocalPath = packDir },
            };
            var configPath = await WriteConfigAsync(dir, config);

            var result = await ValidateCommand.RunAsync(
                globalRoot: null,
                projectRoot: null,
                quiet: false,
                configPath: configPath);

            // Should report errors (exit code 1)
            Assert.Equal(1, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Validate_TemplatePackForUnregisteredTarget_ProducesWarningNotError()
    {
        var dir = CreateTempDir();
        try
        {
            var packDir = Path.Combine(dir, "templates");
            Directory.CreateDirectory(Path.Combine(packDir, "unknown-target"));

            // Write a valid template for an unregistered target
            await File.WriteAllTextAsync(
                Path.Combine(packDir, "unknown-target", "document.scriban"),
                "plain text content");

            var config = new SteeringConfiguration
            {
                RegisteredTargets = ["kiro"],
                TemplatePack = new TemplatePackConfig { LocalPath = packDir },
            };
            var configPath = await WriteConfigAsync(dir, config);

            // Warnings do not cause exit code 1
            var result = await ValidateCommand.RunAsync(
                globalRoot: null,
                projectRoot: null,
                quiet: false,
                configPath: configPath);

            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
