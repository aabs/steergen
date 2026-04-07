using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.PropertyTests.Generation;

/// <summary>
/// Property tests for <see cref="WritePlanBuilder"/>:
/// verifies that unresolved route results are never silently dropped
/// but are instead collected into an <c>other.md</c> fallback file.
/// </summary>
public sealed class WritePlanBuilderProperties
{
    // ── Property: all unresolved entries appear in other.md ──────────────────

    [Fact]
    public void Build_WithUnresolvedEntries_PlacesThemInOtherMd()
    {
        var resolutions = new List<RouteResolutionResult>
        {
            MakeUnresolved("SEC-001"),
            MakeUnresolved("API-001"),
            MakeUnresolved("MISC-001"),
        };

        var plan = new WritePlanBuilder().Build("test-target", resolutions);

        var otherFile = plan.Files.SingleOrDefault(f =>
            string.Equals(f.Path, WritePlanBuilder.FallbackOtherFile, StringComparison.Ordinal));

        Assert.NotNull(otherFile);
        Assert.Equal(3, otherFile.AppendUnits.Count);
        Assert.Contains(otherFile.AppendUnits, u => u.RuleId == "SEC-001");
        Assert.Contains(otherFile.AppendUnits, u => u.RuleId == "API-001");
        Assert.Contains(otherFile.AppendUnits, u => u.RuleId == "MISC-001");
    }

    // ── Property: no rule is silently dropped regardless of resolution state ──

    [Fact]
    public void Build_MixedResolutions_TotalRuleCountIsPreserved()
    {
        var resolved = new[]
        {
            MakeResolved("CORE-001", "output/core/rules.md"),
            MakeResolved("CORE-002", "output/core/rules.md"),
            MakeResolved("SEC-001", "output/security/rules.md"),
        };
        var unresolved = new[]
        {
            MakeUnresolved("API-001"),
            MakeUnresolved("MISC-001"),
        };

        var allResolutions = resolved.Concat(unresolved).ToList();
        var plan = new WritePlanBuilder().Build("test-target", allResolutions);

        var totalUnits = plan.Files.Sum(f => f.AppendUnits.Count);
        Assert.Equal(allResolutions.Count, totalUnits);
    }

    // ── Property: fully resolved resolutions produce no other.md ─────────────

    [Fact]
    public void Build_AllResolutionsResolved_NoOtherMdFile()
    {
        var resolutions = new List<RouteResolutionResult>
        {
            MakeResolved("CORE-001", "output/core/rules.md"),
            MakeResolved("CORE-002", "output/core/rules.md"),
            MakeResolved("SEC-001", "output/security/rules.md"),
        };

        var plan = new WritePlanBuilder().Build("test-target", resolutions);

        Assert.DoesNotContain(plan.Files, f =>
            string.Equals(f.Path, WritePlanBuilder.FallbackOtherFile, StringComparison.Ordinal));
    }

    // ── Property: all-unresolved produces exactly one other.md file ──────────

    [Fact]
    public void Build_AllUnresolved_ProducesExactlyOneOtherMdFile()
    {
        var ruleIds = Enumerable.Range(1, 10)
            .Select(i => $"RULE-{i:D3}")
            .ToList();
        var resolutions = ruleIds
            .Select(MakeUnresolved)
            .ToList();

        var plan = new WritePlanBuilder().Build("test-target", resolutions);

        Assert.Single(plan.Files);
        Assert.Equal(WritePlanBuilder.FallbackOtherFile, plan.Files[0].Path);
        Assert.Equal(ruleIds.Count, plan.Files[0].AppendUnits.Count);
    }

    // ── Property: other.md units are ordered deterministically by rule ID ─────

    [Fact]
    public void Build_UnresolvedEntries_AreOrderedByRuleIdInOtherMd()
    {
        // Deliberately supply rules out of alphabetical order
        var resolutions = new List<RouteResolutionResult>
        {
            MakeUnresolved("Z-RULE"),
            MakeUnresolved("A-RULE"),
            MakeUnresolved("M-RULE"),
        };

        var plan = new WritePlanBuilder().Build("test-target", resolutions);

        var otherFile = plan.Files.Single(f =>
            string.Equals(f.Path, WritePlanBuilder.FallbackOtherFile, StringComparison.Ordinal));

        var ids = otherFile.AppendUnits.Select(u => u.RuleId).ToList();
        Assert.Equal(["A-RULE", "M-RULE", "Z-RULE"], ids);
    }

    // ── Property: other.md is correctly sorted among resolved files ───────────

    [Fact]
    public void Build_MixedResolutions_FilesOrderedAlphabeticallyIncludingOtherMd()
    {
        var resolutions = new List<RouteResolutionResult>
        {
            MakeResolved("Z-001", "zzz/rules.md"),    // sorts after other.md
            MakeResolved("A-001", "aaa/rules.md"),    // sorts before other.md
            MakeUnresolved("MISC-001"),               // goes to other.md
        };

        var plan = new WritePlanBuilder().Build("test-target", resolutions);

        var paths = plan.Files.Select(f => f.Path).ToList();
        Assert.Equal(["aaa/rules.md", "other.md", "zzz/rules.md"], paths);
    }

    // ── Property: empty resolutions produce empty plan ────────────────────────

    [Fact]
    public void Build_EmptyResolutions_ProducesEmptyPlan()
    {
        var plan = new WritePlanBuilder().Build("test-target", []);

        Assert.Empty(plan.Files);
    }

    // ── Property: single unresolved rule is placed in other.md ───────────────

    [Fact]
    public void Build_SingleUnresolvedRule_PlacedInOtherMd()
    {
        var resolutions = new List<RouteResolutionResult>
        {
            MakeUnresolved("ORPHAN-001"),
        };

        var plan = new WritePlanBuilder().Build("test-target", resolutions);

        var file = Assert.Single(plan.Files);
        Assert.Equal(WritePlanBuilder.FallbackOtherFile, file.Path);
        var unit = Assert.Single(file.AppendUnits);
        Assert.Equal("ORPHAN-001", unit.RuleId);
    }

    // ── Property: determinism — same inputs produce identical other.md ─────────

    [Fact]
    public void Build_CalledTwice_ProducesIdenticalOtherMdContent()
    {
        var resolutions = new List<RouteResolutionResult>
        {
            MakeResolved("CORE-001", "output/core.md"),
            MakeUnresolved("SEC-001"),
            MakeUnresolved("API-001"),
        };

        var builder = new WritePlanBuilder();
        var plan1 = builder.Build("test-target", resolutions);
        var plan2 = builder.Build("test-target", resolutions);

        var other1 = plan1.Files.Single(f => f.Path == WritePlanBuilder.FallbackOtherFile);
        var other2 = plan2.Files.Single(f => f.Path == WritePlanBuilder.FallbackOtherFile);

        Assert.Equal(
            other1.AppendUnits.Select(u => u.RuleId),
            other2.AppendUnits.Select(u => u.RuleId));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static RouteResolutionResult MakeUnresolved(string ruleId) =>
        new()
        {
            RuleId = ruleId,
            MatchedRouteIds = [],
            SelectedRouteId = null,
            SelectedDestinationPath = null,
            SelectionReason = "No routes matched.",
            Source = RouteProvenance.Default,
        };

    private static RouteResolutionResult MakeResolved(string ruleId, string destination) =>
        new()
        {
            RuleId = ruleId,
            MatchedRouteIds = ["route-1"],
            SelectedRouteId = "route-1",
            SelectedDestinationPath = destination,
            SelectionReason = "Matched route-1.",
            Source = RouteProvenance.Default,
        };
}
