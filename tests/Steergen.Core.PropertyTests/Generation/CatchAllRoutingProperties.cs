using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.PropertyTests.Generation;

/// <summary>
/// Property tests for wildcard catch-all precedence and fallback ordering in
/// <see cref="RouteResolver"/> and <see cref="RoutePlanner"/>.
/// </summary>
public sealed class CatchAllRoutingProperties
{
    // ── Property: catch-all wildcard matches any rule ────────────────────────────

    [Fact]
    public void Resolve_CatchAllRoute_MatchesAnyCategory()
    {
        var layout = MakeLayout([
            MakeRoute("core", category: "core", anchor: RouteAnchor.Core, order: 10),
            MakeRoute("catch-all", category: "*", anchor: RouteAnchor.None, order: 100),
        ]);
        var resolver = new RouteResolver();

        foreach (var category in new[] { "security", "api", "quality", "unknown-category" })
        {
            var rule = MakeRule($"X-001", category: category);
            var result = resolver.Resolve(rule, layout);
            Assert.True(result.IsResolved, $"Catch-all should match category='{category}'");
        }
    }

    // ── Property: specific route always outranks catch-all ───────────────────────

    [Fact]
    public void Resolve_SpecificRouteAndCatchAll_SpecificRouteWins()
    {
        var layout = MakeLayout([
            MakeRoute("core", category: "core", anchor: RouteAnchor.Core, order: 10),
            MakeRoute("api-specific", category: "api", anchor: RouteAnchor.None, order: 20),
            MakeRoute("catch-all", category: "*", anchor: RouteAnchor.None, order: 100),
        ]);
        var rule = MakeRule("API-001", category: "api");

        var result = new RouteResolver().Resolve(rule, layout);

        Assert.Equal("api-specific", result.SelectedRouteId);
    }

    // ── Property: catch-all is selected when no specific route matches ────────────

    [Fact]
    public void Resolve_NoCatchAllCandidateForSpecificCategory_CatchAllSelectedForUnknown()
    {
        var layout = MakeLayout([
            MakeRoute("core", category: "core", anchor: RouteAnchor.Core, order: 10),
            MakeRoute("api-specific", category: "api", anchor: RouteAnchor.None, order: 20),
            MakeRoute("catch-all", category: "*", anchor: RouteAnchor.None, order: 100),
        ]);
        var rule = MakeRule("SEC-001", category: "security"); // no specific "security" route

        var result = new RouteResolver().Resolve(rule, layout);

        Assert.Equal("catch-all", result.SelectedRouteId);
    }

    // ── Property: fallback only applies when no route (incl. catch-all) matches ───

    [Fact]
    public void Plan_WhenCatchAllExists_FallbackNeverApplied()
    {
        var layout = MakeLayout([
            MakeRoute("core", category: "core", anchor: RouteAnchor.Core, order: 10),
            MakeRoute("catch-all", category: "*", anchor: RouteAnchor.None, order: 100),
        ]);
        var rules = new[]
        {
            MakeRule("SEC-001", category: "security"),
            MakeRule("API-001", category: "api"),
            MakeRule("MISC-001", category: "misc"),
        };

        var planner = new RoutePlanner();
        var results = planner.Plan(rules, layout);

        foreach (var result in results)
        {
            Assert.True(result.IsResolved);
            // Fallback routes have IDs that start with "<fallback:"
            Assert.DoesNotContain("<fallback:", result.SelectedRouteId ?? "");
        }
    }

    // ── Property: fallback applies when no route matches ─────────────────────────

    [Fact]
    public void Plan_WhenNoRouteMatches_FallbackAppliedAtCoreAnchorDirectory()
    {
        var layout = MakeLayoutWithCoreAnchorDirectory([
            MakeRouteWithDir("core", category: "core", anchor: RouteAnchor.Core, order: 10, dir: ".kiro/steering"),
            // No catch-all; only core category routed explicitly
        ]);
        var rule = MakeRule("SEC-001", category: "security"); // won't match core route

        var planner = new RoutePlanner();
        var results = planner.Plan([rule], layout);

        Assert.Single(results);
        var result = results[0];
        Assert.True(result.IsResolved, "Fallback should resolve the rule");
        Assert.StartsWith("<fallback:", result.SelectedRouteId);
        Assert.NotNull(result.SelectedDestinationPath);
        // Fallback path should be in the core anchor directory
        Assert.Contains(".kiro/steering", result.SelectedDestinationPath);
        Assert.Contains("other", result.SelectedDestinationPath);
    }

    // ── Property: rules routed via catch-all have non-null destination path ───────

    [Fact]
    public void Resolve_CatchAllRoute_ProducesNonNullDestinationPath()
    {
        var layout = MakeLayout([
            MakeRoute("core", category: "core", anchor: RouteAnchor.Core, order: 10),
            MakeRoute("catch-all", category: "*", anchor: RouteAnchor.None, order: 100),
        ]);
        var rule = MakeRule("UNKNOWN-001", category: "unknown");

        var result = new RouteResolver().Resolve(rule, layout);

        Assert.NotNull(result.SelectedDestinationPath);
        Assert.NotEmpty(result.SelectedDestinationPath);
    }

    // ── Property: write plan groups catch-all resolutions by their destination ────

    [Fact]
    public void WritePlanBuilder_CatchAllResolutions_GroupedByDestination()
    {
        var layout = MakeLayout([
            MakeRoute("core", category: "core", anchor: RouteAnchor.Core, order: 10),
            MakeRoute("catch-all", category: "*", anchor: RouteAnchor.None, order: 100),
        ]);
        var rules = new[]
        {
            MakeRule("SEC-001", category: "security"),
            MakeRule("SEC-002", category: "security"),
            MakeRule("API-001", category: "api"),
        };

        var planner = new RoutePlanner();
        var resolutions = planner.Plan(rules, layout);
        var planBuilder = new WritePlanBuilder();
        var plan = planBuilder.Build("test-target", resolutions);

        // All rules with same destination should be in the same WritePlanFile
        Assert.True(plan.Files.Count >= 1, "Plan should have at least one file");
        var totalUnits = plan.Files.Sum(f => f.AppendUnits.Count);
        Assert.Equal(rules.Length, totalUnits);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static TargetLayoutDefinition MakeLayout(IReadOnlyList<RouteRuleDefinition> routes) =>
        new()
        {
            TargetId = "test-target",
            Routes = routes,
            Fallback = new FallbackRuleDefinition { Mode = FallbackMode.OtherAtCoreAnchor, FileBaseName = "other" },
        };

    private static TargetLayoutDefinition MakeLayoutWithCoreAnchorDirectory(IReadOnlyList<RouteRuleDefinition> routes) =>
        new()
        {
            TargetId = "test-target",
            Routes = routes,
            Fallback = new FallbackRuleDefinition { Mode = FallbackMode.OtherAtCoreAnchor, FileBaseName = "other" },
        };

    private static RouteRuleDefinition MakeRoute(
        string id,
        string category,
        RouteAnchor anchor,
        int order) =>
        new()
        {
            Id = id,
            Explicit = anchor == RouteAnchor.Core,
            Anchor = anchor,
            Order = order,
            Match = new RouteMatchExpression { Category = [category] },
            Destination = new DestinationTemplate
            {
                Directory = "output/${category}",
                FileName = "rules",
                Extension = ".md",
            },
        };

    private static RouteRuleDefinition MakeRouteWithDir(
        string id,
        string category,
        RouteAnchor anchor,
        int order,
        string dir) =>
        new()
        {
            Id = id,
            Explicit = anchor == RouteAnchor.Core,
            Anchor = anchor,
            Order = order,
            Match = new RouteMatchExpression { Category = [category] },
            Destination = new DestinationTemplate
            {
                Directory = dir,
                FileName = "${category}",
                Extension = ".md",
            },
        };

    private static SteeringRule MakeRule(string id, string category) =>
        new() { Id = id, Category = category };
}
