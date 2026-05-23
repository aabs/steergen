using Steergen.Core.Packs;
using Steergen.Core.Validation;

namespace Steergen.Core.UnitTests.Packs;

public sealed class RulesPackLoaderTests : IDisposable
{
    private readonly string _cacheBase;

    public RulesPackLoaderTests()
    {
        _cacheBase = Path.Combine(Path.GetTempPath(), "RulesPackLoaderTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_cacheBase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheBase))
            Directory.Delete(_cacheBase, recursive: true);
    }

    [Fact]
    public void Load_WithConfiguredPath_LoadsRulesFromConfiguredSubdirectory()
    {
        var cacheRoot = Path.Combine(_cacheBase, "rules", "acme", "monorepo", "v1.0.0");
        Directory.CreateDirectory(cacheRoot);

        // Root-level pack that should be ignored when source.Path is configured.
        File.WriteAllText(Path.Combine(cacheRoot, "pack.yaml"),
            "name: root-pack\nversion: 1.0.0\nminSteergenVersion: 0.1.0\nscope: global\n");
        File.WriteAllText(Path.Combine(cacheRoot, "root-rules.md"), ValidRulesMarkdown("ROOT-001", "Root Rule"));

        var subPack = Path.Combine(cacheRoot, "backend-team");
        Directory.CreateDirectory(subPack);
        File.WriteAllText(Path.Combine(subPack, "pack.yaml"),
            "name: backend-pack\nversion: 1.0.0\nminSteergenVersion: 0.1.0\nscope: supplemental\n");
        File.WriteAllText(Path.Combine(subPack, "backend-rules.md"), ValidRulesMarkdown("BACKEND-001", "Backend Rule"));

        var loader = new RulesPackLoader(new PackManifestParser(), new SteeringValidator());
        var result = loader.Load(
            [
                new RulesPackConfiguration
                {
                    Source = new GitHubPackSource
                    {
                        Owner = "acme",
                        Repo = "monorepo",
                        Ref = "v1.0.0",
                        Path = "backend-team"
                    }
                }
            ],
            _cacheBase,
            "99.0.0");

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Single(result.Documents);
        Assert.Single(result.Documents[0].Rules);
        Assert.Equal("BACKEND-001", result.Documents[0].Rules[0].Id);
        Assert.Equal("backend-pack", result.Documents[0].Rules[0].SourcePackName);
    }

    [Fact]
    public void Load_WithConfiguredPathMissingInCache_ReportsRP005()
    {
        var cacheRoot = Path.Combine(_cacheBase, "rules", "acme", "monorepo", "v1.0.0");
        Directory.CreateDirectory(cacheRoot);

        var loader = new RulesPackLoader(new PackManifestParser(), new SteeringValidator());
        var result = loader.Load(
            [
                new RulesPackConfiguration
                {
                    Source = new GitHubPackSource
                    {
                        Owner = "acme",
                        Repo = "monorepo",
                        Ref = "v1.0.0",
                        Path = "missing-pack"
                    }
                }
            ],
            _cacheBase,
            "99.0.0");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("RP005", diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    private static string ValidRulesMarkdown(string ruleId, string title) =>
        $$"""
        ---
        id: {{ruleId.ToLowerInvariant()}}
        version: "1.0.0"
        title: {{title}}
        scope: global
        status: active
        ---

        # {{title}}

        :::rule id="{{ruleId}}" mandatory="true" category="testing"
        Rule text.
        :::
        """;
}
