using CsCheck;
using Steergen.Core.Targets;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for file size limit enforcement in TemplateResolver.
///
/// Property 12: File Size Limit Enforcement
/// For any file presented to the template resolver or rules pack loader, the file
/// SHALL be rejected with a diagnostic error if its size exceeds 1,048,576 bytes (1 MB),
/// and accepted for processing if its size is at or below that threshold.
///
/// **Validates: Requirements 14.2, 14.7**
/// </summary>
public sealed class FileSizeLimitProperties : IDisposable
{
    private const long MaxFileSizeBytes = 1_048_576;
    private const string EmbeddedSentinel = "__EMBEDDED_FALLBACK__";
    private const string TargetId = "test-target";
    private const string TemplateName = "document";

    private readonly string _tempDir;

    public FileSizeLimitProperties()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"steergen-pbt-filesize-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Generators ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates file sizes that are at or below the 1 MB boundary (accepted range).
    /// Focuses on sizes near the boundary for maximum coverage.
    /// </summary>
    private static readonly Gen<long> GenAcceptedSize =
        Gen.OneOf(
            Gen.Long[1, 100],                                   // Small files
            Gen.Long[MaxFileSizeBytes - 100, MaxFileSizeBytes]  // Near boundary (at or below)
        );

    /// <summary>
    /// Generates file sizes that exceed the 1 MB boundary (rejected range).
    /// Focuses on sizes just above the boundary.
    /// </summary>
    private static readonly Gen<long> GenRejectedSize =
        Gen.OneOf(
            Gen.Long[MaxFileSizeBytes + 1, MaxFileSizeBytes + 100],  // Just above boundary
            Gen.Long[MaxFileSizeBytes + 1, MaxFileSizeBytes + 1024]  // Slightly larger
        );

    // ── Property: files at or below 1 MB are accepted ────────────────────────────

    [Fact]
    public void FilesAtOrBelowLimit_AreAccepted()
    {
        // **Validates: Requirements 14.2, 14.7**
        GenAcceptedSize
            .Sample(
                fileSize =>
                {
                    var (resolver, expectedContent) = CreateResolverWithFile(fileSize);
                    var result = resolver.GetTemplate(TargetId, TemplateName);

                    Assert.Equal(expectedContent, result);
                },
                iter: 100,
                print: size => $"fileSize={size} bytes");
    }

    // ── Property: files exceeding 1 MB are rejected with exception ───────────────

    [Fact]
    public void FilesExceedingLimit_AreRejectedWithException()
    {
        // **Validates: Requirements 14.2, 14.7**
        GenRejectedSize
            .Sample(
                fileSize =>
                {
                    var (resolver, _) = CreateResolverWithFile(fileSize);

                    var ex = Assert.Throws<TemplatePackException>(
                        () => resolver.GetTemplate(TargetId, TemplateName));

                    Assert.Equal("TP002", ex.Diagnostic.Code);
                    Assert.Equal(2, ex.ExitCode);
                },
                iter: 100,
                print: size => $"fileSize={size} bytes");
    }

    // ── Property: exact boundary (1,048,576 bytes) is accepted ───────────────────

    [Fact]
    public void FileAtExactBoundary_IsAccepted()
    {
        // **Validates: Requirements 14.2, 14.7**
        var (resolver, expectedContent) = CreateResolverWithFile(MaxFileSizeBytes);
        var result = resolver.GetTemplate(TargetId, TemplateName);

        Assert.Equal(expectedContent, result);
    }

    // ── Property: one byte over boundary is rejected ─────────────────────────────

    [Fact]
    public void FileOneByteOverBoundary_IsRejected()
    {
        // **Validates: Requirements 14.2, 14.7**
        var (resolver, _) = CreateResolverWithFile(MaxFileSizeBytes + 1);

        var ex = Assert.Throws<TemplatePackException>(
            () => resolver.GetTemplate(TargetId, TemplateName));

        Assert.Equal("TP002", ex.Diagnostic.Code);
        Assert.Equal(2, ex.ExitCode);
    }

    // ── Property: size limit applies consistently across both override layers ────

    [Fact]
    public void SizeLimitApplies_ToBothLocalAndCachedLayers()
    {
        // **Validates: Requirements 14.2, 14.7**
        // When local override has an oversized file, it throws immediately
        // (does not fall through to cached layer)
        GenRejectedSize
            .Sample(
                fileSize =>
                {
                    var localDir = Path.Combine(_tempDir, $"local-{Guid.NewGuid():N}");
                    var cachedDir = Path.Combine(_tempDir, $"cached-{Guid.NewGuid():N}");

                    WriteTemplateFile(localDir, fileSize);
                    WriteTemplateFile(cachedDir, fileSize);

                    var resolver = new TemplateResolver(
                        localOverridePath: localDir,
                        cachedPackPath: cachedDir,
                        embeddedProvider: new SentinelTemplateProvider(),
                        declaredTargets: null,
                        maxFileSizeBytes: MaxFileSizeBytes);

                    var ex = Assert.Throws<TemplatePackException>(
                        () => resolver.GetTemplate(TargetId, TemplateName));

                    Assert.Equal("TP002", ex.Diagnostic.Code);
                },
                iter: 100,
                print: size => $"fileSize={size} bytes");
    }

    // ── Property: oversized file in cached layer also throws ─────────────────────

    [Fact]
    public void OversizedFileInCachedLayer_AlsoThrows()
    {
        // **Validates: Requirements 14.2, 14.7**
        // When local override has no file but cached layer has an oversized file,
        // the resolver throws when it encounters the oversized cached file
        GenRejectedSize
            .Sample(
                fileSize =>
                {
                    var localDir = Path.Combine(_tempDir, $"local-{Guid.NewGuid():N}");
                    var cachedDir = Path.Combine(_tempDir, $"cached-{Guid.NewGuid():N}");

                    // Local dir exists but has no template file for this target
                    Directory.CreateDirectory(localDir);
                    // Cached dir has the oversized file
                    WriteTemplateFile(cachedDir, fileSize);

                    var resolver = new TemplateResolver(
                        localOverridePath: localDir,
                        cachedPackPath: cachedDir,
                        embeddedProvider: new SentinelTemplateProvider(),
                        declaredTargets: null,
                        maxFileSizeBytes: MaxFileSizeBytes);

                    var ex = Assert.Throws<TemplatePackException>(
                        () => resolver.GetTemplate(TargetId, TemplateName));

                    Assert.Equal("TP002", ex.Diagnostic.Code);
                    Assert.Equal(2, ex.ExitCode);
                },
                iter: 100,
                print: size => $"fileSize={size} bytes");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private (TemplateResolver Resolver, string ExpectedContent) CreateResolverWithFile(long fileSize)
    {
        var subDir = Path.Combine(_tempDir, $"pack-{Guid.NewGuid():N}");
        var content = WriteTemplateFile(subDir, fileSize);

        var resolver = new TemplateResolver(
            localOverridePath: subDir,
            cachedPackPath: null,
            embeddedProvider: new SentinelTemplateProvider(),
            declaredTargets: null,
            maxFileSizeBytes: MaxFileSizeBytes);

        return (resolver, content);
    }

    private static string WriteTemplateFile(string packDir, long fileSize)
    {
        var targetDir = Path.Combine(packDir, TargetId);
        Directory.CreateDirectory(targetDir);

        var filePath = Path.Combine(targetDir, $"{TemplateName}.scriban");

        // Write a file of exactly the specified size using ASCII 'x' characters
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        var buffer = new byte[Math.Min(fileSize, 8192)];
        Array.Fill(buffer, (byte)'x');

        var remaining = fileSize;
        while (remaining > 0)
        {
            var toWrite = (int)Math.Min(remaining, buffer.Length);
            fs.Write(buffer, 0, toWrite);
            remaining -= toWrite;
        }

        // Return the expected content (all 'x' characters of the given size)
        return new string('x', (int)fileSize);
    }

    private sealed class SentinelTemplateProvider : ITemplateProvider
    {
        public string GetTemplate(string targetId, string templateName) => EmbeddedSentinel;
    }
}
