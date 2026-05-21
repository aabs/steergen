using CsCheck;
using Steergen.Core.Packs;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for rules pack file discovery.
///
/// Property 11: Rules Pack File Discovery
/// For any directory tree, the rules pack file discovery SHALL return all and only
/// files with the .md extension found recursively under the rules root, enumerated
/// in deterministic ordinal sort order, excluding symbolic links.
///
/// **Validates: Requirements 11.1**
/// </summary>
public sealed class FileDiscoveryProperties : IDisposable
{
    private readonly string _tempDir;

    public FileDiscoveryProperties()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"steergen-pbt-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Generators ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a safe directory name segment (lowercase alphanumeric, prefixed with 'd_'
    /// to avoid conflicts with file names).
    /// </summary>
    private static readonly Gen<string> GenDirSegment =
        Gen.String[Gen.Char['a', 'z'], 1, 6]
           .Select(s => $"d_{(s.Length == 0 ? "a" : s)}");

    /// <summary>
    /// Generates a safe file name segment (lowercase alphanumeric, prefixed with 'f_'
    /// to avoid conflicts with directory names).
    /// </summary>
    private static readonly Gen<string> GenFileNameSegment =
        Gen.String[Gen.Char['a', 'z'], 1, 6]
           .Select(s => $"f_{(s.Length == 0 ? "a" : s)}");

    /// <summary>
    /// Generates a file extension (including the dot). Includes .md and various non-.md extensions.
    /// </summary>
    private static readonly Gen<string> GenExtension =
        Gen.OneOf(
            Gen.Const(".md"),
            Gen.Const(".md"),       // Weight .md more heavily to ensure coverage
            Gen.Const(".txt"),
            Gen.Const(".yaml"),
            Gen.Const(".json"),
            Gen.Const(".cs"),
            Gen.Const(".scriban"),
            Gen.Const(".markdown")  // Similar but not .md
        );

    /// <summary>
    /// Generates a relative path (0-3 subdirectory segments) for a file within the tree.
    /// </summary>
    private static readonly Gen<string[]> GenSubdirPath =
        GenDirSegment.Array[0, 3];

    /// <summary>
    /// Represents a file to be created in the test directory tree.
    /// </summary>
    private sealed record FileEntry(string[] SubDirs, string FileName, string Extension);

    /// <summary>
    /// Generates a single file entry with random subdirectory path, name, and extension.
    /// </summary>
    private static readonly Gen<FileEntry> GenFileEntry =
        Gen.Select(GenSubdirPath, GenFileNameSegment, GenExtension)
           .Select((dirs, name, ext) => new FileEntry(dirs, name, ext));

    /// <summary>
    /// Generates a random directory tree specification (1-20 files).
    /// </summary>
    private static readonly Gen<FileEntry[]> GenDirectoryTree =
        GenFileEntry.Array[1, 20];

    // ── Property: discovery returns all and only .md files ────────────────────────

    [Fact]
    public void Discovery_ReturnsAllAndOnly_MdFiles()
    {
        // **Validates: Requirements 11.1**
        GenDirectoryTree
            .Sample(
                entries =>
                {
                    var rootDir = CreateDirectoryTree(entries);

                    var discovered = RulesPackFileDiscovery.DiscoverMarkdownFiles(rootDir);

                    // Compute expected: all entries with .md extension
                    var expectedPaths = entries
                        .Where(e => e.Extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
                        .Select(e => ComputeFilePath(rootDir, e))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(p => p, StringComparer.Ordinal)
                        .ToList();

                    Assert.Equal(expectedPaths.Count, discovered.Count);
                    for (int i = 0; i < expectedPaths.Count; i++)
                    {
                        Assert.Equal(expectedPaths[i], discovered[i]);
                    }
                },
                iter: 100,
                print: entries => $"files={entries.Length}, mdFiles={entries.Count(e => e.Extension == ".md")}");
    }

    // ── Property: discovery returns files in ordinal sort order ───────────────────

    [Fact]
    public void Discovery_ReturnsPaths_InOrdinalSortOrder()
    {
        // **Validates: Requirements 11.1**
        GenDirectoryTree
            .Sample(
                entries =>
                {
                    var rootDir = CreateDirectoryTree(entries);

                    var discovered = RulesPackFileDiscovery.DiscoverMarkdownFiles(rootDir);

                    // Verify ordinal sort order
                    var sorted = discovered.OrderBy(p => p, StringComparer.Ordinal).ToList();
                    Assert.Equal(sorted, discovered);
                },
                iter: 100,
                print: entries => $"files={entries.Length}");
    }

    // ── Property: discovery never returns non-.md files ──────────────────────────

    [Fact]
    public void Discovery_NeverReturns_NonMdFiles()
    {
        // **Validates: Requirements 11.1**
        GenDirectoryTree
            .Sample(
                entries =>
                {
                    var rootDir = CreateDirectoryTree(entries);

                    var discovered = RulesPackFileDiscovery.DiscoverMarkdownFiles(rootDir);

                    // Every discovered file must have .md extension
                    foreach (var path in discovered)
                    {
                        Assert.True(
                            path.EndsWith(".md", StringComparison.OrdinalIgnoreCase),
                            $"Discovered file '{path}' does not have .md extension");
                    }
                },
                iter: 100,
                print: entries => $"files={entries.Length}");
    }

    // ── Property: discovery is deterministic (same tree → same result) ───────────

    [Fact]
    public void Discovery_IsDeterministic_ForSameTree()
    {
        // **Validates: Requirements 11.1**
        GenDirectoryTree
            .Sample(
                entries =>
                {
                    var rootDir = CreateDirectoryTree(entries);

                    var result1 = RulesPackFileDiscovery.DiscoverMarkdownFiles(rootDir);
                    var result2 = RulesPackFileDiscovery.DiscoverMarkdownFiles(rootDir);

                    Assert.Equal(result1, result2);
                },
                iter: 100,
                print: entries => $"files={entries.Length}");
    }

    // ── Property: discovery finds files in nested subdirectories ──────────────────

    [Fact]
    public void Discovery_FindsFiles_InNestedSubdirectories()
    {
        // **Validates: Requirements 11.1**
        // Generate trees that always have at least one .md file in a subdirectory
        Gen.Select(GenSubdirPath.Where(d => d.Length > 0), GenDirectoryTree)
            .Sample(
                (deepDirs, otherEntries) =>
                {
                    // Use a unique name that won't collide with generated entries
                    var uniqueName = $"f_unique_{Guid.NewGuid():N}";
                    var deepEntry = new FileEntry(deepDirs, uniqueName, ".md");
                    var allEntries = otherEntries.Append(deepEntry).ToArray();

                    var rootDir = CreateDirectoryTree(allEntries);

                    var discovered = RulesPackFileDiscovery.DiscoverMarkdownFiles(rootDir);

                    var deepPath = ComputeFilePath(rootDir, deepEntry);
                    Assert.Contains(deepPath, discovered);
                },
                iter: 100,
                print: t => $"deepPath depth={t.Item1.Length}, otherFiles={t.Item2.Length}");
    }

    // ── Property: empty directory returns empty list ──────────────────────────────

    [Fact]
    public void Discovery_ReturnsEmptyList_ForEmptyDirectory()
    {
        // **Validates: Requirements 11.1**
        var emptyDir = Path.Combine(_tempDir, $"empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyDir);

        var discovered = RulesPackFileDiscovery.DiscoverMarkdownFiles(emptyDir);

        Assert.Empty(discovered);
    }

    // ── Property: non-existent directory throws ──────────────────────────────────

    [Fact]
    public void Discovery_Throws_ForNonExistentDirectory()
    {
        // **Validates: Requirements 11.1**
        var nonExistent = Path.Combine(_tempDir, "does-not-exist");

        Assert.Throws<DirectoryNotFoundException>(
            () => RulesPackFileDiscovery.DiscoverMarkdownFiles(nonExistent));
    }

    // ── Property: symlinks to .md files are excluded ─────────────────────────────

    [Fact]
    public void Discovery_Excludes_SymlinksToMdFiles()
    {
        // **Validates: Requirements 11.1**
        // Note: This test creates actual symlinks. On Windows, this may require
        // developer mode or elevated privileges. If symlink creation fails,
        // the test is skipped gracefully.
        var rootDir = Path.Combine(_tempDir, $"symlink-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDir);

        // Create a real .md file
        var realFile = Path.Combine(rootDir, "real.md");
        File.WriteAllText(realFile, "# Real file");

        // Attempt to create a symlink to the .md file
        var symlinkPath = Path.Combine(rootDir, "linked.md");
        try
        {
            File.CreateSymbolicLink(symlinkPath, realFile);
        }
        catch (IOException)
        {
            // Symlink creation not supported or insufficient privileges — skip
            return;
        }
        catch (UnauthorizedAccessException)
        {
            // Insufficient privileges — skip
            return;
        }

        // Verify the symlink was actually created
        if (!File.Exists(symlinkPath))
            return;

        var discovered = RulesPackFileDiscovery.DiscoverMarkdownFiles(rootDir);

        // Should contain the real file but NOT the symlink
        Assert.Contains(realFile, discovered);
        Assert.DoesNotContain(symlinkPath, discovered);
    }

    // ── Property: symlinks to .md files in subdirectories are excluded ────────────

    [Fact]
    public void Discovery_Excludes_SymlinksInSubdirectories()
    {
        // **Validates: Requirements 11.1**
        var rootDir = Path.Combine(_tempDir, $"symlink-subdir-{Guid.NewGuid():N}");
        var subDir = Path.Combine(rootDir, "sub");
        Directory.CreateDirectory(subDir);

        // Create a real .md file in root
        var realFile = Path.Combine(rootDir, "real.md");
        File.WriteAllText(realFile, "# Real file");

        // Create a real .md file in subdir
        var realSubFile = Path.Combine(subDir, "sub-real.md");
        File.WriteAllText(realSubFile, "# Sub real file");

        // Attempt to create a symlink in subdir pointing to root file
        var symlinkPath = Path.Combine(subDir, "linked.md");
        try
        {
            File.CreateSymbolicLink(symlinkPath, realFile);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        if (!File.Exists(symlinkPath))
            return;

        var discovered = RulesPackFileDiscovery.DiscoverMarkdownFiles(rootDir);

        // Should contain both real files but NOT the symlink
        Assert.Contains(realFile, discovered);
        Assert.Contains(realSubFile, discovered);
        Assert.DoesNotContain(symlinkPath, discovered);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a directory tree from the given file entries and returns the root path.
    /// </summary>
    private string CreateDirectoryTree(FileEntry[] entries)
    {
        var rootDir = Path.Combine(_tempDir, $"tree-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDir);

        foreach (var entry in entries)
        {
            var filePath = ComputeFilePath(rootDir, entry);
            var dir = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(dir);

            // Only write if file doesn't already exist (handles duplicate entries)
            if (!File.Exists(filePath))
                File.WriteAllText(filePath, $"# Content for {entry.FileName}");
        }

        return rootDir;
    }

    /// <summary>
    /// Computes the full file path for a given entry within the root directory.
    /// </summary>
    private static string ComputeFilePath(string rootDir, FileEntry entry)
    {
        var segments = entry.SubDirs.Append($"{entry.FileName}{entry.Extension}").ToArray();
        return Path.Combine(rootDir, Path.Combine(segments));
    }
}
