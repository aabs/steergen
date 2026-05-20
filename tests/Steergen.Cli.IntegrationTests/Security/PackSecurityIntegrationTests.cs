using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using Steergen.Core.Packs;
using Steergen.Core.Targets;

namespace Steergen.Cli.IntegrationTests.Security;

/// <summary>
/// Security integration tests for pack infrastructure.
/// Validates that path traversal in archives is rejected, template files exceeding
/// 1 MB are rejected, and symbolic links in pack directories are not followed.
///
/// Requirements: 14.2, 14.3, 14.4, 14.5
/// </summary>
[Collection("CliOutput")]
public sealed class PackSecurityIntegrationTests : IDisposable
{
    private readonly string _cacheBase;

    public PackSecurityIntegrationTests()
    {
        _cacheBase = Path.Combine(Path.GetTempPath(), $"PackSecurityTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_cacheBase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheBase))
            Directory.Delete(_cacheBase, recursive: true);
    }

    // ── Path traversal in archive entries ─────────────────────────────────────

    /// <summary>
    /// Requirement 14.3: Archives containing path traversal sequences (../) are rejected.
    /// </summary>
    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("templates/../../../etc/shadow")]
    [InlineData("..\\..\\Windows\\System32\\config\\SAM")]
    public async Task DownloadAsync_ArchiveWithPathTraversal_IsRejected(string maliciousPath)
    {
        var archive = CreateArchiveWithTraversalEntry("repo-v1.0.0", maliciousPath);
        var httpClient = CreateHttpClient(HttpStatusCode.OK, archive);
        var downloader = new PackDownloader(httpClient, _cacheBase);

        var source = new GitHubPackSource { Owner = "evil", Repo = "pack", Ref = "v1.0.0" };
        var result = await downloader.DownloadAsync(source, PackType.Template, force: false);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, d =>
            d.Code is "DL003" or "DL004");
    }

    /// <summary>
    /// Requirement 14.4: Archives with entries that resolve outside the pack directory are rejected.
    /// </summary>
    [Fact]
    public async Task DownloadAsync_ArchiveWithAbsolutePath_IsRejected()
    {
        var archive = CreateArchiveWithTraversalEntry("repo-v1.0.0", "/etc/passwd");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, archive);
        var downloader = new PackDownloader(httpClient, _cacheBase);

        var source = new GitHubPackSource { Owner = "evil", Repo = "pack", Ref = "v1.0.0" };
        var result = await downloader.DownloadAsync(source, PackType.Template, force: false);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, d =>
            d.Code is "DL003" or "DL004");
    }

    /// <summary>
    /// Requirement 14.3: The static IsPathSafe method rejects traversal sequences.
    /// </summary>
    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("foo/../../bar")]
    [InlineData("..\\..\\windows\\system32")]
    [InlineData("/absolute/path")]
    [InlineData("\\absolute\\path")]
    public void IsPathSafe_TraversalPaths_ReturnsFalse(string path)
    {
        Assert.False(PackDownloader.IsPathSafe(path));
    }

    /// <summary>
    /// Requirement 14.3: Safe paths within the pack directory are accepted.
    /// </summary>
    [Theory]
    [InlineData("pack.yaml")]
    [InlineData("kiro/main.scriban")]
    [InlineData("templates/speckit/rules.scriban")]
    public void IsPathSafe_ValidPaths_ReturnsTrue(string path)
    {
        Assert.True(PackDownloader.IsPathSafe(path));
    }

    // ── Template files > 1 MB are rejected ────────────────────────────────────

    /// <summary>
    /// Requirement 14.2: Template files exceeding 1 MB (1,048,576 bytes) are rejected
    /// by the TemplateResolver with a TP002 diagnostic.
    /// </summary>
    [Fact]
    public void TemplateResolver_FileExceeding1MB_ThrowsTemplatePackException()
    {
        var packDir = Path.Combine(_cacheBase, "oversized-pack");
        var targetDir = Path.Combine(packDir, "kiro");
        Directory.CreateDirectory(targetDir);

        // Create a template file that exceeds 1 MB
        var oversizedContent = new string('X', 1_048_577); // 1 MB + 1 byte
        File.WriteAllText(Path.Combine(targetDir, "main.scriban"), oversizedContent);

        var embeddedProvider = new StubTemplateProvider();
        var resolver = new TemplateResolver(
            localOverridePath: packDir,
            cachedPackPath: null,
            embeddedProvider: embeddedProvider);

        var ex = Assert.Throws<TemplatePackException>(() =>
            resolver.GetTemplate("kiro", "main"));

        Assert.Equal("TP002", ex.Diagnostic.Code);
        Assert.Contains("1048576", ex.Diagnostic.Message);
    }

    /// <summary>
    /// Requirement 14.2: Template files at exactly 1 MB are accepted.
    /// </summary>
    [Fact]
    public void TemplateResolver_FileAtExactly1MB_IsAccepted()
    {
        var packDir = Path.Combine(_cacheBase, "exact-1mb-pack");
        var targetDir = Path.Combine(packDir, "kiro");
        Directory.CreateDirectory(targetDir);

        // Create a template file at exactly 1 MB
        var content = new string('Y', 1_048_576); // Exactly 1 MB
        File.WriteAllText(Path.Combine(targetDir, "main.scriban"), content);

        var embeddedProvider = new StubTemplateProvider();
        var resolver = new TemplateResolver(
            localOverridePath: packDir,
            cachedPackPath: null,
            embeddedProvider: embeddedProvider);

        var result = resolver.GetTemplate("kiro", "main");

        Assert.Equal(content, result);
    }

    /// <summary>
    /// Requirement 14.7: Rules pack loader rejects individual files > 1 MB.
    /// Verified via the RulesPackLoader which checks file size before parsing.
    /// </summary>
    [Fact]
    public void RulesPackLoader_FileExceeding1MB_EmitsRP004Diagnostic()
    {
        // Set up a fake rules pack cache directory with an oversized file
        var packCacheDir = Path.Combine(_cacheBase, "rules", "acme", "big-rules", "v1.0.0");
        Directory.CreateDirectory(packCacheDir);

        // Write a valid pack.yaml
        File.WriteAllText(Path.Combine(packCacheDir, "pack.yaml"),
            "name: big-rules\nversion: 1.0.0\nminSteergenVersion: 0.1.0\nscope: global\n");

        // Write an oversized .md file (> 1 MB)
        var oversizedContent = "---\nid: oversized-doc\n---\n" + new string('Z', 1_048_577);
        File.WriteAllText(Path.Combine(packCacheDir, "oversized.md"), oversizedContent);

        var manifestParser = new PackManifestParser();
        var validator = new Core.Validation.SteeringValidator();
        var loader = new RulesPackLoader(manifestParser, validator);

        var configs = new List<RulesPackConfiguration>
        {
            new()
            {
                Source = new GitHubPackSource { Owner = "acme", Repo = "big-rules", Ref = "v1.0.0" }
            }
        };

        var result = loader.Load(configs, _cacheBase, "99.0.0");

        Assert.Contains(result.Diagnostics, d => d.Code == "RP004");
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("1 MB"));
    }

    // ── Symlinks in pack directories are not followed ─────────────────────────

    /// <summary>
    /// Requirement 14.5: Symbolic links in template pack directories are not followed.
    /// The TemplateResolver skips files that are symbolic links (ReparsePoint attribute).
    /// Note: Symlink creation may require elevated privileges on Windows.
    /// </summary>
    [Fact]
    public void TemplateResolver_SymlinkInPackDirectory_IsNotFollowed()
    {
        var packDir = Path.Combine(_cacheBase, "symlink-pack");
        var targetDir = Path.Combine(packDir, "kiro");
        Directory.CreateDirectory(targetDir);

        // Create a real file outside the pack directory that the symlink will point to
        var secretFile = Path.Combine(_cacheBase, "secret.txt");
        File.WriteAllText(secretFile, "SECRET CONTENT SHOULD NOT BE ACCESSIBLE");

        var symlinkPath = Path.Combine(targetDir, "main.scriban");

        // Attempt to create a symbolic link — skip test if insufficient privileges
        try
        {
            File.CreateSymbolicLink(symlinkPath, secretFile);
        }
        catch (UnauthorizedAccessException)
        {
            // Symlink creation requires elevated privileges on Windows
            return; // Skip test gracefully
        }
        catch (IOException)
        {
            // May also fail with IOException on some systems
            return; // Skip test gracefully
        }

        // Verify the symlink was actually created
        if (!File.Exists(symlinkPath))
            return; // Skip if symlink creation silently failed

        var embeddedProvider = new StubTemplateProvider("FALLBACK CONTENT");
        var resolver = new TemplateResolver(
            localOverridePath: packDir,
            cachedPackPath: null,
            embeddedProvider: embeddedProvider);

        // The resolver should NOT follow the symlink and should fall back to embedded
        var result = resolver.GetTemplate("kiro", "main");

        Assert.Equal("FALLBACK CONTENT", result);
        Assert.NotEqual("SECRET CONTENT SHOULD NOT BE ACCESSIBLE", result);
    }

    /// <summary>
    /// Requirement 14.8: Symbolic links in rules pack directories are not followed.
    /// The RulesPackLoader skips symlinked files and directories during enumeration.
    /// Note: Symlink creation may require elevated privileges on Windows.
    /// </summary>
    [Fact]
    public void RulesPackLoader_SymlinkInPackDirectory_IsNotFollowed()
    {
        var packCacheDir = Path.Combine(_cacheBase, "rules", "acme", "symlink-rules", "v1.0.0");
        Directory.CreateDirectory(packCacheDir);

        // Write a valid pack.yaml
        File.WriteAllText(Path.Combine(packCacheDir, "pack.yaml"),
            "name: symlink-rules\nversion: 1.0.0\nminSteergenVersion: 0.1.0\nscope: global\n");

        // Create a real .md file outside the pack directory
        var externalDir = Path.Combine(_cacheBase, "external-rules");
        Directory.CreateDirectory(externalDir);
        File.WriteAllText(Path.Combine(externalDir, "secret-rule.md"),
            "---\nid: secret-doc\n---\n:::rule id=\"SECRET-001\" severity=\"error\" domain=\"core\"\nSecret rule.\n:::");

        // Attempt to create a directory symlink pointing to the external directory
        var symlinkDir = Path.Combine(packCacheDir, "linked-rules");
        try
        {
            Directory.CreateSymbolicLink(symlinkDir, externalDir);
        }
        catch (UnauthorizedAccessException)
        {
            return; // Skip test gracefully
        }
        catch (IOException)
        {
            return; // Skip test gracefully
        }

        if (!Directory.Exists(symlinkDir))
            return; // Skip if symlink creation silently failed

        var manifestParser = new PackManifestParser();
        var validator = new Core.Validation.SteeringValidator();
        var loader = new RulesPackLoader(manifestParser, validator);

        var configs = new List<RulesPackConfiguration>
        {
            new()
            {
                Source = new GitHubPackSource { Owner = "acme", Repo = "symlink-rules", Ref = "v1.0.0" }
            }
        };

        var result = loader.Load(configs, _cacheBase, "99.0.0");

        // The loader should NOT have followed the symlink, so no documents from the external dir
        Assert.DoesNotContain(result.Documents, d =>
            d.Rules.Any(r => r.Id == "SECRET-001"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a tar.gz archive containing a path traversal entry.
    /// </summary>
    private static byte[] CreateArchiveWithTraversalEntry(string topLevelDir, string maliciousPath)
    {
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
        using (var tarWriter = new TarWriter(gzipStream, leaveOpen: true))
        {
            var prefix = topLevelDir.TrimEnd('/') + "/";

            // Write a valid pack.yaml first
            var packYamlEntry = new PaxTarEntry(TarEntryType.RegularFile, prefix + "pack.yaml")
            {
                DataStream = new MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes("name: evil-pack\nversion: 1.0.0\nminSteergenVersion: 1.0.0\n"))
            };
            tarWriter.WriteEntry(packYamlEntry);

            // Write the malicious path traversal entry
            var maliciousEntry = new PaxTarEntry(TarEntryType.RegularFile, prefix + maliciousPath)
            {
                DataStream = new MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes("MALICIOUS CONTENT"))
            };
            tarWriter.WriteEntry(maliciousEntry);
        }

        return memoryStream.ToArray();
    }

    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, byte[]? content = null)
    {
        var handler = new FakeHttpMessageHandler(statusCode, content);
        return new HttpClient(handler);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly byte[]? _content;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, byte[]? content = null)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode);
            if (_content is not null)
            {
                response.Content = new ByteArrayContent(_content);
            }
            return Task.FromResult(response);
        }
    }

    private sealed class StubTemplateProvider : ITemplateProvider
    {
        private readonly string _fallbackContent;

        public StubTemplateProvider(string fallbackContent = "")
        {
            _fallbackContent = fallbackContent;
        }

        public string GetTemplate(string targetId, string templateName) => _fallbackContent;
    }
}
