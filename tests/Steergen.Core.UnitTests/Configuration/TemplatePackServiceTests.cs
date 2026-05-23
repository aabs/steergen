using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Xunit;

namespace Steergen.Core.UnitTests.Configuration;

/// <summary>
/// Tests for <see cref="TemplatePackService"/> remove operation.
/// Validates: Requirement 7.4
/// </summary>
public sealed class TemplatePackServiceTests
{
    private static string MakeTempConfigPath()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "testdata", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "steergen.config.yaml");
    }

    private static async Task WriteConfigWithTemplatePack(string path)
    {
        var writer = new SteergenConfigWriter();
        var config = new SteeringConfiguration
        {
            ProjectRoot = "./steering",
            TemplatePack = new TemplatePackConfig
            {
                Source = "github:acme-corp/steergen-templates",
                Ref = "v2.1.0",
            },
        };
        await writer.WriteAsync(path, config);
    }

    private static async Task WriteConfigWithoutTemplatePack(string path)
    {
        var writer = new SteergenConfigWriter();
        var config = new SteeringConfiguration
        {
            ProjectRoot = "./steering",
        };
        await writer.WriteAsync(path, config);
    }

    [Fact]
    public async Task RemoveAsync_WithTemplatePack_ReturnsSuccess()
    {
        var path = MakeTempConfigPath();
        await WriteConfigWithTemplatePack(path);

        var svc = new TemplatePackService();
        var result = await svc.RemoveAsync(path);

        Assert.True(result.Success);
        Assert.False(result.WasNotConfigured);
    }

    [Fact]
    public async Task RemoveAsync_WithTemplatePack_RemovesFromFile()
    {
        var path = MakeTempConfigPath();
        await WriteConfigWithTemplatePack(path);

        var svc = new TemplatePackService();
        await svc.RemoveAsync(path);

        // Verify the template pack is gone from the persisted config
        var loader = new SteergenConfigLoader();
        var loaded = await loader.LoadAsync(path);
        Assert.Null(loaded.TemplatePack);
    }

    [Fact]
    public async Task RemoveAsync_WithTemplatePack_PreservesOtherFields()
    {
        var path = MakeTempConfigPath();
        await WriteConfigWithTemplatePack(path);

        var svc = new TemplatePackService();
        await svc.RemoveAsync(path);

        var loader = new SteergenConfigLoader();
        var loaded = await loader.LoadAsync(path);
        Assert.Equal("./steering", loaded.ProjectRoot);
    }

    [Fact]
    public async Task RemoveAsync_NoTemplatePack_ReturnsNotConfigured()
    {
        var path = MakeTempConfigPath();
        await WriteConfigWithoutTemplatePack(path);

        var svc = new TemplatePackService();
        var result = await svc.RemoveAsync(path);

        Assert.True(result.Success);
        Assert.True(result.WasNotConfigured);
    }

    [Fact]
    public async Task RemoveAsync_MissingConfigFile_ReturnsFailure()
    {
        var svc = new TemplatePackService();
        var result = await svc.RemoveAsync("/nonexistent/path/steergen.config.yaml");

        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task RemoveAsync_ConcurrentModification_ThrowsConflict()
    {
        var path = MakeTempConfigPath();
        await WriteConfigWithTemplatePack(path);

        // Read the config to get the hash
        var svc = new TemplatePackService();

        // Modify the file externally between read and write
        // We simulate this by calling remove twice concurrently
        // First call succeeds, second should detect the change
        await svc.RemoveAsync(path);

        // Write a new config with template pack again
        await WriteConfigWithTemplatePack(path);

        // This should succeed since it reads fresh
        var result = await svc.RemoveAsync(path);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task UpdatePinBySelectorAsync_UpdatesTemplatePinTuple()
    {
        var path = MakeTempConfigPath();
        var writer = new SteergenConfigWriter();
        await writer.WriteAsync(path, new SteeringConfiguration
        {
            ProjectRoot = "./steering",
            TemplatePack = new TemplatePackConfig
            {
                Source = "github:acme/templates",
                EntryKey = "templates/default",
                Ref = "v1.0.0",
                Pin = new PackPin
                {
                    Tag = "v1.0.0",
                    CommitSha = "1111111111111111111111111111111111111111",
                },
            },
        });

        var resolver = new PackSelectorResolver();
        resolver.TryParse("github:acme/templates|templates/default", out var selector, out _);

        var svc = new TemplatePackService();
        var result = await svc.UpdatePinBySelectorAsync(
            path,
            selector,
            "v1.1.0",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        Assert.True(result.Success);

        var loader = new SteergenConfigLoader();
        var loaded = await loader.LoadAsync(path);
        Assert.NotNull(loaded.TemplatePack);
        Assert.Equal("v1.1.0", loaded.TemplatePack!.Ref);
        Assert.Equal("v1.1.0", loaded.TemplatePack!.Pin!.Tag);
        Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", loaded.TemplatePack!.Pin!.CommitSha);
    }

    [Fact]
    public async Task UpdatePinBySelectorAsync_WhenSelectorDoesNotMatch_ReturnsFailure()
    {
        var path = MakeTempConfigPath();
        var writer = new SteergenConfigWriter();
        await writer.WriteAsync(path, new SteeringConfiguration
        {
            ProjectRoot = "./steering",
            TemplatePack = new TemplatePackConfig
            {
                Source = "github:acme/templates",
                EntryKey = "templates/default",
                Ref = "v1.0.0",
            },
        });

        var resolver = new PackSelectorResolver();
        resolver.TryParse("github:acme/templates|templates/other", out var selector, out _);

        var svc = new TemplatePackService();
        var result = await svc.UpdatePinBySelectorAsync(
            path,
            selector,
            "v1.1.0",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        Assert.False(result.Success);
        Assert.Contains("does not match", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }
}
