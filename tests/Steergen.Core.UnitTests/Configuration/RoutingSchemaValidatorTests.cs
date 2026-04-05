using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Validation;
using Xunit;

namespace Steergen.Core.UnitTests.Configuration;

/// <summary>
/// Unit tests for <see cref="RoutingSchemaValidator"/> validation codes,
/// including override-specific scenarios (duplicate IDs, missing core anchor,
/// invalid routes, unknown variables in destination templates).
/// </summary>
public sealed class RoutingSchemaValidatorTests
{
    private static RoutingSchemaValidator Sut => new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TargetLayoutDefinition MakeLayout(params RouteRuleDefinition[] routes) =>
        new()
        {
            TargetId = "test-target",
            Routes = routes.ToList(),
            Fallback = new FallbackRuleDefinition { Mode = FallbackMode.OtherAtCoreAnchor },
        };

    private static RouteRuleDefinition CoreRoute(string id = "core") =>
        new()
        {
            Id = id,
            Scope = RouteScope.Both,
            Anchor = RouteAnchor.Core,
            Explicit = true,
            Order = 0,
            Match = new RouteMatchExpression { Domain = ["core"] },
            Destination = new DestinationTemplate { Directory = "rules", FileName = "constitution", Extension = ".md" },
        };

    private static RouteRuleDefinition ExtraRoute(string id, string domain = "*") =>
        new()
        {
            Id = id,
            Scope = RouteScope.Both,
            Order = 10,
            Match = new RouteMatchExpression { Domain = [domain] },
            Destination = new DestinationTemplate { Directory = "rules", FileName = id, Extension = ".md" },
        };

    // ── RS001: Empty routes ───────────────────────────────────────────────────

    [Fact]
    public void Validate_EmptyRoutes_ProducesRS001Error()
    {
        var layout = MakeLayout(); // no routes
        var diagnostics = Sut.Validate(layout);

        Assert.Single(diagnostics, d => d.Code == "RS001" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Validate_EmptyRoutes_MessageContainsTargetId()
    {
        var layout = MakeLayout();
        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics, d => d.Code == "RS001" && d.Message.Contains("test-target"));
    }

    // ── RS002: Duplicate route IDs ────────────────────────────────────────────

    [Fact]
    public void Validate_DuplicateRouteIds_ProducesRS002Error()
    {
        var layout = MakeLayout(
            CoreRoute("core"),
            ExtraRoute("dup-id"),
            ExtraRoute("dup-id")); // duplicate

        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics, d => d.Code == "RS002" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Validate_DuplicateRouteIds_MessageContainsDuplicateId()
    {
        var layout = MakeLayout(
            CoreRoute("core"),
            ExtraRoute("dup-id"),
            ExtraRoute("dup-id"));

        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics, d => d.Code == "RS002" && d.Message.Contains("dup-id"));
    }

    [Fact]
    public void Validate_MultipleDuplicatePairs_ProducesRS002PerDuplicate()
    {
        var layout = MakeLayout(
            CoreRoute("core"),
            ExtraRoute("dup1"),
            ExtraRoute("dup1"),
            ExtraRoute("dup2"),
            ExtraRoute("dup2"));

        var diagnostics = Sut.Validate(layout);
        var rs002 = diagnostics.Where(d => d.Code == "RS002").ToList();

        Assert.Equal(2, rs002.Count);
    }

    [Fact]
    public void Validate_UniqueRouteIds_NoRS002Diagnostics()
    {
        var layout = MakeLayout(CoreRoute("core"), ExtraRoute("route-a"), ExtraRoute("route-b"));
        var diagnostics = Sut.Validate(layout);

        Assert.DoesNotContain(diagnostics, d => d.Code == "RS002");
    }

    // ── RS003: Core anchor required ───────────────────────────────────────────

    [Fact]
    public void Validate_NoCoreAnchorRoute_ProducesRS003Error()
    {
        var layout = MakeLayout(ExtraRoute("route-a"), ExtraRoute("route-b"));
        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics, d => d.Code == "RS003" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Validate_NoCoreAnchorRoute_MessageContainsTargetId()
    {
        var layout = MakeLayout(ExtraRoute("route-a"));
        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics, d => d.Code == "RS003" && d.Message.Contains("test-target"));
    }

    [Fact]
    public void Validate_WithCoreAnchorRoute_NoRS003Diagnostics()
    {
        var layout = MakeLayout(CoreRoute("core"), ExtraRoute("other"));
        var diagnostics = Sut.Validate(layout);

        Assert.DoesNotContain(diagnostics, d => d.Code == "RS003");
    }

    // ── RS003 interaction with RS001 ──────────────────────────────────────────

    [Fact]
    public void Validate_EmptyRoutes_DoesNotAlsoEmitRS003()
    {
        // RS001 covers empty routes; RS003 should not fire redundantly.
        var layout = MakeLayout();
        var diagnostics = Sut.Validate(layout);

        Assert.DoesNotContain(diagnostics, d => d.Code == "RS003");
    }

    // ── Override-specific invalid route scenarios ─────────────────────────────

    [Fact]
    public void Validate_OverrideWithPathTraversal_ProducesRS004()
    {
        var traversalRoute = new RouteRuleDefinition
        {
            Id = "bad",
            Scope = RouteScope.Both,
            Order = 5,
            Destination = new DestinationTemplate { Directory = "../escaped", FileName = "file", Extension = ".md" },
        };
        var layout = MakeLayout(CoreRoute(), traversalRoute);
        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics, d => d.Code == "RS004");
    }

    [Fact]
    public void Validate_OverrideWithAbsolutePath_ProducesRS005()
    {
        var absRoute = new RouteRuleDefinition
        {
            Id = "absolute",
            Scope = RouteScope.Both,
            Order = 5,
            Destination = new DestinationTemplate { Directory = "/absolute/path", FileName = "file", Extension = ".md" },
        };
        var layout = MakeLayout(CoreRoute(), absRoute);
        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics, d => d.Code == "RS005");
    }

    [Fact]
    public void Validate_OverrideWithSeparatorInFileName_ProducesRS006()
    {
        var badNameRoute = new RouteRuleDefinition
        {
            Id = "bad-name",
            Scope = RouteScope.Both,
            Order = 5,
            Destination = new DestinationTemplate { Directory = "rules", FileName = "sub/bad", Extension = ".md" },
        };
        var layout = MakeLayout(CoreRoute(), badNameRoute);
        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics, d => d.Code == "RS006");
    }

    // ── Valid override layout: no diagnostics ─────────────────────────────────

    [Fact]
    public void Validate_ValidOverrideLayout_NoDiagnostics()
    {
        var layout = MakeLayout(
            CoreRoute("core-anchor"),
            ExtraRoute("domain-security", "security"),
            ExtraRoute("catch-all", "*"));

        var diagnostics = Sut.Validate(layout);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Validate_ValidOverrideWithVariableTemplate_NoDiagnostics()
    {
        var templateRoute = new RouteRuleDefinition
        {
            Id = "by-domain",
            Scope = RouteScope.Both,
            Order = 5,
            Destination = new DestinationTemplate
            {
                Directory = "${domain}",
                FileName = "${category}",
                Extension = ".md",
            },
        };
        var layout = MakeLayout(CoreRoute(), templateRoute);
        var diagnostics = Sut.Validate(layout);

        Assert.Empty(diagnostics);
    }

    // ── Diagnostics are sorted by code ────────────────────────────────────────

    [Fact]
    public void Validate_MultipleErrors_DiagnosticsSortedByCode()
    {
        // Layout with RS002 (dup) + RS004 (traversal); RS003 absent since RS001 route exists.
        var traversalRoute = new RouteRuleDefinition
        {
            Id = "dup",
            Scope = RouteScope.Both,
            Order = 5,
            Destination = new DestinationTemplate { Directory = "../escape", FileName = "file", Extension = ".md" },
        };
        var layout = MakeLayout(
            CoreRoute("core"),
            ExtraRoute("dup"),  // dup id with traversalRoute below
            traversalRoute);

        var diagnostics = Sut.Validate(layout);

        var codes = diagnostics.Select(d => d.Code).ToList();
        Assert.Equal(codes.OrderBy(c => c, StringComparer.Ordinal).ToList(), codes);
    }
}
