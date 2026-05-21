using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using Steergen.Core.Packs;

namespace Steergen.Core.UnitTests.Packs;

/// <summary>
/// Unit tests for <see cref="PackDownloader"/> HTTP interactions, atomic replacement,
/// immutable pin skip logic, default-branch resolution, and subdirectory extraction.
///
/// Requirements: 3.3, 3.5, 4.4, 4.6, 4.8, 9.5
/// </summary>
public sealed class PackDownloaderTests : IDisposable
{
    private readonly string _cacheBase;

    public PackDownloaderTests()
    {
        _cacheBase = Path.Combine(Path.GetTempPath(), "PackDownloaderTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_cacheBase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheBase))
            Directory.Delete(_cacheBase, recursive: true);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a tar.gz archive in memory containing a pack.yaml and optional extra files.
    /// GitHub archives have a top-level directory like {repo}-{ref}/.
    /// </summary>
    private static byte[] CreatePackArchive(
        string topLevelDir,
        string packYamlContent = "name: test-pack\nversion: 1.0.0\nminSteergenVersion: 1.0.0\n",
        Dictionary<string, string>? extraFiles = null,
        string? subDirectory = null)
    {
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
        using (var tarWriter = new TarWriter(gzipStream, leaveOpen: true))
        {
            var prefix = topLevelDir.TrimEnd('/') + "/";
            var contentPrefix = subDirectory is not null
                ? prefix + subDirectory.TrimEnd('/') + "/"
                : prefix;

            // Write pack.yaml
            var packYamlEntry = new PaxTarEntry(TarEntryType.RegularFile, contentPrefix + "pack.yaml")
            {
                DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(packYamlContent))
            };
            tarWriter.WriteEntry(packYamlEntry);

            // Write extra files
            if (extraFiles is not null)
            {
                foreach (var (relativePath, content) in extraFiles)
                {
                    var entry = new PaxTarEntry(TarEntryType.RegularFile, contentPrefix + relativePath)
                    {
                        DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content))
                    };
                    tarWriter.WriteEntry(entry);
                }
            }
        }

        return memoryStream.ToArray();
    }

    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, byte[]? content = null)
    {
        var handler = new FakeHttpMessageHandler(statusCode, content);
        return new HttpClient(handler);
    }

    // ── Success scenarios ────────────────────────────────────────────────────

    [Fact]
    public async Task DownloadAsync_SuccessfulDownload_ExtractsToCache()
    {
        // Requirement 4.4: Pack downloaded and stored in cache
        var archive = CreatePackArchive("repo-v1.0.0");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, archive);
        var downloader = new PackDownloader(httpClient, _cacheBase);

        var source = new GitHubPackSource { Owner = "acme", Repo = "templates", Ref = "v1.0.0" };
        var result = await downloader.DownloadAsync(source, PackType.Template, force: false);

        Assert.True(result.Success);
        Assert.NotNull(result.CachePath);
        Assert.True(Directory.Exists(result.CachePath));
        Assert.True(File.Exists(Path.Combine(result.CachePath, "pack.yaml")));
    }

    [Fact]
    public async Task DownloadAsync_SuccessfulDownload_CachePathMatchesExpectedFormat()
    {
        // Requirement 4.4: Cache path is {cacheBase}/packs/{owner}/{repo}/{ref}/
        var archive = CreatePackArchive("templates-v2.0.0");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, archive);
        var downloader = new PackDownloader(httpClient, _cacheBase);

        var source = new GitHubPackSource { Owner = "org", Repo = "templates", Ref = "v2.0.0" };
        var result = await downloader.DownloadAsync(source, PackType.Template, force: false);

        var expectedPath = Path.Combine(_cacheBase, "packs", "org", "templates", "v2.0.0") + Path.DirectorySeparatorChar;
        Assert.Equal(expectedPath, result.CachePath);
    }

    [Fact]
    public async Task DownloadAsync_RulesPackType_UseRulesSubdirectory()
    {
        // Requirement 9.5: Rules packs use "rules" subdirectory in cache
        var archive = CreatePackArchive("rules-v1.0.0",
            packYamlContent: "name: test-rules\nversion: 1.0.0\nminSteergenVersion: 1.0.0\nscope: global\n");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, archive);
        var downloader = new PackDownloader(httpClient, _cacheBase);

        var source = new GitHubPackSource { Owner = "acme", Repo = "rules", Ref = "v1.0.0" };
        var result = await downloader.DownloadAsync(source, PackType.Rules, force: false);

        var expectedPath = Path.Combine(_cacheBase, "rules", "acme", "rules", "v1.0.0") + Path.DirectorySeparatorChar;
        Assert.Equal(expectedPath, result.CachePath);
    }

    // ── HTTP error scenarios ─────────────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task DownloadAsync_HttpError_ProducesDL001DiagnosticWithStatusAndUrl(HttpStatusCode statusCode)
    {
        // Requirement 3.5: HTTP error responses produce DL001 diagnostic with HTTP status code and repository URL
        var httpClient = CreateHttpClient(statusCode);
        var downloader = new PackDownloader(httpClient, _cacheBase);

        var source = new GitHubPackSource { Owner = "acme", Repo = "templates", Ref = "v1.0.0" };
        var result = await downloader.DownloadAsync(source, PackType.Template, force: false);

        Assert.False(result.Success);
        Assert.Null(result.CachePath);
        Assert.Single(result.Diagnostics);

        var diagnostic = result.Diagnostics[0];
        Assert.Equal("DL001", diagnostic.Code);
        Assert.Contains($"HTTP {(int)statusCode}", diagnostic.Message);
        Assert.Contains("https://github.com/acme/templates/archive/v1.0.0.tar.gz", diagnostic.Message);
    }

    [Fact]
    public async Task DownloadAsync_HttpRequestException_ProducesDL001Diagnostic()
    {
        // Requirement 3.5: Network failures produce DL001 diagnostic
        var handler = new FakeHttpMessageHandler(throwException: true);
        var httpClient = new HttpClient(handler);
        var downloader = new PackDownloader(httpClient, _cacheBase);

        var source = new GitHubPackSource { Owner = "acme", Repo = "templates", Ref = "v1.0.0" };
        var result = await downloader.DownloadAsync(source, PackType.Template, force: false);

        Assert.False(result.Success);
        Assert.Single(result.Diagnostics);
        Assert.Equal("DL001", result.Diagnostics[0].Code);
        Assert.Contains("Failed to download pack", result.Diagnostics[0].Message);
    }

    // ── Immutable pin skip logic ─────────────────────────────────────────────

    [Fact]
    public async Task DownloadAsync_ImmutablePinWithExistingCache_SkipsDownload()
    {
        // Requirement 3.6: Immutable pin (40-char SHA) skips re-download when cache exists
        var sha = "abc123def456789012345678901234567890abcd";
        var source = new GitHubPackSource { Owner = "acme", Repo = "templates", Ref = sha };

        // Pre-create the cache directory to simulate existing cache
        var downloader = new PackDownloader(CreateHttpClient(HttpStatusCode.OK), _cacheBase);
        var cachePath = downloader.GetCachedPath(source, PackType.Template);
        Directory.CreateDirectory(cachePath);

        // Use a handler that would fail if called — proving no HTTP request is made
        var failHandler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(failHandler);
        downloader = new PackDownloader(httpClient, _cacheBase);

        var result = await downloader.DownloadAsync(source, PackType.Template, force: false);

        Assert.True(result.Success);
        Assert.Equal(cachePath, result.CachePath);
        Assert.False(failHandler.WasCalled);
    }

    [Fact]
    public async Task DownloadAsync_ImmutablePinWithForce_RedownloadsEvenWhenCached()
    {
        // Requirement 4.8: --force overrides immutable pin skip
        var sha = "abc123def456789012345678901234567890abcd";
        var source = new GitHubPackSource { Owner = "acme", Repo = "templates", Ref = sha };

        // Pre-create the cache directory
        var downloader = new PackDownloader(CreateHttpClient(HttpStatusCode.OK), _cacheBase);
        var cachePath = downloader.GetCachedPath(source, PackType.Template);
        Directory.CreateDirectory(cachePath);

        // Create a valid archive for the forced download
        var archive = CreatePackArchive("templates-" + sha);
        var httpClient = CreateHttpClient(HttpStatusCode.OK, archive);
        downloader = new PackDownloader(httpClient, _cacheBase);

        var result = await downloader.DownloadAsync(source, PackType.Template, force: true);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(result.CachePath!, "pack.yaml")));
    }

    [Fact]
    public async Task DownloadAsync_NonImmutableRef_AlwaysDownloads()
    {
        // Requirement 3.3: Non-SHA refs (branches/tags) always download
        var source = new GitHubPackSource { Owner = "acme", Repo = "templates", Ref = "main" };

        // Pre-create the cache directory
        var downloader = new PackDownloader(CreateHttpClient(HttpStatusCode.OK), _cacheBase);
        var cachePath = downloader.GetCachedPath(source, PackType.Template);
        Directory.CreateDirectory(cachePath);

        // Create a valid archive
        var archive = CreatePackArchive("templates-main");
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, archive);
        var httpClient = new HttpClient(handler);
        downloader = new PackDownloader(httpClient, _cacheBase);

        var result = await downloader.DownloadAsync(source, PackType.Template, force: false);

        Assert.True(result.Success);
        Assert.True(handler.WasCalled);
    }

    // ── Default-branch resolution ────────────────────────────────────────────

    [Fact]
    public async Task DownloadAsync_NullRef_UsesHEADInArchiveUrl()
    {
        // Requirement 3.3: When ref is null, archive URL uses HEAD
        var archive = CreatePackArchive("templates-HEAD");
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, archive);
        var httpClient = new HttpClient(handler);
        var downloader = new PackDownloader(httpClient, _cacheBase);

        var source = new GitHubPackSource { Owner = "acme", Repo = "templates", Ref = null };
        var result = await downloader.DownloadAsync(source, PackType.Template, force: false);

        Assert.True(result.Success);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal("https://github.com/acme/templates/archive/HEAD.tar.gz", handler.LastRequestUri.ToString());
    }

    // ── Subdirectory extraction ──────────────────────────────────────────────

    [Fact]
    public async Task DownloadAsync_WithPath_ExtractsOnlySubdirectoryContents()
    {
        // Requirement 9.5: When path is specified, only that subdirectory's contents are cached
        var archive = CreatePackArchive(
            "monorepo-v1.0.0",
            subDirectory: "backend-team",
            extraFiles: new Dictionary<string, string>
            {
                ["rules/governance.md"] = "# Governance rules"
            });

        // Also add a file outside the subdirectory that should NOT be extracted
        // We'll create a more complex archive for this
        var archiveWithExtraRoot = CreateArchiveWithMultipleSubdirs(
            "monorepo-v1.0.0",
            subDirs: new Dictionary<string, Dictionary<string, string>>
            {
                ["backend-team"] = new()
                {
                    ["pack.yaml"] = "name: backend-rules\nversion: 1.0.0\nminSteergenVersion: 1.0.0\nscope: global\n",
                    ["rules/governance.md"] = "# Backend governance"
                },
                ["frontend-team"] = new()
                {
                    ["pack.yaml"] = "name: frontend-rules\nversion: 1.0.0\nminSteergenVersion: 1.0.0\nscope: global\n",
                    ["rules/ui.md"] = "# Frontend rules"
                }
            });

        var httpClient = CreateHttpClient(HttpStatusCode.OK, archiveWithExtraRoot);
        var downloader = new PackDownloader(httpClient, _cacheBase);

        var source = new GitHubPackSource { Owner = "acme", Repo = "monorepo", Ref = "v1.0.0", Path = "backend-team" };
        var result = await downloader.DownloadAsync(source, PackType.Rules, force: false);

        Assert.True(result.Success);
        Assert.NotNull(result.CachePath);

        // Should have pack.yaml from the backend-team subdirectory
        Assert.True(File.Exists(Path.Combine(result.CachePath, "pack.yaml")));
        Assert.True(File.Exists(Path.Combine(result.CachePath, "rules", "governance.md")));

        // Should NOT have frontend-team files
        Assert.False(File.Exists(Path.Combine(result.CachePath, "frontend-team", "pack.yaml")));
        Assert.False(Directory.Exists(Path.Combine(result.CachePath, "frontend-team")));
    }

    // ── Atomic replacement ───────────────────────────────────────────────────

    [Fact]
    public async Task DownloadAsync_ExistingCache_ReplacesAtomically()
    {
        // Requirement 4.8: Atomic replacement — existing cache is replaced on success
        var source = new GitHubPackSource { Owner = "acme", Repo = "templates", Ref = "v1.0.0" };

        // Pre-create cache with old content
        var downloader = new PackDownloader(CreateHttpClient(HttpStatusCode.OK), _cacheBase);
        var cachePath = downloader.GetCachedPath(source, PackType.Template);
        Directory.CreateDirectory(cachePath);
        File.WriteAllText(Path.Combine(cachePath, "old-file.txt"), "old content");

        // Download new version
        var archive = CreatePackArchive("templates-v1.0.0",
            extraFiles: new Dictionary<string, string>
            {
                ["kiro/main.scriban"] = "{{ new_content }}"
            });
        var httpClient = CreateHttpClient(HttpStatusCode.OK, archive);
        downloader = new PackDownloader(httpClient, _cacheBase);

        var result = await downloader.DownloadAsync(source, PackType.Template, force: false);

        Assert.True(result.Success);
        // Old file should be gone (replaced)
        Assert.False(File.Exists(Path.Combine(cachePath, "old-file.txt")));
        // New files should be present
        Assert.True(File.Exists(Path.Combine(cachePath, "pack.yaml")));
        Assert.True(File.Exists(Path.Combine(cachePath, "kiro", "main.scriban")));
    }

    [Fact]
    public async Task DownloadAsync_FailedDownload_PreservesExistingCache()
    {
        // Requirement 4.8: Existing cache is preserved on download failure
        var source = new GitHubPackSource { Owner = "acme", Repo = "templates", Ref = "v1.0.0" };

        // Pre-create cache with existing content
        var downloader = new PackDownloader(CreateHttpClient(HttpStatusCode.OK), _cacheBase);
        var cachePath = downloader.GetCachedPath(source, PackType.Template);
        Directory.CreateDirectory(cachePath);
        File.WriteAllText(Path.Combine(cachePath, "existing.txt"), "preserved content");

        // Attempt download that fails (HTTP 404)
        var httpClient = CreateHttpClient(HttpStatusCode.NotFound);
        downloader = new PackDownloader(httpClient, _cacheBase);

        var result = await downloader.DownloadAsync(source, PackType.Template, force: false);

        Assert.False(result.Success);
        // Existing cache should still be intact
        Assert.True(File.Exists(Path.Combine(cachePath, "existing.txt")));
        Assert.Equal("preserved content", File.ReadAllText(Path.Combine(cachePath, "existing.txt")));
    }

    // ── Missing pack.yaml ────────────────────────────────────────────────────

    [Fact]
    public async Task DownloadAsync_MissingPackYaml_ProducesDL002Diagnostic()
    {
        // Requirement 4.6: Archive without pack.yaml is rejected
        var archiveWithoutManifest = CreateArchiveWithoutPackYaml("repo-v1.0.0");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, archiveWithoutManifest);
        var downloader = new PackDownloader(httpClient, _cacheBase);

        var source = new GitHubPackSource { Owner = "acme", Repo = "templates", Ref = "v1.0.0" };
        var result = await downloader.DownloadAsync(source, PackType.Template, force: false);

        Assert.False(result.Success);
        Assert.Single(result.Diagnostics);
        Assert.Equal("DL002", result.Diagnostics[0].Code);
        Assert.Contains("pack.yaml", result.Diagnostics[0].Message);
    }

    [Fact]
    public async Task DownloadAsync_MetadataEntryBeforeTopLevelDirectory_StillFindsPackYaml()
    {
        var archive = CreateArchiveWithLeadingMetadataEntry("repo-v1.0.0");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, archive);
        var downloader = new PackDownloader(httpClient, _cacheBase);

        var source = new GitHubPackSource { Owner = "acme", Repo = "templates", Ref = "v1.0.0" };
        var result = await downloader.DownloadAsync(source, PackType.Template, force: false);

        Assert.True(result.Success);
        Assert.NotNull(result.CachePath);
        Assert.True(File.Exists(Path.Combine(result.CachePath, "pack.yaml")));
        Assert.False(File.Exists(Path.Combine(result.CachePath, "repo-v1.0.0", "pack.yaml")));
    }

    // ── Additional archive helpers ───────────────────────────────────────────

    private static byte[] CreateArchiveWithMultipleSubdirs(
        string topLevelDir,
        Dictionary<string, Dictionary<string, string>> subDirs)
    {
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
        using (var tarWriter = new TarWriter(gzipStream, leaveOpen: true))
        {
            var prefix = topLevelDir.TrimEnd('/') + "/";

            foreach (var (subDir, files) in subDirs)
            {
                var subPrefix = prefix + subDir.TrimEnd('/') + "/";
                foreach (var (relativePath, content) in files)
                {
                    var entry = new PaxTarEntry(TarEntryType.RegularFile, subPrefix + relativePath)
                    {
                        DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content))
                    };
                    tarWriter.WriteEntry(entry);
                }
            }
        }

        return memoryStream.ToArray();
    }

    private static byte[] CreateArchiveWithoutPackYaml(string topLevelDir)
    {
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
        using (var tarWriter = new TarWriter(gzipStream, leaveOpen: true))
        {
            var prefix = topLevelDir.TrimEnd('/') + "/";
            var entry = new PaxTarEntry(TarEntryType.RegularFile, prefix + "README.md")
            {
                DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("# No pack.yaml here"))
            };
            tarWriter.WriteEntry(entry);
        }

        return memoryStream.ToArray();
    }

    private static byte[] CreateArchiveWithLeadingMetadataEntry(string topLevelDir)
    {
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
        using (var tarWriter = new TarWriter(gzipStream, leaveOpen: true))
        {
            // Simulate an entry that appears before the top-level directory prefix.
            var metadataEntry = new PaxTarEntry(TarEntryType.RegularFile, "pax_global_header")
            {
                DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("comment=generated-by-test\n"))
            };
            tarWriter.WriteEntry(metadataEntry);

            var prefix = topLevelDir.TrimEnd('/') + "/";
            var packYamlEntry = new PaxTarEntry(TarEntryType.RegularFile, prefix + "pack.yaml")
            {
                DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("name: test-pack\nversion: 1.0.0\nminSteergenVersion: 1.0.0\n"))
            };
            tarWriter.WriteEntry(packYamlEntry);
        }

        return memoryStream.ToArray();
    }

    // ── Fake HTTP handler ────────────────────────────────────────────────────

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly byte[]? _content;
        private readonly bool _throwException;

        public bool WasCalled { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        public FakeHttpMessageHandler(HttpStatusCode statusCode, byte[]? content = null)
        {
            _statusCode = statusCode;
            _content = content;
        }

        public FakeHttpMessageHandler(bool throwException)
        {
            _throwException = throwException;
            _statusCode = HttpStatusCode.OK;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            LastRequestUri = request.RequestUri;

            if (_throwException)
                throw new HttpRequestException("Network error: connection refused");

            var response = new HttpResponseMessage(_statusCode);
            if (_content is not null)
            {
                response.Content = new ByteArrayContent(_content);
            }

            return Task.FromResult(response);
        }
    }
}
