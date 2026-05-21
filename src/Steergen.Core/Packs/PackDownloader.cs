using System.Formats.Tar;
using System.IO.Compression;
using Steergen.Core.Validation;

namespace Steergen.Core.Packs;

/// <summary>
/// Handles GitHub archive download and extraction to local cache.
/// </summary>
public sealed class PackDownloader
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheBaseDirectory;

    public PackDownloader(HttpClient httpClient, string cacheBaseDirectory)
    {
        _httpClient = httpClient;
        _cacheBaseDirectory = cacheBaseDirectory;
    }

    /// <summary>
    /// Downloads a pack from GitHub to the local cache.
    /// Returns the local cache path on success.
    /// </summary>
    public async Task<PackDownloadResult> DownloadAsync(
        GitHubPackSource source,
        PackType packType,
        bool force,
        CancellationToken cancellationToken = default)
    {
        var cachePath = GetCachedPath(source, packType);

        // If immutable pin, cache exists, and not forced — skip download
        if (!force && IsImmutablePin(source.Ref) && Directory.Exists(cachePath))
        {
            return new PackDownloadResult
            {
                Success = true,
                CachePath = cachePath
            };
        }

        var refValue = source.Ref ?? "HEAD";
        var archiveUrl = $"https://github.com/{source.Owner}/{source.Repo}/archive/{refValue}.tar.gz";

        // Download the archive
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(archiveUrl, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return new PackDownloadResult
            {
                Success = false,
                Diagnostics = [new Diagnostic(
                    "DL001",
                    $"Failed to download pack from {archiveUrl}: {ex.Message}",
                    DiagnosticSeverity.Error)]
            };
        }

        if (!response.IsSuccessStatusCode)
        {
            return new PackDownloadResult
            {
                Success = false,
                Diagnostics = [new Diagnostic(
                    "DL001",
                    $"GitHub repository not accessible: HTTP {(int)response.StatusCode} from {archiveUrl}",
                    DiagnosticSeverity.Error)]
            };
        }

        // Extract to a temp directory for atomic replacement
        var tempDir = Path.Combine(Path.GetTempPath(), $"steergen-pack-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var gzipStream = new GZipStream(responseStream, CompressionMode.Decompress);
            using var tarReader = new TarReader(gzipStream);

            // GitHub tarballs have a top-level directory like {repo}-{ref}/
            // We need to detect and strip this prefix
            string? topLevelPrefix = null;
            var diagnostics = new List<Diagnostic>();

            while (await tarReader.GetNextEntryAsync(cancellationToken: cancellationToken) is { } entry)
            {
                var entryName = entry.Name;

                // Validate path safety
                if (!IsPathSafe(entryName))
                {
                    // Clean up temp directory
                    DeleteDirectorySafe(tempDir);
                    return new PackDownloadResult
                    {
                        Success = false,
                        Diagnostics = [new Diagnostic(
                            "DL003",
                            $"Archive contains path traversal or unsafe path: {entryName}",
                            DiagnosticSeverity.Error)]
                    };
                }

                // Detect the top-level GitHub directory prefix (e.g. repo-ref/).
                // Some archives may start with metadata entries that have no slash;
                // do not lock in an empty prefix until we see a filesystem entry.
                if (topLevelPrefix is null)
                {
                    var firstSlash = entryName.IndexOf('/');
                    if (firstSlash > 0)
                    {
                        topLevelPrefix = entryName[..(firstSlash + 1)];
                    }
                }

                // Strip the top-level prefix
                var relativePath = topLevelPrefix is not null
                    && topLevelPrefix.Length > 0
                    && entryName.StartsWith(topLevelPrefix, StringComparison.Ordinal)
                    ? entryName[topLevelPrefix.Length..]
                    : entryName;

                // Skip the top-level directory entry itself
                if (string.IsNullOrEmpty(relativePath))
                    continue;

                // When source.Path is specified, only extract files under that subdirectory
                if (source.Path is not null)
                {
                    var subDirPrefix = source.Path.TrimEnd('/') + "/";
                    if (!relativePath.StartsWith(subDirPrefix, StringComparison.Ordinal)
                        && relativePath != source.Path.TrimEnd('/'))
                    {
                        continue; // Skip entries not under the specified subdirectory
                    }

                    // Strip the subdirectory prefix from the relative path
                    relativePath = relativePath.StartsWith(subDirPrefix, StringComparison.Ordinal)
                        ? relativePath[subDirPrefix.Length..]
                        : string.Empty;

                    if (string.IsNullOrEmpty(relativePath))
                        continue;
                }

                // Validate the stripped path is still safe
                if (!IsPathSafe(relativePath))
                {
                    DeleteDirectorySafe(tempDir);
                    return new PackDownloadResult
                    {
                        Success = false,
                        Diagnostics = [new Diagnostic(
                            "DL004",
                            $"Archive contains file outside expected directory structure: {relativePath}",
                            DiagnosticSeverity.Error)]
                    };
                }

                var destPath = Path.Combine(tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

                // Verify the resolved path is within the temp directory
                var fullDestPath = Path.GetFullPath(destPath);
                var fullTempDir = Path.GetFullPath(tempDir) + Path.DirectorySeparatorChar;
                if (!fullDestPath.StartsWith(fullTempDir, StringComparison.OrdinalIgnoreCase))
                {
                    DeleteDirectorySafe(tempDir);
                    return new PackDownloadResult
                    {
                        Success = false,
                        Diagnostics = [new Diagnostic(
                            "DL004",
                            $"Archive entry resolves outside expected directory: {relativePath}",
                            DiagnosticSeverity.Error)]
                    };
                }

                switch (entry.EntryType)
                {
                    case TarEntryType.Directory:
                        Directory.CreateDirectory(destPath);
                        break;

                    case TarEntryType.RegularFile:
                    case TarEntryType.V7RegularFile:
                        var parentDir = Path.GetDirectoryName(destPath);
                        if (parentDir is not null)
                            Directory.CreateDirectory(parentDir);

                        await using (var fileStream = File.Create(destPath))
                        {
                            if (entry.DataStream is not null)
                            {
                                await entry.DataStream.CopyToAsync(fileStream, cancellationToken);
                            }
                        }
                        break;

                    // Skip symlinks and other entry types for security
                    default:
                        break;
                }
            }

            // Validate pack.yaml presence
            var packYamlPath = Path.Combine(tempDir, "pack.yaml");
            if (!File.Exists(packYamlPath))
            {
                DeleteDirectorySafe(tempDir);
                return new PackDownloadResult
                {
                    Success = false,
                    Diagnostics = [new Diagnostic(
                        "DL002",
                        $"Downloaded archive does not contain pack.yaml",
                        DiagnosticSeverity.Error)]
                };
            }

            // Atomic swap: move temp directory into cache location
            var cacheParent = Path.GetDirectoryName(cachePath.TrimEnd(Path.DirectorySeparatorChar));
            if (cacheParent is not null)
                Directory.CreateDirectory(cacheParent);

            // Remove existing cache directory if present (atomic replacement)
            if (Directory.Exists(cachePath))
                Directory.Delete(cachePath, recursive: true);

            Directory.Move(tempDir, cachePath.TrimEnd(Path.DirectorySeparatorChar));

            return new PackDownloadResult
            {
                Success = true,
                CachePath = cachePath,
                Diagnostics = diagnostics
            };
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Preserve existing cache on download/extraction failure
            DeleteDirectorySafe(tempDir);
            throw;
        }
    }

    private static void DeleteDirectorySafe(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; don't mask the original error
        }
    }

    /// <summary>
    /// Returns the local cache path for a given source and pack type.
    /// Path format: {cacheBaseDirectory}/{packTypeDir}/{owner}/{repo}/{ref}/
    /// where packTypeDir is "packs" for Template and "rules" for Rules.
    /// </summary>
    public string GetCachedPath(GitHubPackSource source, PackType packType)
    {
        var packTypeDir = packType switch
        {
            PackType.Template => "packs",
            PackType.Rules => "rules",
            _ => throw new ArgumentOutOfRangeException(nameof(packType))
        };

        var refValue = source.Ref ?? "HEAD";

        return Path.Combine(
            _cacheBaseDirectory,
            packTypeDir,
            source.Owner,
            source.Repo,
            refValue) + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Determines if a ref value is an immutable pin — a full 40-character
    /// lowercase hexadecimal Git commit SHA.
    /// </summary>
    /// <param name="refValue">The Git ref string to check (tag, branch, or SHA).</param>
    /// <returns>
    /// <c>true</c> if <paramref name="refValue"/> is exactly 40 characters long
    /// and consists entirely of lowercase hexadecimal characters (0-9, a-f);
    /// <c>false</c> otherwise (including when <paramref name="refValue"/> is null).
    /// </returns>
    public static bool IsImmutablePin(string? refValue)
    {
        if (refValue is null || refValue.Length != 40)
            return false;

        foreach (var c in refValue)
        {
            if (c is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that an archive entry path is safe for extraction into a pack directory.
    /// A path is considered unsafe (returns false) if:
    /// <list type="bullet">
    ///   <item>It is null or empty</item>
    ///   <item>It contains the path traversal sequence <c>../</c> or <c>..\</c></item>
    ///   <item>It starts with <c>/</c> or <c>\</c> (absolute path)</item>
    ///   <item>The normalized resolved path would escape the pack directory root</item>
    /// </list>
    /// </summary>
    /// <param name="entryPath">The relative file path from the archive entry.</param>
    /// <returns>
    /// <c>true</c> if the path is safe for extraction within a pack directory;
    /// <c>false</c> if the path contains traversal sequences, is absolute, or would
    /// resolve outside the pack directory structure.
    /// </returns>
    public static bool IsPathSafe(string? entryPath)
    {
        if (string.IsNullOrEmpty(entryPath))
            return false;

        // Reject absolute paths (starting with / or \)
        if (entryPath[0] is '/' or '\\')
            return false;

        // Normalize separators to forward slash for consistent checking
        var normalized = entryPath.Replace('\\', '/');

        // Reject paths containing the traversal sequence "../"
        if (normalized.Contains("../"))
            return false;

        // Resolve the path segments to detect traversal that escapes the root
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var depth = 0;

        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                depth--;
                if (depth < 0)
                    return false; // Would escape the pack directory
            }
            else if (segment != ".")
            {
                depth++;
            }
        }

        return true;
    }
}
