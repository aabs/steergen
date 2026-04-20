using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Validation;

namespace Steergen.Core.UnitTests.Generation;

/// <summary>
/// Tests that <see cref="RoutingSchemaValidator"/> rejects unsafe destination paths
/// and accepts valid root-bounded paths.
/// </summary>
public sealed class RoutePathSafetyTests
{
    private static RoutingSchemaValidator Sut => new();

    private static TargetLayoutDefinition MakeLayout(params RouteRuleDefinition[] routes) =>
        new()
        {
            TargetId = "speckit",
            Routes = routes.ToList(),
            Fallback = new FallbackRuleDefinition { Mode = FallbackMode.OtherAtCoreAnchor },
        };

    private static RouteRuleDefinition CoreRoute(string directory = "rules", string fileName = "core.md") =>
        new()
        {
            Id = "core-route",
            Scope = RouteScope.Both,
            Anchor = RouteAnchor.Core,
            Order = 0,
            Destination = new DestinationTemplate { Directory = directory, FileName = fileName },
        };

    private static RouteRuleDefinition ExtraRoute(string id, string directory, string fileName) =>
        new()
        {
            Id = id,
            Scope = RouteScope.Both,
            Order = 1,
            Destination = new DestinationTemplate { Directory = directory, FileName = fileName },
        };

    // ── Path traversal ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("../escape")]
    [InlineData("../..")]
    [InlineData("safe/../../../escape")]
    [InlineData("valid/../../bad")]
    public void Validate_DestinationDirectoryWithTraversal_ProducesError(string directory)
    {
        var layout = MakeLayout(
            CoreRoute(),
            ExtraRoute("bad-route", directory, "file.md"));

        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics,
            d => d.Severity == DiagnosticSeverity.Error && d.Code == "RS004");
    }

    [Theory]
    [InlineData("/absolute/path")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32")]
    public void Validate_DestinationDirectoryAbsolutePath_ProducesError(string directory)
    {
        var layout = MakeLayout(
            CoreRoute(),
            ExtraRoute("bad-route", directory, "file.md"));

        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics,
            d => d.Severity == DiagnosticSeverity.Error && d.Code == "RS005");
    }

    [Theory]
    [InlineData("file/with/slash.md")]
    [InlineData("dir\\file.md")]
    [InlineData("nested/name.md")]
    public void Validate_FileNameWithPathSeparator_ProducesError(string fileName)
    {
        var layout = MakeLayout(
            CoreRoute(),
            ExtraRoute("bad-route", "rules", fileName));

        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics,
            d => d.Severity == DiagnosticSeverity.Error && d.Code == "RS006");
    }

    // ── Valid paths ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("rules")]
    [InlineData("rules/security")]
    [InlineData("")]
    [InlineData("${targetRoot}/rules")]
    [InlineData("${profileRoot}/rules")]
    [InlineData("${tempRoot}/rules")]
    public void Validate_ValidRelativeDirectory_NoPathError(string directory)
    {
        var layout = MakeLayout(CoreRoute(directory, "core.md"));

        var diagnostics = Sut.Validate(layout);

        Assert.DoesNotContain(diagnostics,
            d => d.Code is "RS004" or "RS005" or "RS006");
    }

    [Theory]
    [InlineData("core.md")]
    [InlineData("rules.md")]
    [InlineData("${domain}.md")]
    public void Validate_ValidFileName_NoPathError(string fileName)
    {
        var layout = MakeLayout(CoreRoute("rules", fileName));

        var diagnostics = Sut.Validate(layout);

        Assert.DoesNotContain(diagnostics,
            d => d.Code is "RS004" or "RS005" or "RS006");
    }

    // ── Structural validation ─────────────────────────────────────────────────

    [Fact]
    public void Validate_EmptyRoutes_ProducesError()
    {
        var layout = MakeLayout(/* no routes */);

        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics,
            d => d.Severity == DiagnosticSeverity.Error && d.Code == "RS001");
    }

    [Fact]
    public void Validate_DuplicateRouteIds_ProducesError()
    {
        var layout = MakeLayout(
            CoreRoute(),
            new RouteRuleDefinition
            {
                Id = "core-route",
                Scope = RouteScope.Both,
                Order = 1,
                Destination = new DestinationTemplate { Directory = "rules", FileName = "other.md" },
            });

        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics,
            d => d.Severity == DiagnosticSeverity.Error && d.Code == "RS002");
    }

    [Fact]
    public void Validate_NoCoreAnchorRoute_ProducesError()
    {
        var layout = MakeLayout(
            new RouteRuleDefinition
            {
                Id = "no-anchor",
                Scope = RouteScope.Both,
                Anchor = RouteAnchor.None,
                Order = 0,
                Destination = new DestinationTemplate { Directory = "rules", FileName = "rules.md" },
            });

        var diagnostics = Sut.Validate(layout);

        Assert.Contains(diagnostics,
            d => d.Severity == DiagnosticSeverity.Error && d.Code == "RS003");
    }

    [Fact]
    public void Validate_ValidLayout_NoDiagnostics()
    {
        var layout = MakeLayout(
            CoreRoute(),
            ExtraRoute("extra", "rules/security", "security.md"));

        var diagnostics = Sut.Validate(layout);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Validate_ValidLayoutWithVariablesInPaths_NoDiagnostics()
    {
        var layout = MakeLayout(
            new RouteRuleDefinition
            {
                Id = "core-route",
                Scope = RouteScope.Both,
                Anchor = RouteAnchor.Core,
                Order = 0,
                Destination = new DestinationTemplate
                {
                    Directory = "${targetRoot}/rules/${domain}",
                    FileName = "${category}.md",
                },
            });

        var diagnostics = Sut.Validate(layout);

        Assert.DoesNotContain(diagnostics,
            d => d.Code is "RS004" or "RS005" or "RS006");
    }
}
