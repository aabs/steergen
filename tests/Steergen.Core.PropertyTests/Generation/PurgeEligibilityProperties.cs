using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.PropertyTests.Generation;

/// <summary>
/// Property tests for purge eligibility invariants in <see cref="GeneratedFilePurger"/>.
/// Verifies root-bounded safety, glob-driven eligibility, and no-op conditions.
/// </summary>
public sealed class PurgeEligibilityProperties : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("purge-props-").FullName;
    private readonly GeneratedFilePurger _purger = new();

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // ── Property: files within root matching globs are removed ───────────────

    [Fact]
    public void Purge_FilesUnderRoot_MatchingGlob_AreRemoved()
    {
        var file = Path.Combine(_root, "rule.md");
        File.WriteAllText(file, "content");

        var policy = new PurgePolicyDefinition
        {
            Enabled = true,
            Roots = [_root],
            Globs = ["*.md"],
        };

        var result = _purger.Purge("speckit", policy);

        Assert.True(result.Success);
        Assert.Contains(Path.GetFullPath(file), result.RemovedFiles);
        Assert.False(File.Exists(file), "File should have been deleted.");
    }

    // ── Property: files outside root are NEVER eligible ──────────────────────

    [Fact]
    public void Purge_FileOutsideRoot_IsNeverEligible()
    {
        // Create a file OUTSIDE the configured root
        var outsideRoot = Directory.CreateTempSubdirectory("purge-outside-").FullName;
        try
        {
            var outsideFile = Path.Combine(outsideRoot, "outside.md");
            File.WriteAllText(outsideFile, "content");

            // Configure with an unrelated root that happens to share a prefix
            var policy = new PurgePolicyDefinition
            {
                Enabled = true,
                Roots = [_root],
                Globs = ["*.md"],
            };

            var result = _purger.Purge("speckit", policy);

            Assert.True(result.Success);
            Assert.True(
                !result.RemovedFiles.Contains(Path.GetFullPath(outsideFile)),
                "A file outside the configured root must never be removed.");
            Assert.True(File.Exists(outsideFile), "The outside file should still exist.");
        }
        finally
        {
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    // ── Property: empty globs always produce no-op ───────────────────────────

    [Fact]
    public void Purge_EmptyGlobs_AlwaysNoOp()
    {
        var file = Path.Combine(_root, "kept.md");
        File.WriteAllText(file, "content");

        var policy = new PurgePolicyDefinition
        {
            Enabled = true,
            Roots = [_root],
            Globs = [],
        };

        var result = _purger.Purge("speckit", policy);

        Assert.True(result.Success);
        Assert.NotNull(result.NoOpReason);
        Assert.Empty(result.RemovedFiles);
        Assert.True(File.Exists(file), "File should NOT have been deleted.");
    }

    // ── Property: disabled purge is always a no-op ───────────────────────────

    [Fact]
    public void Purge_DisabledPolicy_AlwaysNoOp()
    {
        var file = Path.Combine(_root, "kept.md");
        File.WriteAllText(file, "content");

        var policy = new PurgePolicyDefinition
        {
            Enabled = false,
            Roots = [_root],
            Globs = ["*.md"],
        };

        var result = _purger.Purge("speckit", policy);

        Assert.True(result.Success);
        Assert.NotNull(result.NoOpReason);
        Assert.Empty(result.RemovedFiles);
        Assert.True(File.Exists(file), "File should NOT have been deleted when disabled.");
    }

    // ── Property: dry-run never deletes files ────────────────────────────────

    [Fact]
    public void Purge_DryRun_NeverDeletesFiles()
    {
        var file1 = Path.Combine(_root, "a.md");
        var file2 = Path.Combine(_root, "b.md");
        File.WriteAllText(file1, "content");
        File.WriteAllText(file2, "content");

        var policy = new PurgePolicyDefinition
        {
            Enabled = true,
            Roots = [_root],
            Globs = ["*.md"],
        };

        var result = _purger.Purge("speckit", policy, dryRun: true);

        Assert.True(result.Success);
        Assert.Empty(result.RemovedFiles);
        Assert.All(result.SkippedFiles, s =>
            Assert.Equal(SkippedPurgeReason.DryRun, s.Reason));
        Assert.True(File.Exists(file1), "Dry-run must not delete files.");
        Assert.True(File.Exists(file2), "Dry-run must not delete files.");
    }
}
