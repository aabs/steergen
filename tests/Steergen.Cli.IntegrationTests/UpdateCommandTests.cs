using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Xunit;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]

/// <summary>
/// Integration tests for the <c>update</c> command covering latest-compatible
/// resolution, exact-version pinning, preview-version flows, missing config, and
/// invalid version format diagnostics.
/// </summary>
public sealed class UpdateCommandTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "steergen-test-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task<string> WriteConfigAsync(string dir, string? templatePackVersion = null)
    {
        var path = Path.Combine(dir, "steergen.config.yaml");
        var config = new SteeringConfiguration
        {
            GlobalRoot = Path.Combine(dir, "steering", "global"),
            ProjectRoot = Path.Combine(dir, "steering", "project"),
            TemplatePackVersion = templatePackVersion,
        };
        var writer = new SteergenConfigWriter();
        await writer.WriteAsync(path, config);
        return path;
    }

    private static async Task<string?> ReadTemplatePackVersionAsync(string configPath)
    {
        var loader = new SteergenConfigLoader();
        var config = await loader.LoadAsync(configPath);
        return config.TemplatePackVersion;
    }

    // ── Latest-compatible flow ────────────────────────────────────────────────

    [Fact]
    public async Task Update_NoVersionFlag_ReturnsExitCode0()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            var result = await UpdateCommand.RunAsync(configPath, version: null, preview: false);
            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Update_NoVersionFlag_UpdatesConfigToLatestStable()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            await UpdateCommand.RunAsync(configPath, version: null, preview: false);
            var version = await ReadTemplatePackVersionAsync(configPath);
            // The built-in catalog's latest stable must be a valid stable SemVer
            Assert.NotNull(version);
            Assert.True(Steergen.Core.Updates.TemplateVersionResolver.IsValidVersion(version));
            Assert.False(Steergen.Core.Updates.TemplateVersionResolver.IsPreviewVersion(version));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Exact-version flow ────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ExactVersion_ReturnsExitCode0()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            var result = await UpdateCommand.RunAsync(configPath, version: "1.0.0", preview: false);
            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Update_ExactVersion_PersistsRequestedVersion()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "0.9.0");
            await UpdateCommand.RunAsync(configPath, version: "1.0.0", preview: false);
            var version = await ReadTemplatePackVersionAsync(configPath);
            Assert.Equal("1.0.0", version);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Update_ExactVersionNotInCatalog_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            var result = await UpdateCommand.RunAsync(configPath, version: "99.99.99", preview: false);
            Assert.Equal(2, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Preview-version flow ─────────────────────────────────────────────────

    [Fact]
    public async Task Update_PreviewFlag_ReturnsExitCode0()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            var result = await UpdateCommand.RunAsync(configPath, version: null, preview: true);
            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Update_PreviewFlag_UpdatesConfigToPreviewOrStableVersion()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            await UpdateCommand.RunAsync(configPath, version: null, preview: true);
            var version = await ReadTemplatePackVersionAsync(configPath);
            Assert.NotNull(version);
            Assert.True(Steergen.Core.Updates.TemplateVersionResolver.IsValidVersion(version));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Update_ExactPreviewVersion_PersistsPreviewVersion()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            await UpdateCommand.RunAsync(configPath, version: "1.1.0-preview1", preview: false);
            var version = await ReadTemplatePackVersionAsync(configPath);
            Assert.Equal("1.1.0-preview1", version);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Error flows ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_InvalidVersionFormat_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            var result = await UpdateCommand.RunAsync(configPath, version: "not-a-version", preview: false);
            Assert.Equal(2, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Update_MissingConfigFile_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = Path.Combine(dir, "does-not-exist.yaml");
            var result = await UpdateCommand.RunAsync(configPath, version: null, preview: false);
            Assert.Equal(2, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Update_CommandAutoDiscoversConfigFromCurrentDirectory()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");

            using var scope = new CurrentDirectoryScope(dir);
            var result = await UpdateCommand.Create().Parse("--version 1.2.0").InvokeAsync();

            Assert.Equal(0, result);
            Assert.Equal("1.2.0", await ReadTemplatePackVersionAsync(configPath));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
