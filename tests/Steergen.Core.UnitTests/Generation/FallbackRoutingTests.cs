using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.UnitTests.Generation;

/// <summary>
/// Unit tests for core-anchor <c>other.*</c> fallback behavior in <see cref="RoutePlanner"/>.
/// </summary>
public sealed class FallbackRoutingTests
{
    private readonly RoutePlanner _planner = new();

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static TargetLayoutDefinition MakeLayout(
        IReadOnlyList<RouteRuleDefinition> routes,
        string fallbackBaseName = "other")
    {
        return new TargetLayoutDefinition
        {
            TargetId = "speckit",
            Routes = routes.ToList(),
            Fallback = new FallbackRuleDefinition
            {
                Mode = FallbackMode.OtherAtCoreAnchor,
                FileBaseName = fallbackBaseName,
            },
        };
    }

    private static RouteRuleDefinition CoreRoute(string dir = "rules", string ext = ".md") =>
        new()
        {
            Id = "core-route",
            Scope = RouteScope.Both,
            Anchor = RouteAnchor.Core,
            Order = 10,
            Match = new RouteMatchExpression { Domain = ["core"] },
            Destination = new DestinationTemplate
            {
                Directory = dir,
                FileName = "constitution",
                Extension = ext,
            },
        };

    private static RouteRuleDefinition SpecificRoute(string id, string domain) =>
        new()
        {
            Id = id,
            Scope = RouteScope.Both,
            Anchor = RouteAnchor.None,
            Order = 20,
            Match = new RouteMatchExpression { Domain = [domain] },
            Destination = new DestinationTemplate
            {
                Directory = "modules",
                FileName = domain,
                Extension = ".md",
            },
        };

    private static SteeringRule MakeRule(string id, string domain = "core") =>
        new() { Id = id, Domain = domain };

    // ── Fallback when no route matches ─────────────────────────────────────────

    [Fact]
    public void Plan_UnmatchedRule_FallsBackToOtherAtCoreAnchorDir()
    {
        var layout = MakeLayout([CoreRoute("core-dir")]);
        var rule = MakeRule("UNKNOWN-001", domain: "unknown");

        var results = _planner.Plan([rule], layout);

        Assert.Single(results);
        var result = results[0];
        Assert.True(result.IsResolved, "Fallback should produce a resolved result.");
        Assert.Equal("other.md", Path.GetFileName(result.SelectedDestinationPath));
        Assert.Contains("core-dir", result.SelectedDestinationPath!);
    }

    [Fact]
    public void Plan_UnmatchedRule_FallbackUsesExtensionFromCoreAnchor()
    {
        var layout = MakeLayout([CoreRoute(dir: "rules", ext: ".yaml")]);
        var rule = MakeRule("X-001", domain: "unknown");

        var results = _planner.Plan([rule], layout);

        Assert.Equal("other.yaml", Path.GetFileName(results[0].SelectedDestinationPath));
    }

    [Fact]
    public void Plan_UnmatchedRule_FallbackUsesLayoutFileBaseName()
    {
        var layout = MakeLayout([CoreRoute()], fallbackBaseName: "misc");
        var rule = MakeRule("X-001", domain: "unknown");

        var results = _planner.Plan([rule], layout);

        Assert.StartsWith("misc", Path.GetFileNameWithoutExtension(results[0].SelectedDestinationPath));
    }

    // ── Catch-all prevents fallback ────────────────────────────────────────────

    [Fact]
    public void Plan_CatchAllRoute_PreventsOtherFallback()
    {
        var catchAll = new RouteRuleDefinition
        {
            Id = "catch-all",
            Scope = RouteScope.Both,
            Anchor = RouteAnchor.None,
            Order = 100,
            Match = new RouteMatchExpression { Domain = ["*"] },
            Destination = new DestinationTemplate
            {
                Directory = "catch",
                FileName = "all",
                Extension = ".md",
            },
        };
        var layout = MakeLayout([CoreRoute(), catchAll]);
        var rule = MakeRule("X-001", domain: "unknown");

        var results = _planner.Plan([rule], layout);

        Assert.True(results[0].IsResolved);
        Assert.Equal("catch-all", results[0].SelectedRouteId);
        Assert.DoesNotContain("other", results[0].SelectedDestinationPath ?? "");
    }

    // ── No core anchor ─────────────────────────────────────────────────────────

    [Fact]
    public void Plan_NoCoreAnchorAndNoMatch_ReturnsUnresolvedWithDiagnosticMessage()
    {
        var layout = MakeLayout([SpecificRoute("api-route", "api")]);
        var rule = MakeRule("X-001", domain: "security");

        var results = _planner.Plan([rule], layout);

        var result = results[0];
        Assert.False(result.IsResolved, "Without core anchor and no match, result should be unresolved.");
        Assert.Contains("core-anchor", result.SelectionReason, StringComparison.OrdinalIgnoreCase);
    }

    // ── Multiple unmatched rules ────────────────────────────────────────────────

    [Fact]
    public void Plan_MultipleUnmatchedRules_AllFallbackToSameOtherFile()
    {
        var layout = MakeLayout([CoreRoute("base")]);
        var rules = new[]
        {
            MakeRule("X-001", domain: "unknown-a"),
            MakeRule("X-002", domain: "unknown-b"),
            MakeRule("X-003", domain: "unknown-c"),
        };

        var results = _planner.Plan(rules, layout);

        var fallbackPaths = results
            .Where(r => r.IsResolved)
            .Select(r => r.SelectedDestinationPath)
            .Distinct()
            .ToList();

        Assert.True(fallbackPaths.Count == 1, "All unmatched rules should fall back to the same other.md file.");
    }

    // ── Mixed matched and unmatched ─────────────────────────────────────────────

    [Fact]
    public void Plan_MixedMatchedAndUnmatched_MatchedUseRouteAndUnmatchedUseFallback()
    {
        var layout = MakeLayout([CoreRoute(), SpecificRoute("api-route", "api")]);
        var rules = new[]
        {
            MakeRule("CORE-001", domain: "core"),
            MakeRule("API-001", domain: "api"),
            MakeRule("X-001", domain: "security"),
        };

        var results = _planner.Plan(rules, layout);

        Assert.Equal("core-route", results[0].SelectedRouteId);
        Assert.Equal("api-route", results[1].SelectedRouteId);
        Assert.Contains("other", results[2].SelectedDestinationPath ?? "");
    }

    // ── Fallback path is at core anchor directory ───────────────────────────────

    [Fact]
    public void Plan_FallbackPath_IsColocatedWithCoreAnchorDirectory()
    {
        var layout = MakeLayout([CoreRoute(dir: "my-team/speckit")]);
        var rule = MakeRule("X-001", domain: "anything");

        var results = _planner.Plan([rule], layout);

        Assert.StartsWith("my-team/speckit/", results[0].SelectedDestinationPath);
    }
}
