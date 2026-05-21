using CsCheck;
using Steergen.Core.Packs;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for path traversal rejection in archive entry validation.
///
/// Property 13: Path Traversal Rejection
/// For any file path extracted from a downloaded archive, the path SHALL be rejected
/// if it contains the sequence "../" or if the resolved path would place the file
/// outside the expected pack directory structure. All paths without traversal sequences
/// that resolve within the pack directory SHALL be accepted.
///
/// Validates: Requirements 14.3, 14.4
/// </summary>
public sealed class PathTraversalProperties
{
    // ── Generators ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates safe path segments (no traversal, no separators, non-empty).
    /// </summary>
    private static readonly Gen<string> GenSafeSegment =
        Gen.String[Gen.Char.AlphaNumeric, 1, 12]
           .Select(s => s.Length == 0 ? "a" : s);

    /// <summary>
    /// Generates valid relative paths that should always be accepted.
    /// These are paths with 1-5 segments joined by '/', no traversal sequences.
    /// </summary>
    private static readonly Gen<string> GenSafePath =
        GenSafeSegment.Array[1, 5]
            .Select(segments => string.Join("/", segments));

    /// <summary>
    /// Generates paths containing "../" traversal sequences that escape the root.
    /// Strategy: generate N down-segments then N+1 ".." segments, ensuring escape.
    /// </summary>
    private static readonly Gen<string> GenTraversalPath =
        Gen.Select(
            Gen.Int[0, 3],
            GenSafeSegment.Array[0, 2])
        .Select((downCount, suffix) =>
        {
            // Create downCount segments down, then downCount+1 ".." to escape root
            var down = downCount > 0
                ? string.Join("/", Enumerable.Range(0, downCount).Select(i => $"d{i}")) + "/"
                : "";
            var up = string.Join("/", Enumerable.Repeat("..", downCount + 1));
            var suffixPart = suffix.Length > 0 ? "/" + string.Join("/", suffix) : "";
            return $"{down}{up}{suffixPart}";
        });

    /// <summary>
    /// Generates absolute paths starting with '/' or '\'.
    /// </summary>
    private static readonly Gen<string> GenAbsolutePath =
        Gen.Select(
            Gen.OneOf(Gen.Const('/'), Gen.Const('\\')),
            GenSafeSegment.Array[1, 4])
        .Select((sep, segments) => sep + string.Join("/", segments));

    // ── Property: paths with "../" traversal sequences are always rejected ───────

    [Fact]
    public void PathsWithTraversalSequences_AreAlwaysRejected()
    {
        // **Validates: Requirements 14.3**
        GenTraversalPath
            .Sample(
                path =>
                {
                    Assert.False(
                        PackDownloader.IsPathSafe(path),
                        $"Expected path '{path}' to be rejected (contains traversal)");
                },
                iter: 200,
                print: path => $"path=\"{path}\"");
    }

    // ── Property: absolute paths are always rejected ─────────────────────────────

    [Fact]
    public void AbsolutePaths_AreAlwaysRejected()
    {
        // **Validates: Requirements 14.4**
        GenAbsolutePath
            .Sample(
                path =>
                {
                    Assert.False(
                        PackDownloader.IsPathSafe(path),
                        $"Expected absolute path '{path}' to be rejected");
                },
                iter: 200,
                print: path => $"path=\"{path}\"");
    }

    // ── Property: safe relative paths are always accepted ────────────────────────

    [Fact]
    public void SafeRelativePaths_AreAlwaysAccepted()
    {
        // **Validates: Requirements 14.3, 14.4**
        GenSafePath
            .Sample(
                path =>
                {
                    Assert.True(
                        PackDownloader.IsPathSafe(path),
                        $"Expected safe path '{path}' to be accepted");
                },
                iter: 200,
                print: path => $"path=\"{path}\"");
    }

    // ── Property: null and empty paths are rejected ──────────────────────────────

    [Fact]
    public void NullAndEmptyPaths_AreRejected()
    {
        // **Validates: Requirements 14.3, 14.4**
        Assert.False(PackDownloader.IsPathSafe(null));
        Assert.False(PackDownloader.IsPathSafe(""));
    }

    // ── Property: paths with backslash traversal are rejected ────────────────────

    [Fact]
    public void PathsWithBackslashTraversal_AreRejected()
    {
        // **Validates: Requirements 14.3**
        GenSafeSegment.Array[1, 3]
            .Select(segments => string.Join("\\", segments) + "\\..\\" + "escape")
            .Sample(
                path =>
                {
                    Assert.False(
                        PackDownloader.IsPathSafe(path),
                        $"Expected path '{path}' with backslash traversal to be rejected");
                },
                iter: 100,
                print: path => $"path=\"{path}\"");
    }

    // ── Property: paths that resolve outside root via depth exhaustion are rejected

    [Fact]
    public void PathsThatEscapeRootViaDepthExhaustion_AreRejected()
    {
        // Generate paths like "a/../.." which go one level above root
        // **Validates: Requirements 14.4**
        Gen.Int[1, 4]
            .Select(depth =>
            {
                // Create a path with `depth` segments down, then `depth + 1` ".." segments up
                var down = string.Join("/", Enumerable.Range(0, depth).Select(i => $"d{i}"));
                var up = string.Join("/", Enumerable.Repeat("..", depth + 1));
                return $"{down}/{up}";
            })
            .Sample(
                path =>
                {
                    Assert.False(
                        PackDownloader.IsPathSafe(path),
                        $"Expected path '{path}' to be rejected (escapes root via depth exhaustion)");
                },
                iter: 100,
                print: path => $"path=\"{path}\"");
    }

    // ── Property: mixed safe paths with "." current-dir references are accepted ──

    [Fact]
    public void PathsWithCurrentDirReferences_AreAccepted()
    {
        // Paths like "a/./b/./c" should be accepted (single dot is current dir, not traversal)
        // **Validates: Requirements 14.3, 14.4**
        GenSafeSegment.Array[1, 4]
            .Select(segments => string.Join("/./", segments))
            .Sample(
                path =>
                {
                    Assert.True(
                        PackDownloader.IsPathSafe(path),
                        $"Expected path '{path}' with '.' references to be accepted");
                },
                iter: 100,
                print: path => $"path=\"{path}\"");
    }
}
