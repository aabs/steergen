using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using CsCheck;
using Steergen.Core.Packs;
using Steergen.Core.Validation;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for multi-pack repository behavior.
/// Validates that installing multiple packs from the same source/ref with different
/// paths never erases sibling paths from cache, and that rules loading remains
/// scoped to each configured path.
/// </summary>
public sealed class MultiPackArchiveProperties
{
    private static readonly Gen<string> GenPathSegment =
        Gen.String[Gen.Char['a', 'z'], 3, 10]
            .Select(s => $"team-{s}");

    [Fact]
    public void SequentialDownloads_SameRepoRefDifferentPaths_PreserveAllSubpaths()
    {
        GenPathSegment.Array[2, 4]
            .Where(paths => paths.Distinct(StringComparer.Ordinal).Count() == paths.Length)
            .Sample(
                paths =>
                {
                    var cacheBase = Path.Combine(Path.GetTempPath(), "MultiPackProps_Download_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(cacheBase);

                    try
                    {
                        var owner = "acme";
                        var repo = "shared-rules";
                        var refValue = "v1.0.0";

                        var archive = CreateArchiveWithSubdirs(
                            topLevelDir: $"{repo}-{refValue}",
                            subDirs: paths.ToDictionary(
                                p => p,
                                p => new Dictionary<string, string>
                                {
                                    ["pack.yaml"] = BuildPackManifest($"{p}-pack", "global"),
                                    ["rules.md"] = BuildRulesDoc($"doc-{p}", BuildRuleId(p), $"rule-from-{p}")
                                }));

                        var downloader = new PackDownloader(CreateHttpClient(HttpStatusCode.OK, archive), cacheBase);

                        foreach (var path in paths)
                        {
                            var result = downloader.DownloadAsync(
                                new GitHubPackSource
                                {
                                    Owner = owner,
                                    Repo = repo,
                                    Ref = refValue,
                                    Path = path
                                },
                                PackType.Rules,
                                force: false).GetAwaiter().GetResult();

                            Assert.True(result.Success);
                            Assert.NotNull(result.CachePath);

                            // The regression check: previously, this could erase sibling paths.
                            foreach (var expectedPath in paths)
                            {
                                Assert.True(File.Exists(Path.Combine(result.CachePath!, expectedPath, "pack.yaml")));
                                Assert.True(File.Exists(Path.Combine(result.CachePath!, expectedPath, "rules.md")));
                            }
                        }
                    }
                    finally
                    {
                        if (Directory.Exists(cacheBase))
                            Directory.Delete(cacheBase, recursive: true);
                    }
                },
                iter: 80,
                print: paths => $"paths=[{string.Join(",", paths)}]");
    }

    [Fact]
    public void RulesPackLoader_WithMultipleConfiguredPaths_LoadsOnlyEachConfiguredPackRoot()
    {
        GenPathSegment.Array[2, 4]
            .Where(paths => paths.Distinct(StringComparer.Ordinal).Count() == paths.Length)
            .Sample(
                paths =>
                {
                    var cacheBase = Path.Combine(Path.GetTempPath(), "MultiPackProps_Load_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(cacheBase);

                    try
                    {
                        var owner = "acme";
                        var repo = "shared-rules";
                        var refValue = "v1.0.0";
                        var cacheRoot = Path.Combine(cacheBase, "rules", owner, repo, refValue);
                        Directory.CreateDirectory(cacheRoot);

                        // Add a root-level manifest/doc that should not be used when path is configured.
                        File.WriteAllText(Path.Combine(cacheRoot, "pack.yaml"), BuildPackManifest("root-pack", "global"));
                        File.WriteAllText(Path.Combine(cacheRoot, "root.md"), BuildRulesDoc("root-doc", "ROOT-001", "root"));

                        var expectedRuleIds = new HashSet<string>(StringComparer.Ordinal);
                        var packConfigs = new List<RulesPackConfiguration>();

                        foreach (var path in paths)
                        {
                            var subDir = Path.Combine(cacheRoot, path);
                            Directory.CreateDirectory(subDir);

                            var packName = $"{path}-pack";
                            var scope = (expectedRuleIds.Count % 2 == 0) ? "global" : "supplemental";
                            var ruleId = BuildRuleId(path);
                            expectedRuleIds.Add(ruleId);

                            File.WriteAllText(Path.Combine(subDir, "pack.yaml"), BuildPackManifest(packName, scope));
                            File.WriteAllText(Path.Combine(subDir, "rules.md"), BuildRulesDoc($"doc-{path}", ruleId, $"body-{path}"));

                            packConfigs.Add(new RulesPackConfiguration
                            {
                                Source = new GitHubPackSource
                                {
                                    Owner = owner,
                                    Repo = repo,
                                    Ref = refValue,
                                    Path = path
                                }
                            });
                        }

                        var loader = new RulesPackLoader(new PackManifestParser(), new SteeringValidator());
                        var result = loader.Load(packConfigs, cacheBase, "99.0.0");

                        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

                        var resolvedRuleIds = result.Documents
                            .SelectMany(d => d.Rules)
                            .Select(r => r.Id)
                            .Where(id => id is not null)
                            .Cast<string>()
                            .ToHashSet(StringComparer.Ordinal);

                        Assert.Equal(expectedRuleIds.Count, resolvedRuleIds.Count);
                        Assert.True(expectedRuleIds.SetEquals(resolvedRuleIds));
                        Assert.DoesNotContain("ROOT-001", resolvedRuleIds);
                    }
                    finally
                    {
                        if (Directory.Exists(cacheBase))
                            Directory.Delete(cacheBase, recursive: true);
                    }
                },
                iter: 80,
                print: paths => $"paths=[{string.Join(",", paths)}]");
    }

    private static string BuildRuleId(string path)
    {
        var normalized = path.Replace("team-", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        if (normalized.Length > 12)
            normalized = normalized[..12];

        return $"{normalized}-001";
    }

    private static string BuildPackManifest(string packName, string scope) =>
        $"name: {packName}\nversion: 1.0.0\nminSteergenVersion: 0.1.0\nscope: {scope}\n";

    private static string BuildRulesDoc(string docId, string ruleId, string body) =>
        $$"""
        ---
        id: {{docId}}
        version: "1.0.0"
        title: {{docId}}
        scope: global
        status: active
        ---

        # {{docId}}

        :::rule id="{{ruleId}}" mandatory="true" category="testing"
        {{body}}
        :::
        """;

    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, byte[] content)
    {
        return new HttpClient(new StaticArchiveMessageHandler(statusCode, content));
    }

    private static byte[] CreateArchiveWithSubdirs(
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

    private sealed class StaticArchiveMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly byte[] _content;

        public StaticArchiveMessageHandler(HttpStatusCode statusCode, byte[] content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new ByteArrayContent(_content)
            };
            return Task.FromResult(response);
        }
    }
}
