using Steergen.Cli.Commands;
using Steergen.Core.Configuration;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]

/// <summary>
/// Integration tests for the <c>init</c> command covering multi-target bootstrap, idempotency,
/// invalid target IDs, and missing project-root handling.
/// </summary>
public sealed class InitCommandTests
{
    // ── Happy-path: single target ────────────────────────────────────────────

    [Fact]
    public void Init_SingleValidTarget_ReturnsExitCode0()
    {
        var root = CreateTempDir();
        try
        {
            var result = InitCommand.RunAsync(root, ["speckit"]);
            Assert.Equal(0, result);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Init_SingleValidTarget_CreatesSteeringFolders()
    {
        var root = CreateTempDir();
        try
        {
            InitCommand.RunAsync(root, ["speckit"]);

            Assert.True(Directory.Exists(Path.Combine(root, "steering", "global")));
            Assert.True(Directory.Exists(Path.Combine(root, "steering", "project")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Init_SingleValidTarget_CreatesTargetOutputFolder()
    {
        var root = CreateTempDir();
        try
        {
            InitCommand.RunAsync(root, ["kiro"]);

            Assert.True(Directory.Exists(Path.Combine(root, ".kiro", "steering")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Init_SingleValidTarget_WritesBootstrapConfigFile()
    {
        var root = CreateTempDir();
        try
        {
            var result = InitCommand.RunAsync(root, ["speckit"]);

            Assert.Equal(0, result);
            Assert.True(File.Exists(Path.Combine(root, "steergen.config.yaml")));

            var loader = new SteergenConfigLoader();
            var config = await loader.LoadAsync(Path.Combine(root, "steergen.config.yaml"));

            Assert.Equal(Path.Combine(root, "steering", "global"), config.GlobalRoot);
            Assert.Equal(Path.Combine(root, "steering", "project"), config.ProjectRoot);
            Assert.Equal(["speckit"], config.RegisteredTargets);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ── Multi-target bootstrap ───────────────────────────────────────────────

    [Fact]
    public void Init_MultipleValidTargets_CreatesAllOutputFolders()
    {
        var root = CreateTempDir();
        try
        {
            var result = InitCommand.RunAsync(root, ["speckit", "kiro", "copilot-agent"]);

            Assert.Equal(0, result);
            Assert.True(Directory.Exists(Path.Combine(root, ".speckit", "memory")));
            Assert.True(Directory.Exists(Path.Combine(root, ".kiro", "steering")));
            Assert.True(Directory.Exists(Path.Combine(root, ".github")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ── Idempotency ──────────────────────────────────────────────────────────

    [Fact]
    public void Init_CalledTwiceWithSameTarget_BothCallsReturnExitCode0()
    {
        var root = CreateTempDir();
        try
        {
            var first = InitCommand.RunAsync(root, ["speckit"]);
            var second = InitCommand.RunAsync(root, ["speckit"]);

            Assert.Equal(0, first);
            Assert.Equal(0, second);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Init_CalledTwiceWithSameTarget_FoldersStillExistAndAreNotDeleted()
    {
        var root = CreateTempDir();
        try
        {
            // Place a sentinel file inside the target folder to verify it is not wiped.
            InitCommand.RunAsync(root, ["speckit"]);
            var sentinel = Path.Combine(root, ".speckit", "memory", "sentinel.txt");
            File.WriteAllText(sentinel, "do not delete");

            InitCommand.RunAsync(root, ["speckit"]);

            Assert.True(File.Exists(sentinel), "Sentinel file must not be deleted on second init");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ── Error cases ──────────────────────────────────────────────────────────

    [Fact]
    public void Init_UnknownTarget_ReturnsExitCode2()
    {
        var root = CreateTempDir();
        try
        {
            var result = InitCommand.RunAsync(root, ["unknown-target"]);
            Assert.Equal(2, result);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Init_UnknownTarget_CreatesNoFolders()
    {
        var root = CreateTempDir();
        try
        {
            InitCommand.RunAsync(root, ["bad-id"]);

            Assert.Empty(Directory.GetDirectories(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Init_MissingProjectRoot_ReturnsExitCode2()
    {
        var missingDir = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}");
        var result = InitCommand.RunAsync(missingDir, ["speckit"]);
        Assert.Equal(2, result);
    }

    // ── No targets ──────────────────────────────────────────────────────────

    [Fact]
    public void Init_NoTargets_ReturnsExitCode0AndCreatesSteeringFolders()
    {
        var root = CreateTempDir();
        try
        {
            var result = InitCommand.RunAsync(root, []);

            Assert.Equal(0, result);
            Assert.True(Directory.Exists(Path.Combine(root, "steering", "global")));
            Assert.True(Directory.Exists(Path.Combine(root, "steering", "project")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Init_NoTargets_WritesConfigWithEmptyRegisteredTargets()
    {
        var root = CreateTempDir();
        try
        {
            var result = InitCommand.RunAsync(root, []);

            Assert.Equal(0, result);

            var loader = new SteergenConfigLoader();
            var config = await loader.LoadAsync(Path.Combine(root, "steergen.config.yaml"));

            Assert.Empty(config.RegisteredTargets);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"steergen-init-integ-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
