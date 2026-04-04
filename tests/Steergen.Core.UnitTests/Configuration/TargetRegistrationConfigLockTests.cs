using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Xunit;

namespace Steergen.Core.UnitTests.Configuration;

/// <summary>
/// Tests for optimistic-lock conflict scenarios in <see cref="TargetRegistrationService"/>.
/// </summary>
public sealed class TargetRegistrationConfigLockTests
{
    private static string MakeTempConfigPath()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "testdata", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "steergen.config.yaml");
    }

    private static async Task WriteInitialConfigAsync(string path)
    {
        var writer = new SteergenConfigWriter();
        var config = new SteeringConfiguration
        {
            GlobalRoot = "/global",
            ProjectRoot = "/project",
            RegisteredTargets = [],
        };
        await writer.WriteAsync(path, config);
    }

    // ── Add: happy path ──────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_NewTarget_ReturnsSuccess()
    {
        var path = MakeTempConfigPath();
        await WriteInitialConfigAsync(path);

        var svc = new TargetRegistrationService();
        var result = await svc.AddAsync(path, "speckit");

        Assert.True(result.Success);
        Assert.False(result.WasAlreadyPresent);
        Assert.Equal("speckit", result.TargetId);
    }

    [Fact]
    public async Task AddAsync_NewTarget_PersistsTargetInFile()
    {
        var path = MakeTempConfigPath();
        await WriteInitialConfigAsync(path);

        var svc = new TargetRegistrationService();
        await svc.AddAsync(path, "kiro");

        var loader = new SteergenConfigLoader();
        var loaded = await loader.LoadAsync(path);

        Assert.Contains("kiro", loaded.RegisteredTargets);
    }

    [Fact]
    public async Task AddAsync_DuplicateTarget_ReturnsAlreadyPresent()
    {
        var path = MakeTempConfigPath();
        await WriteInitialConfigAsync(path);

        var svc = new TargetRegistrationService();
        await svc.AddAsync(path, "speckit");

        var result = await svc.AddAsync(path, "speckit");

        Assert.True(result.Success);
        Assert.True(result.WasAlreadyPresent);
    }

    [Fact]
    public async Task AddAsync_DuplicateTarget_DoesNotDuplicateEntry()
    {
        var path = MakeTempConfigPath();
        await WriteInitialConfigAsync(path);

        var svc = new TargetRegistrationService();
        await svc.AddAsync(path, "speckit");
        await svc.AddAsync(path, "speckit");

        var loader = new SteergenConfigLoader();
        var loaded = await loader.LoadAsync(path);

        Assert.Single(loaded.RegisteredTargets, t => t == "speckit");
    }

    // ── Add: conflict ────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_WhenFileChangedAfterRead_ThrowsConflictException()
    {
        var path = MakeTempConfigPath();
        await WriteInitialConfigAsync(path);

        // Intercept: read hash, then externally modify, then try to write via service
        var bytes = await File.ReadAllBytesAsync(path);
        var hash = SteergenConfigWriter.ComputeFileHash(bytes);

        // Simulate external modification
        await File.AppendAllTextAsync(path, "# external change\n");

        // The service reads the current file normally; we simulate the conflict
        // by directly invoking the writer with the stale hash
        var loader = new SteergenConfigLoader();
        var config = await loader.LoadAsync(path);
        var updated = config with { RegisteredTargets = ["speckit"] };

        var writer = new SteergenConfigWriter();
        await Assert.ThrowsAsync<ConfigWriteConflictException>(
            () => writer.WriteAsync(path, updated, hash));
    }

    // ── Remove: happy path ───────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_ExistingTarget_ReturnsSuccess()
    {
        var path = MakeTempConfigPath();
        await WriteInitialConfigAsync(path);

        var svc = new TargetRegistrationService();
        await svc.AddAsync(path, "speckit");

        var result = await svc.RemoveAsync(path, "speckit");

        Assert.True(result.Success);
        Assert.False(result.WasNotPresent);
        Assert.Equal("speckit", result.TargetId);
    }

    [Fact]
    public async Task RemoveAsync_ExistingTarget_RemovesFromFile()
    {
        var path = MakeTempConfigPath();
        await WriteInitialConfigAsync(path);

        var svc = new TargetRegistrationService();
        await svc.AddAsync(path, "speckit");
        await svc.RemoveAsync(path, "speckit");

        var loader = new SteergenConfigLoader();
        var loaded = await loader.LoadAsync(path);

        Assert.DoesNotContain("speckit", loaded.RegisteredTargets);
    }

    [Fact]
    public async Task RemoveAsync_TargetNotPresent_ReturnsNotPresent()
    {
        var path = MakeTempConfigPath();
        await WriteInitialConfigAsync(path);

        var svc = new TargetRegistrationService();
        var result = await svc.RemoveAsync(path, "unknown-target");

        Assert.True(result.Success);
        Assert.True(result.WasNotPresent);
    }

    [Fact]
    public async Task RemoveAsync_OnlyRemovesMatchingTarget_LeavesOthersIntact()
    {
        var path = MakeTempConfigPath();
        await WriteInitialConfigAsync(path);

        var svc = new TargetRegistrationService();
        await svc.AddAsync(path, "speckit");
        await svc.AddAsync(path, "kiro");
        await svc.RemoveAsync(path, "speckit");

        var loader = new SteergenConfigLoader();
        var loaded = await loader.LoadAsync(path);

        Assert.DoesNotContain("speckit", loaded.RegisteredTargets);
        Assert.Contains("kiro", loaded.RegisteredTargets);
    }

    // ── Missing config file ──────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_MissingConfigFile_ReturnsFailure()
    {
        var svc = new TargetRegistrationService();
        var result = await svc.AddAsync("/nonexistent/path/steergen.config.yaml", "speckit");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task RemoveAsync_MissingConfigFile_ReturnsFailure()
    {
        var svc = new TargetRegistrationService();
        var result = await svc.RemoveAsync("/nonexistent/path/steergen.config.yaml", "speckit");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }
}
