using BenchmarkDotNet.Attributes;
using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Benchmarks;

/// <summary>
/// Benchmarks for the layout routing engine: 1,000 rules routed across 4 built-in targets.
/// Validates the NFR-003 performance budget: routing 1,000 rules across 4 targets in under 5 seconds.
/// </summary>
[MemoryDiagnoser]
public class LayoutRoutingBenchmarks
{
    private const int RuleCount = 1_000;
    private static readonly string[] Domains = ["core", "security", "performance", "observability", "compliance", "frontend", "infrastructure"];
    private static readonly string[] Categories = ["quality", "security", "performance", "observability", "compliance", "accessibility", "build"];
    private static readonly string[] TargetIds = ["speckit", "kiro", "copilot-agent", "kiro-agent"];

    private SteeringRule[] _rules = [];
    private TargetLayoutDefinition _layout = new();
    private readonly RouteResolver _resolver = new();
    private readonly RoutePlanner _planner = new();
    private readonly WritePlanBuilder _builder = new();

    [GlobalSetup]
    public void Setup()
    {
        _rules = BuildRules(RuleCount);
        _layout = BuildLayout();
    }

    /// <summary>
    /// Resolve a single rule against a 10-route layout (hot path per rule).
    /// </summary>
    [Benchmark]
    public RouteResolutionResult ResolveSingleRule() =>
        _resolver.Resolve(_rules[0], _layout);

    /// <summary>
    /// Plan 1,000 rules against one target layout — the core routing hot path.
    /// </summary>
    [Benchmark]
    public IReadOnlyList<RouteResolutionResult> PlanThousandRules() =>
        _planner.Plan(_rules, _layout);

    /// <summary>
    /// Build a write plan from 1,000 resolved rules (grouping and ordering phase).
    /// </summary>
    [Benchmark]
    public WritePlan BuildWritePlan()
    {
        var resolutions = _planner.Plan(_rules, _layout);
        return _builder.Build("speckit", resolutions);
    }

    /// <summary>
    /// Route 1,000 rules through all 4 built-in targets (full NFR-003 scenario).
    /// </summary>
    [Benchmark]
    public WritePlan[] RouteThroughAllTargets()
    {
        var plans = new WritePlan[TargetIds.Length];
        for (int i = 0; i < TargetIds.Length; i++)
        {
            var resolutions = _planner.Plan(_rules, _layout);
            plans[i] = _builder.Build(TargetIds[i], resolutions);
        }
        return plans;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static SteeringRule[] BuildRules(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new SteeringRule
            {
                Id = $"BENCH-{i:D4}",
                Severity = "info",
                Domain = Domains[i % Domains.Length],
                Category = Categories[i % Categories.Length],
            })
            .ToArray();

    private static TargetLayoutDefinition BuildLayout() => new()
    {
        TargetId = "speckit",
        Version = "1.0",
        Roots = new LayoutRootsDefinition
        {
            GlobalRoot = "/tmp/global",
            ProjectRoot = "/tmp/project",
            TargetRoot = "/tmp/project/.speckit",
        },
        Routes =
        [
            new RouteRuleDefinition
            {
                Id = "core-global",
                Scope = RouteScope.Global,
                Explicit = true,
                Anchor = RouteAnchor.Core,
                Order = 10,
                Match = new RouteMatchExpression { Domain = ["core"] },
                Destination = new DestinationTemplate { Directory = "/tmp/global/.speckit", FileName = "constitution", Extension = ".md" },
            },
            new RouteRuleDefinition
            {
                Id = "security-global",
                Scope = RouteScope.Global,
                Explicit = true,
                Order = 20,
                Match = new RouteMatchExpression { Domain = ["security"] },
                Destination = new DestinationTemplate { Directory = "/tmp/global/.speckit", FileName = "security", Extension = ".md" },
            },
            new RouteRuleDefinition
            {
                Id = "catch-all-global",
                Scope = RouteScope.Global,
                Explicit = false,
                Order = 100,
                Match = new RouteMatchExpression { Domain = ["*"] },
                Destination = new DestinationTemplate { Directory = "/tmp/global/.speckit", FileName = "${domain}", Extension = ".md" },
            },
            new RouteRuleDefinition
            {
                Id = "core-project",
                Scope = RouteScope.Project,
                Explicit = true,
                Anchor = RouteAnchor.Core,
                Order = 10,
                Match = new RouteMatchExpression { Domain = ["core"] },
                Destination = new DestinationTemplate { Directory = "/tmp/project/.speckit", FileName = "constitution", Extension = ".md" },
            },
            new RouteRuleDefinition
            {
                Id = "security-project",
                Scope = RouteScope.Project,
                Explicit = true,
                Order = 20,
                Match = new RouteMatchExpression { Domain = ["security"] },
                Destination = new DestinationTemplate { Directory = "/tmp/project/.speckit", FileName = "security", Extension = ".md" },
            },
            new RouteRuleDefinition
            {
                Id = "performance-project",
                Scope = RouteScope.Project,
                Explicit = true,
                Order = 30,
                Match = new RouteMatchExpression { Domain = ["performance"] },
                Destination = new DestinationTemplate { Directory = "/tmp/project/.speckit", FileName = "performance", Extension = ".md" },
            },
            new RouteRuleDefinition
            {
                Id = "observability-project",
                Scope = RouteScope.Project,
                Explicit = true,
                Order = 40,
                Match = new RouteMatchExpression { Domain = ["observability"] },
                Destination = new DestinationTemplate { Directory = "/tmp/project/.speckit", FileName = "observability", Extension = ".md" },
            },
            new RouteRuleDefinition
            {
                Id = "compliance-project",
                Scope = RouteScope.Project,
                Explicit = true,
                Order = 50,
                Match = new RouteMatchExpression { Domain = ["compliance"] },
                Destination = new DestinationTemplate { Directory = "/tmp/project/.speckit", FileName = "compliance", Extension = ".md" },
            },
            new RouteRuleDefinition
            {
                Id = "catch-all-project",
                Scope = RouteScope.Project,
                Explicit = false,
                Order = 100,
                Match = new RouteMatchExpression { Domain = ["*"] },
                Destination = new DestinationTemplate { Directory = "/tmp/project/.speckit", FileName = "${domain}", Extension = ".md" },
            },
        ],
        Fallback = new FallbackRuleDefinition
        {
            Mode = FallbackMode.OtherAtCoreAnchor,
            FileBaseName = "other",
        },
    };
}
