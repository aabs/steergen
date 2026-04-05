using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.UnitTests.Generation;

/// <summary>
/// Unit tests for <see cref="GeneratedFilePurger"/>: no-glob no-op, unsafe purge blocking,
/// dry-run, and basic remove behavior.
/// </summary>
public sealed class GeneratedFilePurgerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("purger-unit-").FullName;
    private readonly GeneratedFilePurger _purger = new();

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // ── No-op conditions ─────────────────────────────────────────────────────

    [Fact]
    public void Purge_NoGlobs_ReturnsNoOp()
    {
        var policy = new PurgePolicyDefinition { Enabled = true, Roots = [_root], Globs = [] };

        var result = _purger.Purge("speckit", policy);

        Assert.True(result.Success);
        Assert.NotNull(result.NoOpReason);
        Assert.Empty(result.RemovedFiles);
    }

    [Fact]
    public void Purge_EmptyRoots_ReturnsNoOp()
    {
        var policy = new PurgePolicyDefinition { Enabled = true, Roots = [], Globs = ["*.md"] };

        var result = _purger.Purge("speckit", policy);

        Assert.True(result.Success);
        Assert.NotNull(result.NoOpReason);
        Assert.Empty(result.RemovedFiles);
    }

    [Fact]
    public void Purge_DisabledPolicy_ReturnsNoOp()
    {
        var file = Path.Combine(_root, "file.md");
        File.WriteAllText(file, "content");

        var policy = new PurgePolicyDefinition { Enabled = false, Roots = [_root], Globs = ["*.md"] };

        var result = _purger.Purge("speckit", policy);

        Assert.True(result.Success);
        Assert.NotNull(result.NoOpReason);
        Assert.Empty(result.RemovedFiles);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Purge_NonExistentRoot_ReturnsSuccessNoFiles()
    {
        var policy = new PurgePolicyDefinition
        {
            Enabled = true,
            Roots = [Path.Combine(_root, "nonexistent")],
            Globs = ["*.md"],
        };

        var result = _purger.Purge("speckit", policy);

        Assert.True(result.Success);
        Assert.Empty(result.RemovedFiles);
    }

    // ── Matching files are removed ───────────────────────────────────────────

    [Fact]
    public void Purge_MatchingFiles_AreRemoved()
    {
        var file1 = Path.Combine(_root, "a.md");
        var file2 = Path.Combine(_root, "b.md");
        var keepFile = Path.Combine(_root, "keep.txt");
        File.WriteAllText(file1, "content");
        File.WriteAllText(file2, "content");
        File.WriteAllText(keepFile, "keep");

        var policy = new PurgePolicyDefinition { Enabled = true, Roots = [_root], Globs = ["*.md"] };

        var result = _purger.Purge("speckit", policy);

        Assert.True(result.Success);
        Assert.Equal(2, result.RemovedFiles.Count);
        Assert.False(File.Exists(file1));
        Assert.False(File.Exists(file2));
        Assert.True(File.Exists(keepFile), ".txt file should not be purged.");
    }

    [Fact]
    public void Purge_RecursiveGlob_RemovesFilesInSubdirectories()
    {
        var subDir = Path.Combine(_root, "sub");
        Directory.CreateDirectory(subDir);
        var rootFile = Path.Combine(_root, "root.md");
        var subFile = Path.Combine(subDir, "sub.md");
        File.WriteAllText(rootFile, "root");
        File.WriteAllText(subFile, "sub");

        var policy = new PurgePolicyDefinition { Enabled = true, Roots = [_root], Globs = ["**/*.md"] };

        var result = _purger.Purge("speckit", policy);

        Assert.True(result.Success);
        Assert.Equal(2, result.RemovedFiles.Count);
        Assert.False(File.Exists(rootFile));
        Assert.False(File.Exists(subFile));
    }

    [Fact]
    public void Purge_RemovedFiles_AreOrderedDeterministically()
    {
        var files = new[] { "c.md", "a.md", "b.md" };
        foreach (var f in files)
            File.WriteAllText(Path.Combine(_root, f), "content");

        var policy = new PurgePolicyDefinition { Enabled = true, Roots = [_root], Globs = ["*.md"] };

        var result = _purger.Purge("speckit", policy);

        Assert.True(result.Success);
        Assert.True(
            result.RemovedFiles.SequenceEqual(
                result.RemovedFiles.OrderBy(f => f, StringComparer.Ordinal)),
            "Removed files must be sorted deterministically.");
    }

    // ── Dry-run: does not delete ─────────────────────────────────────────────

    [Fact]
    public void Purge_DryRun_ReportsButDoesNotDelete()
    {
        var file = Path.Combine(_root, "file.md");
        File.WriteAllText(file, "content");

        var policy = new PurgePolicyDefinition { Enabled = true, Roots = [_root], Globs = ["*.md"] };

        var result = _purger.Purge("speckit", policy, dryRun: true);

        Assert.True(result.Success);
        Assert.Empty(result.RemovedFiles);
        Assert.Single(result.SkippedFiles);
        Assert.Equal(SkippedPurgeReason.DryRun, result.SkippedFiles[0].Reason);
        Assert.True(File.Exists(file), "Dry-run must not delete files.");
    }

    // ── Safety: root-bounded enforcement ─────────────────────────────────────

    [Fact]
    public void Purge_NonMatchingExtension_IsNotRemoved()
    {
        var keepFile = Path.Combine(_root, "kept.txt");
        File.WriteAllText(keepFile, "content");

        var policy = new PurgePolicyDefinition { Enabled = true, Roots = [_root], Globs = ["*.md"] };

        var result = _purger.Purge("speckit", policy);

        Assert.True(result.Success);
        Assert.Empty(result.RemovedFiles);
        Assert.True(File.Exists(keepFile));
    }

    // ── ResolvePolicy: template variable resolution ──────────────────────────

    [Fact]
    public void ResolvePolicy_ResolvesGlobalRootTemplate()
    {
        var policy = new PurgePolicyDefinition
        {
            Enabled = true,
            Roots = ["${globalRoot}/.speckit"],
            Globs = ["*.md"],
        };
        var context = new Dictionary<string, string> { ["globalRoot"] = "/home/user" };

        var resolved = GeneratedFilePurger.ResolvePolicy(policy, context);

        Assert.Equal("/home/user/.speckit", resolved.Roots[0]);
    }

    [Fact]
    public void ResolvePolicy_NullContext_LeavesTemplatesUnchanged()
    {
        var policy = new PurgePolicyDefinition
        {
            Enabled = true,
            Roots = ["${globalRoot}/.speckit"],
            Globs = ["*.md"],
        };

        var resolved = GeneratedFilePurger.ResolvePolicy(policy, null);

        Assert.Equal("${globalRoot}/.speckit", resolved.Roots[0]);
    }
}
