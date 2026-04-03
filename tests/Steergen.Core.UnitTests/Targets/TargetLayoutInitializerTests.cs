using Steergen.Core.Targets;

namespace Steergen.Core.UnitTests.Targets;

/// <summary>
/// Unit tests for TargetLayoutInitializer covering folder rules and invalid-target-identifier rejection.
/// </summary>
public sealed class TargetLayoutInitializerTests
{
    // ── IsValidTargetId ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("speckit")]
    [InlineData("kiro")]
    [InlineData("copilot-agent")]
    [InlineData("kiro-agent")]
    public void IsValidTargetId_KnownTargets_ReturnsTrue(string id) =>
        Assert.True(TargetLayoutInitializer.IsValidTargetId(id));

    [Theory]
    [InlineData("unknown")]
    [InlineData("SPECKIT")]      // case-sensitive
    [InlineData("")]
    [InlineData("my-plugin")]
    public void IsValidTargetId_UnknownTargets_ReturnsFalse(string id) =>
        Assert.False(TargetLayoutInitializer.IsValidTargetId(id));

    // ── GetLayoutFolders ─────────────────────────────────────────────────────

    [Fact]
    public void GetLayoutFolders_AlwaysIncludesSharedSteeringFolders()
    {
        var root = Path.GetTempPath();
        var folders = TargetLayoutInitializer.GetLayoutFolders(root, "speckit");

        Assert.Contains(folders, f => f.EndsWith(Path.Combine("steering", "global"), StringComparison.Ordinal));
        Assert.Contains(folders, f => f.EndsWith(Path.Combine("steering", "project"), StringComparison.Ordinal));
    }

    [Fact]
    public void GetLayoutFolders_IncludesPerTargetOutputFolder()
    {
        var root = Path.GetTempPath();
        var folders = TargetLayoutInitializer.GetLayoutFolders(root, "kiro");

        Assert.Contains(folders, f => f.EndsWith("kiro", StringComparison.Ordinal));
    }

    // ── Initialize: invalid target ───────────────────────────────────────────

    [Fact]
    public void Initialize_UnknownTargetId_ReturnsFailure()
    {
        var root = CreateTempDir();
        try
        {
            var result = TargetLayoutInitializer.Initialize(root, ["unknown-target"]);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("unknown-target", result.ErrorMessage);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Initialize_UnknownTargetId_CreatesNoFolders()
    {
        var root = CreateTempDir();
        try
        {
            TargetLayoutInitializer.Initialize(root, ["bad-id"]);

            // Only the root should exist - no sub-folders were created.
            Assert.Empty(Directory.GetDirectories(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ── Initialize: folder creation ──────────────────────────────────────────

    [Fact]
    public void Initialize_ValidTarget_CreatesExpectedFolders()
    {
        var root = CreateTempDir();
        try
        {
            var result = TargetLayoutInitializer.Initialize(root, ["speckit"]);

            Assert.True(result.Success);
            Assert.True(Directory.Exists(Path.Combine(root, "steering", "global")));
            Assert.True(Directory.Exists(Path.Combine(root, "steering", "project")));
            Assert.True(Directory.Exists(Path.Combine(root, "speckit")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Initialize_ValidTarget_CreatedFoldersListed()
    {
        var root = CreateTempDir();
        try
        {
            var result = TargetLayoutInitializer.Initialize(root, ["kiro"]);

            Assert.True(result.Success);
            Assert.NotEmpty(result.CreatedFolders);
            Assert.All(result.CreatedFolders, f => Assert.True(Directory.Exists(f)));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ── Initialize: idempotency ──────────────────────────────────────────────

    [Fact]
    public void Initialize_CalledTwice_SecondCallReportsExistingFolders()
    {
        var root = CreateTempDir();
        try
        {
            TargetLayoutInitializer.Initialize(root, ["speckit"]);
            var second = TargetLayoutInitializer.Initialize(root, ["speckit"]);

            Assert.True(second.Success);
            Assert.Empty(second.CreatedFolders);
            Assert.NotEmpty(second.ExistingFolders);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ── Initialize: multiple targets ─────────────────────────────────────────

    [Fact]
    public void Initialize_MultipleTargets_CreatesAllOutputFolders()
    {
        var root = CreateTempDir();
        try
        {
            var result = TargetLayoutInitializer.Initialize(root, ["speckit", "kiro"]);

            Assert.True(result.Success);
            Assert.True(Directory.Exists(Path.Combine(root, "speckit")));
            Assert.True(Directory.Exists(Path.Combine(root, "kiro")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Initialize_MultipleTargets_SharedFoldersCreatedOnce()
    {
        var root = CreateTempDir();
        try
        {
            var result = TargetLayoutInitializer.Initialize(root, ["speckit", "kiro"]);

            // Shared dirs appear once in CreatedFolders even for multiple targets.
            var globalCount = result.CreatedFolders
                .Count(f => f.EndsWith(Path.Combine("steering", "global"), StringComparison.Ordinal));
            Assert.Equal(1, globalCount);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ── Initialize: mixed valid/invalid ─────────────────────────────────────

    [Fact]
    public void Initialize_MixedValidAndInvalidTargets_ReturnsFailureForAll()
    {
        var root = CreateTempDir();
        try
        {
            var result = TargetLayoutInitializer.Initialize(root, ["speckit", "bad-target"]);

            Assert.False(result.Success);
            Assert.Contains("bad-target", result.ErrorMessage!);
            // No folders should have been created.
            Assert.Empty(Directory.GetDirectories(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"steergen-init-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
