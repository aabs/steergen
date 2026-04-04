using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.PropertyTests.Generation;

public sealed class SpeckitPartitionProperties
{
    private static SteeringRule MakeCoreRule(string id) =>
        new() { Id = id, Domain = "core", Severity = "info", PrimaryText = $"Core rule {id}." };

    private static SteeringRule MakeDomainRule(string id, string domain) =>
        new() { Id = id, Domain = domain, Severity = "info", PrimaryText = $"Rule {id} in {domain}." };

    [Fact]
    public void Partition_AllCoreRules_YieldsOnlyCorePartition()
    {
        var partitioner = new CoreGuidancePartitioner();
        var rules = new[]
        {
            MakeCoreRule("CORE-001"),
            MakeCoreRule("CORE-002"),
            MakeCoreRule("CORE-003"),
        };

        var result = partitioner.Partition(rules);

        Assert.Equal(3, result.CoreRules.Count);
        Assert.Empty(result.DomainModules);
    }

    [Fact]
    public void Partition_AllDomainRules_YieldsOnlyDomainModules()
    {
        var partitioner = new CoreGuidancePartitioner();
        var rules = new[]
        {
            MakeDomainRule("API-001", "api"),
            MakeDomainRule("API-002", "api"),
            MakeDomainRule("SEC-001", "security"),
        };

        var result = partitioner.Partition(rules);

        Assert.Empty(result.CoreRules);
        Assert.Equal(2, result.DomainModules.Count);
        Assert.Contains("api", result.DomainModules.Keys);
        Assert.Contains("security", result.DomainModules.Keys);
    }

    [Fact]
    public void Partition_MixedRules_CoreRulesNeverAppearInDomainModules()
    {
        var partitioner = new CoreGuidancePartitioner();
        var rules = new[]
        {
            MakeCoreRule("CORE-001"),
            MakeCoreRule("CORE-002"),
            MakeDomainRule("API-001", "api"),
            MakeDomainRule("SEC-001", "security"),
        };

        var result = partitioner.Partition(rules);

        var domainRuleIds = result.DomainModules.Values
            .SelectMany(r => r)
            .Select(r => r.Id)
            .ToHashSet();

        Assert.DoesNotContain("CORE-001", domainRuleIds);
        Assert.DoesNotContain("CORE-002", domainRuleIds);
    }

    [Fact]
    public void Partition_MixedRules_AllRulesAccountedFor()
    {
        var partitioner = new CoreGuidancePartitioner();
        var rules = new[]
        {
            MakeCoreRule("CORE-001"),
            MakeDomainRule("API-001", "api"),
            MakeDomainRule("API-002", "api"),
            MakeDomainRule("SEC-001", "security"),
        };

        var result = partitioner.Partition(rules);

        var coreIds = result.CoreRules.Select(r => r.Id).ToHashSet();
        var domainIds = result.DomainModules.Values
            .SelectMany(r => r)
            .Select(r => r.Id)
            .ToHashSet();

        var allPartitionedIds = coreIds.Union(domainIds).OrderBy(id => id).ToList();
        var inputIds = rules.Select(r => r.Id).OrderBy(id => id).ToList();

        Assert.Equal(inputIds, allPartitionedIds);
    }

    [Fact]
    public void Partition_SameInputTwice_YieldsDeterministicOrdering()
    {
        var partitioner = new CoreGuidancePartitioner();
        var rules = new[]
        {
            MakeCoreRule("CORE-003"),
            MakeCoreRule("CORE-001"),
            MakeCoreRule("CORE-002"),
            MakeDomainRule("API-002", "api"),
            MakeDomainRule("API-001", "api"),
        };

        var r1 = partitioner.Partition(rules);
        var r2 = partitioner.Partition(rules);

        Assert.Equal(r1.CoreRules.Select(r => r.Id), r2.CoreRules.Select(r => r.Id));
        Assert.Equal(
            r1.DomainModules["api"].Select(r => r.Id),
            r2.DomainModules["api"].Select(r => r.Id));
    }

    [Fact]
    public void Partition_CoreRules_AreOrderedById()
    {
        var partitioner = new CoreGuidancePartitioner();
        var rules = new[]
        {
            MakeCoreRule("CORE-003"),
            MakeCoreRule("CORE-001"),
            MakeCoreRule("CORE-002"),
        };

        var result = partitioner.Partition(rules);

        var ids = result.CoreRules.Select(r => r.Id).ToList();
        Assert.Equal(["CORE-001", "CORE-002", "CORE-003"], ids);
    }

    [Fact]
    public void Partition_DomainRules_AreOrderedByIdWithinDomain()
    {
        var partitioner = new CoreGuidancePartitioner();
        var rules = new[]
        {
            MakeDomainRule("API-003", "api"),
            MakeDomainRule("API-001", "api"),
            MakeDomainRule("API-002", "api"),
        };

        var result = partitioner.Partition(rules);

        var ids = result.DomainModules["api"].Select(r => r.Id).ToList();
        Assert.Equal(["API-001", "API-002", "API-003"], ids);
    }

    [Fact]
    public void Partition_ConstitutionContainsNoDomainRules_Invariant()
    {
        var partitioner = new CoreGuidancePartitioner();
        // Generate a larger mix
        var rulesList = new List<SteeringRule>();
        for (var i = 1; i <= 10; i++)
            rulesList.Add(MakeCoreRule($"CORE-{i:D3}"));
        for (var i = 1; i <= 5; i++)
            rulesList.Add(MakeDomainRule($"API-{i:D3}", "api"));
        for (var i = 1; i <= 5; i++)
            rulesList.Add(MakeDomainRule($"OBS-{i:D3}", "observability"));

        var result = partitioner.Partition(rulesList);

        Assert.All(result.CoreRules, r =>
            Assert.Equal("core", r.Domain, StringComparer.OrdinalIgnoreCase));

        Assert.All(result.DomainModules, kvp =>
            Assert.All(kvp.Value, r =>
                Assert.False(string.Equals(r.Domain, "core", StringComparison.OrdinalIgnoreCase))));
    }
}
