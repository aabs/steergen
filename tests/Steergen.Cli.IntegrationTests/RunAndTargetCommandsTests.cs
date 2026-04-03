using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Xunit;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]
/// <summary>
/// Integration tests for the <c>run</c> command with explicit <c>--target</c> scoping,
/// and for <c>target add</c>/<c>target remove</c> commands.
/// </summary>
public sealed class RunAndTargetCommandsTests
{
    private static readonly string FixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance"));

    private static string MakeTempDir() => Directory.CreateTempSubdirectory("runtest-").FullName;

    private static async Task<string> WriteConfigAsync(
        string dir,
        string? globalRoot = null,
        string? projectRoot = null,
        IEnumerable<string>? registeredTargets = null)
    {
        var configPath = Path.Combine(dir, "steergen.config.yaml");
        var writer = new SteergenConfigWriter();
        var config = new SteeringConfiguration
        {
            GlobalRoot = globalRoot,
            ProjectRoot = projectRoot,
            RegisteredTargets = (registeredTargets ?? []).ToList(),
        };
        await writer.WriteAsync(configPath, config);
        return configPath;
    }

    // ── Run: explicit --target ───────────────────────────────────────────────

    [Fact]
    public async Task Run_WithExplicitSpeckitTarget_ReturnsExitCode0()
    {
        var outputDir = MakeTempDir();
        try
        {
            var exitCode = await RunCommand.RunAsync(
                configPath: null,
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Run_WithExplicitSpeckitTarget_ProducesConstitutionFile()
    {
        var outputDir = MakeTempDir();
        try
        {
            await RunCommand.RunAsync(
                configPath: null,
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            Assert.True(File.Exists(Path.Combine(outputDir, "speckit", "constitution.md")));
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Run_WithRegisteredTargetsFromConfig_RunsAllRegisteredTargets()
    {
        var workDir = MakeTempDir();
        try
        {
            var outputDir = Path.Combine(workDir, "output");
            Directory.CreateDirectory(outputDir);
            var configPath = await WriteConfigAsync(
                workDir,
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                registeredTargets: ["speckit"]);

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: [],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(Path.Combine(outputDir, "speckit", "constitution.md")));
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    [Fact]
    public async Task Run_WithUnknownTarget_ReturnsExitCode2()
    {
        var outputDir = MakeTempDir();
        try
        {
            var exitCode = await RunCommand.RunAsync(
                configPath: null,
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                outputBase: outputDir,
                explicitTargets: ["nonexistent-target"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(2, exitCode);
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Run_NoTargetsConfiguredAndNoExplicit_ReturnsExitCode0WithNoFiles()
    {
        var workDir = MakeTempDir();
        try
        {
            var configPath = await WriteConfigAsync(
                workDir,
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project"),
                registeredTargets: []);

            var outputDir = Path.Combine(workDir, "output");
            Directory.CreateDirectory(outputDir);

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: [],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    // ── Target add ───────────────────────────────────────────────────────────

    [Fact]
    public async Task TargetAdd_NewTarget_ReturnsExitCode0()
    {
        var workDir = MakeTempDir();
        try
        {
            var configPath = await WriteConfigAsync(workDir);
            var exitCode = await TargetCommand.AddAsync(configPath, "speckit");

            Assert.Equal(0, exitCode);
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    [Fact]
    public async Task TargetAdd_NewTarget_PersistsToConfig()
    {
        var workDir = MakeTempDir();
        try
        {
            var configPath = await WriteConfigAsync(workDir);
            await TargetCommand.AddAsync(configPath, "kiro");

            var loader = new SteergenConfigLoader();
            var loaded = await loader.LoadAsync(configPath);

            Assert.Contains("kiro", loaded.RegisteredTargets);
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    [Fact]
    public async Task TargetAdd_IdempotentSecondCall_ReturnsExitCode0()
    {
        var workDir = MakeTempDir();
        try
        {
            var configPath = await WriteConfigAsync(workDir);
            await TargetCommand.AddAsync(configPath, "speckit");
            var second = await TargetCommand.AddAsync(configPath, "speckit");

            Assert.Equal(0, second);
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    [Fact]
    public async Task TargetAdd_IdempotentSecondCall_DoesNotDuplicateEntry()
    {
        var workDir = MakeTempDir();
        try
        {
            var configPath = await WriteConfigAsync(workDir);
            await TargetCommand.AddAsync(configPath, "speckit");
            await TargetCommand.AddAsync(configPath, "speckit");

            var loader = new SteergenConfigLoader();
            var loaded = await loader.LoadAsync(configPath);

            Assert.Single(loaded.RegisteredTargets, t => t == "speckit");
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    [Fact]
    public async Task TargetAdd_MissingConfigFile_ReturnsExitCode2()
    {
        var exitCode = await TargetCommand.AddAsync("/nonexistent/steergen.config.yaml", "speckit");
        Assert.Equal(2, exitCode);
    }

    // ── Target remove ────────────────────────────────────────────────────────

    [Fact]
    public async Task TargetRemove_ExistingTarget_ReturnsExitCode0()
    {
        var workDir = MakeTempDir();
        try
        {
            var configPath = await WriteConfigAsync(workDir);
            await TargetCommand.AddAsync(configPath, "speckit");
            var exitCode = await TargetCommand.RemoveAsync(configPath, "speckit");

            Assert.Equal(0, exitCode);
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    [Fact]
    public async Task TargetRemove_ExistingTarget_RemovesFromConfig()
    {
        var workDir = MakeTempDir();
        try
        {
            var configPath = await WriteConfigAsync(workDir);
            await TargetCommand.AddAsync(configPath, "speckit");
            await TargetCommand.RemoveAsync(configPath, "speckit");

            var loader = new SteergenConfigLoader();
            var loaded = await loader.LoadAsync(configPath);

            Assert.DoesNotContain("speckit", loaded.RegisteredTargets);
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    [Fact]
    public async Task TargetRemove_NotPresent_ReturnsExitCode0Idempotently()
    {
        var workDir = MakeTempDir();
        try
        {
            var configPath = await WriteConfigAsync(workDir);
            var exitCode = await TargetCommand.RemoveAsync(configPath, "speckit");

            Assert.Equal(0, exitCode);
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    [Fact]
    public async Task TargetRemove_LeavesOtherTargetsIntact()
    {
        var workDir = MakeTempDir();
        try
        {
            var configPath = await WriteConfigAsync(workDir);
            await TargetCommand.AddAsync(configPath, "speckit");
            await TargetCommand.AddAsync(configPath, "kiro");
            await TargetCommand.RemoveAsync(configPath, "speckit");

            var loader = new SteergenConfigLoader();
            var loaded = await loader.LoadAsync(configPath);

            Assert.DoesNotContain("speckit", loaded.RegisteredTargets);
            Assert.Contains("kiro", loaded.RegisteredTargets);
        }
        finally { Directory.Delete(workDir, recursive: true); }
    }

    [Fact]
    public async Task TargetRemove_MissingConfigFile_ReturnsExitCode2()
    {
        var exitCode = await TargetCommand.RemoveAsync("/nonexistent/steergen.config.yaml", "speckit");
        Assert.Equal(2, exitCode);
    }
}
