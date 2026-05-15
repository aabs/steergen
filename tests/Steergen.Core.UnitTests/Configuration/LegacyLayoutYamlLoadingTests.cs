using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Xunit;

namespace Steergen.Core.UnitTests.Configuration;

/// <summary>
/// Unit tests verifying that legacy layout YAML fields (match.domain, match.severity,
/// match.profile) load without error and are silently discarded, and that the new
/// match.mandatory field maps correctly to <see cref="RouteMatchExpression.Mandatory"/>.
/// </summary>
/// <remarks>
/// Example-based tests are used here because the scenarios are specific backward-compatibility
/// contracts with exact expected outcomes, not broad input-space invariants.
/// </remarks>
public sealed class LegacyLayoutYamlLoadingTests
{
    private static readonly string TempDir = Path.Combine(
        AppContext.BaseDirectory, "testdata", "legacy-layout");

    private static string WriteTempYaml(string content)
    {
        Directory.CreateDirectory(TempDir);
        var path = Path.Combine(TempDir, Guid.NewGuid() + ".yaml");
        File.WriteAllText(path, content);
        return path;
    }

    // ── Legacy match.domain loads without error ──────────────────────────────

    [Fact]
    public async Task LoadAsync_MatchDomain_LoadsWithoutError()
    {
        var yaml = """
            routes:
              - id: legacy-domain-route
                scope: global
                order: 10
                match:
                  domain: core
                destination:
                  directory: "${targetRoot}"
                  fileName: "output"
                  extension: ".md"
            """;
        var overridePath = WriteTempYaml(yaml);

        var loader = new LayoutOverrideLoader();
        var layout = await loader.LoadAsync("speckit", overridePath);

        Assert.Single(layout.Routes);
        Assert.Equal("legacy-domain-route", layout.Routes[0].Id);
        // Domain is silently discarded — RouteMatchExpression has no Domain field
        Assert.True(layout.Routes[0].Match.IsEmpty);
    }

    // ── Legacy match.severity loads without error ────────────────────────────

    [Fact]
    public async Task LoadAsync_MatchSeverity_LoadsWithoutError()
    {
        var yaml = """
            routes:
              - id: legacy-severity-route
                scope: project
                order: 20
                match:
                  severity: error
                destination:
                  directory: "${projectRoot}"
                  fileName: "errors"
                  extension: ".md"
            """;
        var overridePath = WriteTempYaml(yaml);

        var loader = new LayoutOverrideLoader();
        var layout = await loader.LoadAsync("speckit", overridePath);

        Assert.Single(layout.Routes);
        Assert.Equal("legacy-severity-route", layout.Routes[0].Id);
        // Severity is silently discarded — no Severity field on RouteMatchExpression
        Assert.True(layout.Routes[0].Match.IsEmpty);
    }

    // ── Legacy match.profile loads without error ─────────────────────────────

    [Fact]
    public async Task LoadAsync_MatchProfile_LoadsWithoutError()
    {
        var yaml = """
            routes:
              - id: legacy-profile-route
                scope: both
                order: 30
                match:
                  profile: strict
                destination:
                  directory: "${targetRoot}"
                  fileName: "strict-rules"
                  extension: ".md"
            """;
        var overridePath = WriteTempYaml(yaml);

        var loader = new LayoutOverrideLoader();
        var layout = await loader.LoadAsync("speckit", overridePath);

        Assert.Single(layout.Routes);
        Assert.Equal("legacy-profile-route", layout.Routes[0].Id);
        // Profile is silently discarded — no Profile field on RouteMatchExpression
        Assert.True(layout.Routes[0].Match.IsEmpty);
    }

    // ── match.mandatory: true maps to RouteMatchExpression.Mandatory == true ─

    [Fact]
    public async Task LoadAsync_MatchMandatoryTrue_MapsToMandatoryTrue()
    {
        var yaml = """
            routes:
              - id: mandatory-route
                scope: global
                order: 50
                match:
                  mandatory: true
                  category: security
                destination:
                  directory: "${targetRoot}"
                  fileName: "mandatory-rules"
                  extension: ".md"
            """;
        var overridePath = WriteTempYaml(yaml);

        var loader = new LayoutOverrideLoader();
        var layout = await loader.LoadAsync("speckit", overridePath);

        Assert.Single(layout.Routes);
        Assert.Equal("mandatory-route", layout.Routes[0].Id);
        Assert.Equal(true, layout.Routes[0].Match.Mandatory);
    }

    // ── match.mandatory: false maps to RouteMatchExpression.Mandatory == false

    [Fact]
    public async Task LoadAsync_MatchMandatoryFalse_MapsToMandatoryFalse()
    {
        var yaml = """
            routes:
              - id: advisory-route
                scope: project
                order: 60
                match:
                  mandatory: false
                  category: guidance
                destination:
                  directory: "${projectRoot}"
                  fileName: "advisory-rules"
                  extension: ".md"
            """;
        var overridePath = WriteTempYaml(yaml);

        var loader = new LayoutOverrideLoader();
        var layout = await loader.LoadAsync("speckit", overridePath);

        Assert.Single(layout.Routes);
        Assert.Equal("advisory-route", layout.Routes[0].Id);
        Assert.Equal(false, layout.Routes[0].Match.Mandatory);
    }

    // ── Absent match.mandatory maps to RouteMatchExpression.Mandatory == null ─

    [Fact]
    public async Task LoadAsync_MatchMandatoryAbsent_MapsToMandatoryNull()
    {
        var yaml = """
            routes:
              - id: no-mandatory-route
                scope: global
                order: 70
                match:
                  category: "*"
                destination:
                  directory: "${targetRoot}"
                  fileName: "all-rules"
                  extension: ".md"
            """;
        var overridePath = WriteTempYaml(yaml);

        var loader = new LayoutOverrideLoader();
        var layout = await loader.LoadAsync("speckit", overridePath);

        Assert.Single(layout.Routes);
        Assert.Equal("no-mandatory-route", layout.Routes[0].Id);
        Assert.Null(layout.Routes[0].Match.Mandatory);
    }
}
