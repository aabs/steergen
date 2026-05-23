using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]
public sealed class MultiPackArchiveRegressionTests
{
    [Fact]
    public async Task SequentialInstalls_SameRepoRefDifferentPaths_PreserveAllCachedSubpaths_AndRunLoadsBoth()
    {
        var workspaceDir = Path.Combine(Path.GetTempPath(), "steergen-multipack-regr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceDir);

        var owner = "it-multipack-owner-" + Guid.NewGuid().ToString("N")[..8];
        var repo = "it-multipack-repo-" + Guid.NewGuid().ToString("N")[..8];
        var refValue = "v1.0.0";
        var backendPath = "backend-team";
        var frontendPath = "frontend-team";

        var cacheBase = GetCacheBaseDirectory();

        try
        {
            var archive = CreateArchiveWithMultipleSubdirs(
                topLevelDir: $"{repo}-{refValue}",
                subDirs: new Dictionary<string, Dictionary<string, string>>
                {
                    [backendPath] = new()
                    {
                        ["pack.yaml"] = "name: backend-rules\nversion: 1.0.0\nminSteergenVersion: 0.1.0\nscope: global\n",
                        ["backend-rules.md"] = BuildRulesDoc("backend-doc", "BACKEND-001", "Backend policy from multi-pack")
                    },
                    [frontendPath] = new()
                    {
                        ["pack.yaml"] = "name: frontend-rules\nversion: 1.0.0\nminSteergenVersion: 0.1.0\nscope: supplemental\n",
                        ["frontend-rules.md"] = BuildRulesDoc("frontend-doc", "FRONTEND-001", "Frontend policy from multi-pack")
                    }
                });

            var httpClient = CreateHttpClient(HttpStatusCode.OK, archive);
            var downloader = new PackDownloader(httpClient, cacheBase);

            var backendSource = new GitHubPackSource
            {
                Owner = owner,
                Repo = repo,
                Ref = refValue,
                Path = backendPath
            };
            var firstInstall = await downloader.DownloadAsync(backendSource, PackType.Rules, force: false);
            Assert.True(firstInstall.Success);

            var frontendSource = new GitHubPackSource
            {
                Owner = owner,
                Repo = repo,
                Ref = refValue,
                Path = frontendPath
            };
            var secondInstall = await downloader.DownloadAsync(frontendSource, PackType.Rules, force: false);
            Assert.True(secondInstall.Success);

            var cachePath = secondInstall.CachePath!;
            Assert.True(File.Exists(Path.Combine(cachePath, backendPath, "pack.yaml")));
            Assert.True(File.Exists(Path.Combine(cachePath, backendPath, "backend-rules.md")));
            Assert.True(File.Exists(Path.Combine(cachePath, frontendPath, "pack.yaml")));
            Assert.True(File.Exists(Path.Combine(cachePath, frontendPath, "frontend-rules.md")));

            var projectRoot = Path.Combine(workspaceDir, "steering", "project");
            Directory.CreateDirectory(projectRoot);
            File.WriteAllText(
                Path.Combine(projectRoot, "project-rules.md"),
                BuildRulesDoc("project-doc", "PROJECT-001", "Project-local rule"));

            var configPath = await WriteConfigAsync(
                workspaceDir,
                projectRoot,
                [
                    new RulesPackEntry
                    {
                        Source = $"github:{owner}/{repo}",
                        Ref = refValue,
                        Path = backendPath,
                        Scope = PackScope.Global
                    },
                    new RulesPackEntry
                    {
                        Source = $"github:{owner}/{repo}",
                        Ref = refValue,
                        Path = frontendPath,
                        Scope = PackScope.Supplemental
                    }
                ]);

            var outputDir = Path.Combine(workspaceDir, "output");
            Directory.CreateDirectory(outputDir);

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);

            var generatedFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
            Assert.NotEmpty(generatedFiles);
            var allContent = string.Join("\n", generatedFiles.Select(File.ReadAllText));

            Assert.Contains("BACKEND-001", allContent);
            Assert.Contains("FRONTEND-001", allContent);

            // Ensure both subpaths remain present after command execution.
            Assert.True(File.Exists(Path.Combine(cachePath, backendPath, "pack.yaml")));
            Assert.True(File.Exists(Path.Combine(cachePath, frontendPath, "pack.yaml")));
        }
        finally
        {
            if (Directory.Exists(workspaceDir))
                Directory.Delete(workspaceDir, recursive: true);

            RemoveRulesCache(owner, repo, refValue);
        }
    }

    private static string GetCacheBaseDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".steergen");
    }

    private static void RemoveRulesCache(string owner, string repo, string refValue)
    {
        var cacheBase = GetCacheBaseDirectory();
        var cachePath = Path.Combine(cacheBase, "rules", owner, repo, refValue);
        if (Directory.Exists(cachePath))
            Directory.Delete(cachePath, recursive: true);

        var repoDir = Path.Combine(cacheBase, "rules", owner, repo);
        if (Directory.Exists(repoDir) && !Directory.EnumerateFileSystemEntries(repoDir).Any())
            Directory.Delete(repoDir);

        var ownerDir = Path.Combine(cacheBase, "rules", owner);
        if (Directory.Exists(ownerDir) && !Directory.EnumerateFileSystemEntries(ownerDir).Any())
            Directory.Delete(ownerDir);
    }

    private static async Task<string> WriteConfigAsync(
        string rootDir,
        string projectRoot,
        IReadOnlyList<RulesPackEntry> rulesPacks)
    {
        var configPath = Path.Combine(rootDir, "steergen.config.yaml");
        var config = new SteeringConfiguration
        {
            ProjectRoot = projectRoot,
            RegisteredTargets = ["speckit"],
            RulesPacks = rulesPacks,
        };

        var writer = new SteergenConfigWriter();
        await writer.WriteAsync(configPath, config);
        return configPath;
    }

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

    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, byte[]? content = null)
    {
        return new HttpClient(new FakeHttpMessageHandler(statusCode, content));
    }

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
                foreach (var (relativePath, fileContent) in files)
                {
                    var entry = new PaxTarEntry(TarEntryType.RegularFile, subPrefix + relativePath)
                    {
                        DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(fileContent))
                    };
                    tarWriter.WriteEntry(entry);
                }
            }
        }

        return memoryStream.ToArray();
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
                response.Content = new ByteArrayContent(_content);

            return Task.FromResult(response);
        }
    }
}
