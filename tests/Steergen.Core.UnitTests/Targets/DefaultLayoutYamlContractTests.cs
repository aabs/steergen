using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Xunit;

namespace Steergen.Core.UnitTests.Targets;

/// <summary>
/// Verifies that every built-in target's default-layout.yaml (embedded in Steergen.Core)
/// satisfies the config-schema contract: required top-level sections, roots, at least one
/// core-anchor route per scope, fallback section, and purge section.
/// </summary>
public sealed class DefaultLayoutYamlContractTests
{
    private static readonly string[] BuiltinTargetIds =
        ["speckit", "kiro", "copilot-agent", "kiro-agent"];

    private static TargetLayoutDefinition Load(string targetId) =>
        new LayoutOverrideLoader().LoadDefault(targetId);

    [Theory]
    [InlineData("speckit")]
    [InlineData("kiro")]
    [InlineData("copilot-agent")]
    [InlineData("kiro-agent")]
    public void DefaultLayout_HasVersion(string targetId)
    {
        var layout = Load(targetId);
        Assert.False(string.IsNullOrWhiteSpace(layout.Version),
            $"Target '{targetId}' default-layout.yaml must declare a non-empty version.");
    }

    [Theory]
    [InlineData("speckit")]
    [InlineData("kiro")]
    [InlineData("copilot-agent")]
    [InlineData("kiro-agent")]
    public void DefaultLayout_HasAllRootTemplates(string targetId)
    {
        var roots = Load(targetId).Roots;
        Assert.False(string.IsNullOrWhiteSpace(roots.GlobalRoot),
            $"Target '{targetId}' roots.globalRoot must be non-empty.");
        Assert.False(string.IsNullOrWhiteSpace(roots.ProjectRoot),
            $"Target '{targetId}' roots.projectRoot must be non-empty.");
        Assert.False(string.IsNullOrWhiteSpace(roots.TargetRoot),
            $"Target '{targetId}' roots.targetRoot must be non-empty.");
    }

    [Theory]
    [InlineData("speckit")]
    [InlineData("kiro")]
    [InlineData("copilot-agent")]
    [InlineData("kiro-agent")]
    public void DefaultLayout_HasAtLeastOneRoute(string targetId)
    {
        var layout = Load(targetId);
        Assert.NotEmpty(layout.Routes);
    }

    [Theory]
    [InlineData("speckit")]
    [InlineData("kiro")]
    [InlineData("copilot-agent")]
    [InlineData("kiro-agent")]
    public void DefaultLayout_HasCoreAnchorRouteForGlobalScope(string targetId)
    {
        var layout = Load(targetId);
        var hasCoreGlobal = layout.Routes.Any(r =>
            r.Anchor == RouteAnchor.Core &&
            (r.Scope == RouteScope.Global || r.Scope == RouteScope.Both));
        Assert.True(hasCoreGlobal,
            $"Target '{targetId}' must have at least one core-anchor route for global scope.");
    }

    [Theory]
    [InlineData("speckit")]
    [InlineData("kiro")]
    [InlineData("copilot-agent")]
    [InlineData("kiro-agent")]
    public void DefaultLayout_HasCoreAnchorRouteForProjectScope(string targetId)
    {
        var layout = Load(targetId);
        var hasCoreProject = layout.Routes.Any(r =>
            r.Anchor == RouteAnchor.Core &&
            (r.Scope == RouteScope.Project || r.Scope == RouteScope.Both));
        Assert.True(hasCoreProject,
            $"Target '{targetId}' must have at least one core-anchor route for project scope.");
    }

    [Theory]
    [InlineData("speckit")]
    [InlineData("kiro")]
    [InlineData("copilot-agent")]
    [InlineData("kiro-agent")]
    public void DefaultLayout_AllRoutesHaveUniqueIds(string targetId)
    {
        var routes = Load(targetId).Routes;
        var ids = routes.Select(r => r.Id).ToList();
        var distinctIds = ids.Distinct(StringComparer.Ordinal).ToList();
        Assert.Equal(ids.Count, distinctIds.Count);
    }

    [Theory]
    [InlineData("speckit")]
    [InlineData("kiro")]
    [InlineData("copilot-agent")]
    [InlineData("kiro-agent")]
    public void DefaultLayout_AllRoutesHaveNonEmptyDestination(string targetId)
    {
        var routes = Load(targetId).Routes;
        foreach (var route in routes)
        {
            Assert.False(string.IsNullOrWhiteSpace(route.Destination.Directory),
                $"Route '{route.Id}' in '{targetId}' must have a non-empty destination.directory.");
            Assert.False(string.IsNullOrWhiteSpace(route.Destination.FileName),
                $"Route '{route.Id}' in '{targetId}' must have a non-empty destination.fileName.");
        }
    }

    [Theory]
    [InlineData("speckit")]
    [InlineData("kiro")]
    [InlineData("copilot-agent")]
    [InlineData("kiro-agent")]
    public void DefaultLayout_HasFallbackSection(string targetId)
    {
        var fallback = Load(targetId).Fallback;
        Assert.NotNull(fallback);
        Assert.Equal(FallbackMode.OtherAtCoreAnchor, fallback.Mode);
        Assert.False(string.IsNullOrWhiteSpace(fallback.FileBaseName));
    }

    [Theory]
    [InlineData("speckit")]
    [InlineData("kiro")]
    [InlineData("copilot-agent")]
    [InlineData("kiro-agent")]
    public void DefaultLayout_HasPurgeSection(string targetId)
    {
        var purge = Load(targetId).Purge;
        Assert.NotNull(purge);
        Assert.NotEmpty(purge.Roots);
    }

    [Theory]
    [InlineData("speckit")]
    [InlineData("kiro")]
    [InlineData("copilot-agent")]
    [InlineData("kiro-agent")]
    public void DefaultLayout_AllRouteIdsAreNonEmpty(string targetId)
    {
        var routes = Load(targetId).Routes;
        foreach (var route in routes)
            Assert.False(string.IsNullOrWhiteSpace(route.Id),
                $"A route in target '{targetId}' has an empty or missing id.");
    }
}
