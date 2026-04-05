using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.PropertyTests.Generation;

/// <summary>
/// Property tests for <see cref="RouteResolver"/>: deterministic single-destination resolution.
/// </summary>
public sealed class RouteResolverProperties
{
    // ── Property: every matched rule resolves to exactly one destination ─────────

    [Fact]
    public void Resolve_WithMatchingRoute_ReturnsExactlyOneDestinationPath()
    {
        var layout = MakeLayout([
            MakeRoute("core", domain: "core", anchor: RouteAnchor.Core, order: 10),
            MakeRoute("catch-all", domain: "*", anchor: RouteAnchor.None, order: 100),
        ]);

        var rules = new[]
        {
            MakeRule("CORE-001", domain: "core"),
            MakeRule("SEC-001", domain: "security"),
            MakeRule("API-001", domain: "api"),
        };

        var resolver = new RouteResolver();
        foreach (var rule in rules)
        {
            var result = resolver.Resolve(rule, layout);
            Assert.True(result.IsResolved, $"Rule '{rule.Id}' should resolve but did not: {result.SelectionReason}");
            Assert.NotNull(result.SelectedDestinationPath);
            Assert.NotEmpty(result.SelectedDestinationPath);
        }
    }

    // ── Property: determinism — same inputs always produce same destination ───────

    [Fact]
    public void Resolve_CalledTwiceWithSameInputs_ProducesSameResult()
    {
        var layout = MakeLayout([
            MakeRoute("core", domain: "core", anchor: RouteAnchor.Core, order: 10),
            MakeRoute("security-module", domain: "security", anchor: RouteAnchor.None, order: 20),
            MakeRoute("catch-all", domain: "*", anchor: RouteAnchor.None, order: 100),
        ]);
        var rule = MakeRule("SEC-001", domain: "security");

        var resolver = new RouteResolver();
        var result1 = resolver.Resolve(rule, layout);
        var result2 = resolver.Resolve(rule, layout);

        Assert.Equal(result1.SelectedRouteId, result2.SelectedRouteId);
        Assert.Equal(result1.SelectedDestinationPath, result2.SelectedDestinationPath);
    }

    // ── Property: explicit route always beats non-explicit for same condition ─────

    [Fact]
    public void Resolve_ExplicitRouteBeatsNonExplicit_WhenBothMatch()
    {
        var layout = MakeLayout([
            MakeRoute("explicit-core", domain: "core", anchor: RouteAnchor.Core, order: 10, isExplicit: true),
            MakeRoute("non-explicit-core", domain: "core", anchor: RouteAnchor.None, order: 5, isExplicit: false),
        ]);
        var rule = MakeRule("CORE-001", domain: "core");

        var result = new RouteResolver().Resolve(rule, layout);

        Assert.Equal("explicit-core", result.SelectedRouteId);
    }

    // ── Property: unresolved result when no route matches ────────────────────────

    [Fact]
    public void Resolve_WithNoMatchingRoute_ReturnsUnresolved()
    {
        var layout = MakeLayout([
            MakeRoute("core", domain: "core", anchor: RouteAnchor.Core, order: 10),
        ]);
        var rule = MakeRule("SEC-001", domain: "security");

        var result = new RouteResolver().Resolve(rule, layout);

        Assert.False(result.IsResolved);
        Assert.Null(result.SelectedRouteId);
        Assert.Null(result.SelectedDestinationPath);
    }

    // ── Property: more specific route beats wildcard when both match ──────────────

    [Fact]
    public void Resolve_SpecificDomainBeforeWildcard_SpecificWins()
    {
        var layout = MakeLayout([
            MakeRoute("core", domain: "core", anchor: RouteAnchor.Core, order: 10),
            MakeRoute("security-specific", domain: "security", anchor: RouteAnchor.None, order: 20),
            MakeRoute("catch-all", domain: "*", anchor: RouteAnchor.None, order: 100),
        ]);
        var rule = MakeRule("SEC-001", domain: "security");

        var result = new RouteResolver().Resolve(rule, layout);

        Assert.Equal("security-specific", result.SelectedRouteId);
    }

    // ── Property: matched route IDs list includes all candidates ─────────────────

    [Fact]
    public void Resolve_MatchedRouteIds_ContainsAllCandidateRoutes()
    {
        var layout = MakeLayout([
            MakeRoute("core", domain: "core", anchor: RouteAnchor.Core, order: 10),
            MakeRoute("security-specific", domain: "security", anchor: RouteAnchor.None, order: 20),
            MakeRoute("catch-all", domain: "*", anchor: RouteAnchor.None, order: 100),
        ]);
        var rule = MakeRule("SEC-001", domain: "security");

        var result = new RouteResolver().Resolve(rule, layout);

        // Both "security-specific" and "catch-all" match a security rule
        Assert.Contains("security-specific", result.MatchedRouteIds);
        Assert.Contains("catch-all", result.MatchedRouteIds);
        Assert.DoesNotContain("core", result.MatchedRouteIds); // domain=core won't match domain=security
    }

    // ── Property: empty layout (no routes) always produces unresolved ─────────────

    [Fact]
    public void Resolve_EmptyRoutesList_AlwaysUnresolved()
    {
        var layout = MakeLayout([]);
        var rule = MakeRule("CORE-001", domain: "core");

        var result = new RouteResolver().Resolve(rule, layout);

        Assert.False(result.IsResolved);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static TargetLayoutDefinition MakeLayout(IReadOnlyList<RouteRuleDefinition> routes) =>
        new()
        {
            TargetId = "test-target",
            Routes = routes,
            Fallback = new FallbackRuleDefinition { Mode = FallbackMode.OtherAtCoreAnchor, FileBaseName = "other" },
        };

    private static RouteRuleDefinition MakeRoute(
        string id,
        string domain,
        RouteAnchor anchor,
        int order,
        bool isExplicit = false) =>
        new()
        {
            Id = id,
            Explicit = isExplicit,
            Anchor = anchor,
            Order = order,
            Match = new RouteMatchExpression { Domain = [domain] },
            Destination = new DestinationTemplate
            {
                Directory = "output/${domain}",
                FileName = "rules",
                Extension = ".md",
            },
        };

    private static SteeringRule MakeRule(string id, string domain) =>
        new() { Id = id, Domain = domain, Severity = "info" };
}
