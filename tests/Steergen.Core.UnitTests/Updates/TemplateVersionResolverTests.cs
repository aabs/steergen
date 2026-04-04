using Steergen.Core.Updates;
using Xunit;

namespace Steergen.Core.UnitTests.Updates;

/// <summary>
/// Unit tests for <see cref="TemplateVersionResolver"/> covering stable and
/// <c>previewN</c> SemVer parsing, latest-stable resolution, latest-preview
/// resolution, and exact-version validation.
/// </summary>
public sealed class TemplateVersionResolverTests
{
    // ── IsValidVersion ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("2.3.4", true)]
    [InlineData("10.20.30", true)]
    [InlineData("1.0.0-preview1", true)]
    [InlineData("1.0.0-preview12", true)]
    [InlineData("2.3.4-preview99", true)]
    [InlineData("", false)]
    [InlineData("1.0", false)]
    [InlineData("1.0.0.0", false)]
    [InlineData("1.0.0-alpha1", false)]
    [InlineData("1.0.0-beta2", false)]
    [InlineData("abc", false)]
    public void IsValidVersion_ReturnsExpected(string version, bool expected)
    {
        Assert.Equal(expected, TemplateVersionResolver.IsValidVersion(version));
    }

    // ── IsPreviewVersion ────────────────────────────────────────────────────

    [Theory]
    [InlineData("1.0.0-preview1", true)]
    [InlineData("2.0.0-preview99", true)]
    [InlineData("1.0.0", false)]
    [InlineData("2.3.4", false)]
    public void IsPreviewVersion_ReturnsExpected(string version, bool expected)
    {
        Assert.Equal(expected, TemplateVersionResolver.IsPreviewVersion(version));
    }

    // ── ResolveLatestStable ──────────────────────────────────────────────────

    [Fact]
    public void ResolveLatestStable_EmptyCatalog_ReturnsNull()
    {
        var result = TemplateVersionResolver.ResolveLatestStable([]);
        Assert.Null(result);
    }

    [Fact]
    public void ResolveLatestStable_OnlyPreviewVersions_ReturnsNull()
    {
        var result = TemplateVersionResolver.ResolveLatestStable(["1.0.0-preview1", "2.0.0-preview2"]);
        Assert.Null(result);
    }

    [Fact]
    public void ResolveLatestStable_MixedCatalog_ReturnsHighestStable()
    {
        var catalog = new[] { "1.0.0", "1.2.0", "2.0.0-preview1", "1.1.0" };
        var result = TemplateVersionResolver.ResolveLatestStable(catalog);
        Assert.Equal("1.2.0", result);
    }

    [Fact]
    public void ResolveLatestStable_AllStable_ReturnsHighest()
    {
        var catalog = new[] { "3.0.0", "1.0.0", "2.1.0" };
        var result = TemplateVersionResolver.ResolveLatestStable(catalog);
        Assert.Equal("3.0.0", result);
    }

    [Fact]
    public void ResolveLatestStable_SingleStableEntry_ReturnsThatEntry()
    {
        var result = TemplateVersionResolver.ResolveLatestStable(["1.0.0"]);
        Assert.Equal("1.0.0", result);
    }

    // ── ResolveLatestIncludingPreview ────────────────────────────────────────

    [Fact]
    public void ResolveLatestIncludingPreview_EmptyCatalog_ReturnsNull()
    {
        var result = TemplateVersionResolver.ResolveLatestIncludingPreview([]);
        Assert.Null(result);
    }

    [Fact]
    public void ResolveLatestIncludingPreview_MixedCatalog_ReturnsHighestOverall()
    {
        var catalog = new[] { "1.0.0", "1.2.0", "2.0.0-preview1", "1.1.0" };
        var result = TemplateVersionResolver.ResolveLatestIncludingPreview(catalog);
        Assert.Equal("2.0.0-preview1", result);
    }

    [Fact]
    public void ResolveLatestIncludingPreview_AllStable_ReturnsHighestStable()
    {
        var catalog = new[] { "1.0.0", "3.0.0", "2.0.0" };
        var result = TemplateVersionResolver.ResolveLatestIncludingPreview(catalog);
        Assert.Equal("3.0.0", result);
    }

    [Fact]
    public void ResolveLatestIncludingPreview_MultiplePreviewSameBase_ReturnsHighestPreviewN()
    {
        var catalog = new[] { "2.0.0-preview3", "2.0.0-preview10", "2.0.0-preview1" };
        var result = TemplateVersionResolver.ResolveLatestIncludingPreview(catalog);
        Assert.Equal("2.0.0-preview10", result);
    }

    // ── ResolveExact ─────────────────────────────────────────────────────────

    [Fact]
    public void ResolveExact_MatchingVersion_ReturnsIt()
    {
        var catalog = new[] { "1.0.0", "1.2.0", "2.0.0-preview1" };
        var result = TemplateVersionResolver.ResolveExact(catalog, "1.2.0");
        Assert.Equal("1.2.0", result);
    }

    [Fact]
    public void ResolveExact_MatchingPreviewVersion_ReturnsIt()
    {
        var catalog = new[] { "1.0.0", "2.0.0-preview1" };
        var result = TemplateVersionResolver.ResolveExact(catalog, "2.0.0-preview1");
        Assert.Equal("2.0.0-preview1", result);
    }

    [Fact]
    public void ResolveExact_VersionNotInCatalog_ReturnsNull()
    {
        var catalog = new[] { "1.0.0", "1.2.0" };
        var result = TemplateVersionResolver.ResolveExact(catalog, "9.9.9");
        Assert.Null(result);
    }

    [Fact]
    public void ResolveExact_InvalidVersionFormat_ReturnsNull()
    {
        var catalog = new[] { "1.0.0" };
        var result = TemplateVersionResolver.ResolveExact(catalog, "not-a-version");
        Assert.Null(result);
    }

    // ── Compare / ordering ───────────────────────────────────────────────────

    [Fact]
    public void ResolveLatestStable_HandlesVersionsOutOfOrder()
    {
        var catalog = new[] { "1.10.0", "1.9.0", "1.2.0" };
        var result = TemplateVersionResolver.ResolveLatestStable(catalog);
        Assert.Equal("1.10.0", result);
    }

    [Fact]
    public void ResolveLatestIncludingPreview_PreviewBehindCurrentStable_StableWins()
    {
        // 2.0.0-preview1 loses to 2.0.0 (stable wins same base when numerically higher)
        var catalog = new[] { "2.0.0", "2.0.0-preview1" };
        var result = TemplateVersionResolver.ResolveLatestIncludingPreview(catalog);
        Assert.Equal("2.0.0", result);
    }
}
