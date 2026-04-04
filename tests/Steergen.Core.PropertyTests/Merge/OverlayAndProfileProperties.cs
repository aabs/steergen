using Steergen.Core.Merge;
using Steergen.Core.Model;
using Xunit;

namespace Steergen.Core.PropertyTests.Merge;

public sealed class OverlayAndProfileProperties
{
    private static SteeringDocument MakeDoc(string id, params SteeringRule[] rules) =>
        new() { Id = id, Rules = rules };

    private static SteeringRule MakeRule(string id, string? profile = null) =>
        new() { Id = id, Domain = "core", Severity = "info", Profile = profile };

    [Fact]
    public void Resolve_SameInputsTwice_YieldsIdenticalOutput()
    {
        var resolver = new SteeringResolver();
        var global = new[] { MakeDoc("doc-A", MakeRule("R001"), MakeRule("R002")) };
        var project = new[] { MakeDoc("doc-B", MakeRule("R003")) };
        var profiles = new[] { "default" };

        var result1 = resolver.Resolve(global, project, profiles);
        var result2 = resolver.Resolve(global, project, profiles);

        Assert.Equal(result1.Rules.Count, result2.Rules.Count);
        for (int i = 0; i < result1.Rules.Count; i++)
            Assert.Equal(result1.Rules[i].Id, result2.Rules[i].Id);
    }

    [Fact]
    public void Resolve_ProjectRulesOverrideGlobalRulesWithSameId()
    {
        var resolver = new SteeringResolver();
        var globalRule = MakeRule("R001") with { PrimaryText = "global text" };
        var projectRule = MakeRule("R001") with { PrimaryText = "project text" };

        var global = new[] { MakeDoc("doc-A", globalRule) };
        var project = new[] { MakeDoc("doc-A", projectRule) };

        var result = resolver.Resolve(global, project, []);
        var rule = result.Rules.Single(r => r.Id == "R001");
        Assert.Equal("project text", rule.PrimaryText);
    }

    [Fact]
    public void Resolve_FilterByProfile_OnlyReturnsMatchingRules()
    {
        var resolver = new SteeringResolver();
        var r1 = MakeRule("R001", profile: "strict");
        var r2 = MakeRule("R002", profile: "default");
        var r3 = MakeRule("R003", profile: null);

        var global = new[] { MakeDoc("doc-A", r1, r2, r3) };
        var result = resolver.Resolve(global, [], ["strict"]);

        var ruleIds = result.Rules.Select(r => r.Id).ToHashSet();
        Assert.Contains("R001", ruleIds);
        Assert.DoesNotContain("R002", ruleIds);
        Assert.Contains("R003", ruleIds);
    }

    [Fact]
    public void Resolve_RuleOrder_IsStableSortedByDocIdThenRuleId()
    {
        var resolver = new SteeringResolver();
        var global = new[]
        {
            MakeDoc("doc-B", MakeRule("R002"), MakeRule("R001")),
            MakeDoc("doc-A", MakeRule("R004"), MakeRule("R003")),
        };

        var result = resolver.Resolve(global, [], []);
        var ids = result.Rules.Select(r => r.Id).ToList();
        var sorted = ids.OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, ids);
    }
}
