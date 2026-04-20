using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Xunit;

namespace Steergen.Core.UnitTests.Configuration;

public sealed class OptimisticConfigWriterTests
{
    private static string GetTestFilePath()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "testdata", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "steergen.config.yaml");
    }

    private static SteeringConfiguration MakeConfig(string? projectRoot = null) =>
        new()
        {
            GlobalRoot = "/global",
            ProjectRoot = projectRoot ?? "/project",
            GenerationRoot = "/generation",
            ActiveProfiles = ["default"],
            TemplatePackVersion = "1.0.0",
        };

    [Fact]
    public async Task WriteAsync_WhenFileUnchanged_Succeeds()
    {
        var path = GetTestFilePath();
        var writer = new SteergenConfigWriter();
        var config = MakeConfig();

        await writer.WriteAsync(path, config);

        var ex = await Record.ExceptionAsync(() => writer.WriteAsync(path, config));
        Assert.Null(ex);
    }

    [Fact]
    public async Task WriteAsync_ToNonExistentFile_CreatesFile()
    {
        var path = GetTestFilePath();
        Assert.False(File.Exists(path));

        var writer = new SteergenConfigWriter();
        await writer.WriteAsync(path, MakeConfig());

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task WriteAsync_ConfigIsCorrectlyDeserializedAfterWrite()
    {
        var path = GetTestFilePath();
        var writer = new SteergenConfigWriter();
        var loader = new SteergenConfigLoader();
        var config = MakeConfig("/my/project");

        await writer.WriteAsync(path, config);
        var loaded = await loader.LoadAsync(path);

        Assert.Equal(config.GlobalRoot, loaded.GlobalRoot);
        Assert.Equal(config.ProjectRoot, loaded.ProjectRoot);
        Assert.Equal(config.GenerationRoot, loaded.GenerationRoot);
        Assert.Equal(config.TemplatePackVersion, loaded.TemplatePackVersion);
    }

    [Fact]
    public async Task WriteAsync_WhenFileChangedBetweenReadAndWrite_ThrowsConflictException()
    {
        var path = GetTestFilePath();
        var writer = new SteergenConfigWriter();
        var config = MakeConfig();

        // Write initial content
        await writer.WriteAsync(path, config);

        // Capture the hash of the current state
        var currentBytes = await File.ReadAllBytesAsync(path);
        var capturedHash = SteergenConfigWriter.ComputeFileHash(currentBytes);

        // Simulate external modification
        await File.WriteAllTextAsync(path, "globalRoot: /tampered\n");

        // Now try to write with the old (stale) hash - should throw
        await Assert.ThrowsAsync<ConfigWriteConflictException>(
            () => writer.WriteAsync(path, config, expectedHash: capturedHash));
    }
}
