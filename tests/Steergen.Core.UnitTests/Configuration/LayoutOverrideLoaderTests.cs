using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Xunit;

namespace Steergen.Core.UnitTests.Configuration;

/// <summary>
/// Unit tests for <see cref="LayoutOverrideLoader"/> deep-merge semantics:
/// recursive map merge with scalar/list replacement by override values.
/// </summary>
public sealed class LayoutOverrideLoaderTests
{
    private static readonly string TempDir = Path.Combine(
        AppContext.BaseDirectory, "testdata", "layout-override");

    private static string WriteTempYaml(string content)
    {
        Directory.CreateDirectory(TempDir);
        var path = Path.Combine(TempDir, Guid.NewGuid() + ".yaml");
        File.WriteAllText(path, content);
        return path;
    }

    // ── LoadDefault ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("speckit")]
    [InlineData("kiro")]
    [InlineData("copilot-agent")]
    [InlineData("kiro-agent")]
    public void LoadDefault_BuiltinTarget_ReturnsPopulatedLayout(string targetId)
    {
        var loader = new LayoutOverrideLoader();
        var layout = loader.LoadDefault(targetId);

        Assert.Equal(targetId, layout.TargetId);
        Assert.NotEmpty(layout.Routes);
        Assert.NotNull(layout.Fallback);
    }

    [Fact]
    public void LoadDefault_UnknownTarget_Throws()
    {
        var loader = new LayoutOverrideLoader();
        Assert.Throws<ArgumentException>(() => loader.LoadDefault("unknown-target"));
    }

    // ── Deep-merge: scalars ───────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_OverrideVersion_ReplacesDefault()
    {
        var overrideYaml = "version: \"2.0\"\n";
        var overridePath = WriteTempYaml(overrideYaml);

        var loader = new LayoutOverrideLoader();
        var layout = await loader.LoadAsync("speckit", overridePath);

        Assert.Equal("2.0", layout.Version);
    }

    [Fact]
    public async Task LoadAsync_NullOverridePath_ReturnsDefaultLayout()
    {
        var loader = new LayoutOverrideLoader();
        var layout = await loader.LoadAsync("speckit", overrideFilePath: null);

        Assert.Equal("speckit", layout.TargetId);
        Assert.NotEmpty(layout.Routes);
    }

    // ── Deep-merge: roots map merges recursively ─────────────────────────────

    [Fact]
    public async Task LoadAsync_OverrideTargetRoot_MergesRootsMap()
    {
        var overrideYaml = """
            roots:
              targetRoot: "${projectRoot}/custom"
            """;
        var overridePath = WriteTempYaml(overrideYaml);

        var loader = new LayoutOverrideLoader();
        var layout = await loader.LoadAsync("speckit", overridePath);

        // Only targetRoot should change; globalRoot and projectRoot stay from default.
        Assert.Equal("${globalRoot}", layout.Roots.GlobalRoot);
        Assert.Equal("${projectRoot}", layout.Roots.ProjectRoot);
        Assert.Equal("${projectRoot}/custom", layout.Roots.TargetRoot);
    }

    // ── Deep-merge: lists are replaced (not merged) ──────────────────────────

    [Fact]
    public async Task LoadAsync_OverridePurgeGlobs_ReplacesList()
    {
        var overrideYaml = """
            purge:
              roots:
                - "${projectRoot}/.speckit"
              globs:
                - "**/*.txt"
            """;
        var overridePath = WriteTempYaml(overrideYaml);

        var loader = new LayoutOverrideLoader();
        var layout = await loader.LoadAsync("speckit", overridePath);

        Assert.NotNull(layout.Purge);
        Assert.Single(layout.Purge!.Globs);
        Assert.Equal("**/*.txt", layout.Purge.Globs[0]);
    }

    [Fact]
    public async Task LoadAsync_OverrideRoutesList_ReplacesDefaultRoutes()
    {
        var overrideYaml = """
            routes:
              - id: custom-route
                scope: project
                explicit: true
                anchor: core
                order: 1
                match:
                  domain: core
                destination:
                  directory: "${projectRoot}/custom"
                  fileName: "rules"
                  extension: ".md"
            """;
        var overridePath = WriteTempYaml(overrideYaml);

        var loader = new LayoutOverrideLoader();
        var layout = await loader.LoadAsync("speckit", overridePath);

        Assert.Single(layout.Routes);
        Assert.Equal("custom-route", layout.Routes[0].Id);
    }

    // ── Deep-merge: fallback map merges ──────────────────────────────────────

    [Fact]
    public async Task LoadAsync_OverrideFallbackFileBaseName_MergesFallback()
    {
        var overrideYaml = """
            fallback:
              fileBaseName: extras
            """;
        var overridePath = WriteTempYaml(overrideYaml);

        var loader = new LayoutOverrideLoader();
        var layout = await loader.LoadAsync("speckit", overridePath);

        Assert.Equal(FallbackMode.OtherAtCoreAnchor, layout.Fallback.Mode);
        Assert.Equal("extras", layout.Fallback.FileBaseName);
    }

    // ── Deep-merge: absent overrides preserve defaults ────────────────────────

    [Fact]
    public async Task LoadAsync_EmptyOverride_PreservesDefaultRoutes()
    {
        var defaultLayout = new LayoutOverrideLoader().LoadDefault("speckit");
        var overrideYaml = "version: \"9.9\"\n";
        var overridePath = WriteTempYaml(overrideYaml);

        var loader = new LayoutOverrideLoader();
        var merged = await loader.LoadAsync("speckit", overridePath);

        // Routes should be unchanged (override didn't specify routes).
        Assert.Equal(defaultLayout.Routes.Count, merged.Routes.Count);
    }

    // ── StringOrList YAML format tests ───────────────────────────────────────

    [Fact]
    public void LoadDefault_Speckit_DomainWildcardRouteHasSingleWildcard()
    {
        var layout = new LayoutOverrideLoader().LoadDefault("speckit");
        var catchAll = layout.Routes.FirstOrDefault(r => r.Id == "domain-module-global");
        Assert.NotNull(catchAll);
        Assert.Single(catchAll!.Match.Domain);
        Assert.Equal("*", catchAll.Match.Domain[0]);
    }
}
