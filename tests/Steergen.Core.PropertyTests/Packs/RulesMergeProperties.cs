using CsCheck;
using Steergen.Core.Merge;
using Steergen.Core.Model;
using Steergen.Core.Packs;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for rules merge with scope-based precedence and source tagging.
///
/// Property 9: Rules Merge with Scope-Based Precedence
/// For any set of rules from project-local sources and rules packs at various scopes,
/// the merge SHALL resolve duplicate rule IDs by selecting the rule from the
/// highest-precedence source, where precedence is:
/// project-local > project-scoped packs > supplemental-scoped packs > global-scoped packs.
/// Within the same scope level, for any two packs declaring the same rule ID, the rule
/// from the pack declared earlier in the rulesPacks list SHALL win. When a consumer scope
/// override is specified, the merge SHALL use the overridden scope instead of the
/// manifest-declared scope.
///
/// **Validates: Requirements 10.3, 10.4, 10.5, 10.6, 11.5, 11.7**
///
/// Property 10: Rule Source Tagging
/// For any rule loaded from a rules pack, the resolved rule SHALL carry a
/// SourcePackName equal to the pack's manifest name field and a SourcePackScope
/// equal to the effective scope used during merge.
///
/// **Validates: Requirements 11.6**
/// </summary>
public sealed class RulesMergeProperties
{
    // ── Generators ───────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenAlphaString =
        Gen.String[Gen.Char['a', 'z'], 2, 10];

    private static readonly Gen<string> GenPackName =
        Gen.Select(GenAlphaString, GenAlphaString)
           .Select((prefix, suffix) => $"{prefix}-{suffix}-rules");

    private static readonly Gen<string> GenRuleId =
        Gen.Select(GenAlphaString, Gen.Int[1, 999])
           .Select((prefix, num) => $"{prefix}-{num:D3}");

    private static readonly Gen<PackScope> GenPackScope =
        Gen.OneOf(
            Gen.Const<PackScope>(PackScope.Global),
            Gen.Const<PackScope>(PackScope.Supplemental),
            Gen.Const<PackScope>(PackScope.Project));

    private static readonly Gen<PackScope?> GenOptionalScopeOverride =
        Gen.Frequency(
            (3, Gen.Const((PackScope?)null)),
            (1, GenPackScope.Select(s => (PackScope?)s)));

    private static readonly Gen<string> GenRuleText =
        Gen.String[Gen.Char.AlphaNumeric, 10, 50];

    /// <summary>
    /// Represents a simulated rules pack with manifest metadata and rules.
    /// </summary>
    private sealed record TestRulesPack(
        string ManifestName,
        PackScope ManifestScope,
        PackScope? ConsumerScopeOverride,
        IReadOnlyList<SteeringRule> Rules);

    /// <summary>
    /// Generates a single rule with a unique ID and text content.
    /// </summary>
    private static readonly Gen<SteeringRule> GenRule =
        Gen.Select(GenRuleId, GenRuleText)
           .Select((id, text) => new SteeringRule
           {
               Id = id,
               PrimaryText = text
           });

    /// <summary>
    /// Generates a test rules pack with 1-5 rules, a manifest name, scope, and optional override.
    /// </summary>
    private static readonly Gen<TestRulesPack> GenTestPack =
        Gen.Select(GenPackName, GenPackScope, GenOptionalScopeOverride, GenRule.Array[1, 5])
           .Select((name, scope, overrideScope, rules) =>
               new TestRulesPack(name, scope, overrideScope, rules.ToList()));

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the effective scope for a pack, applying consumer override if present.
    /// This mirrors the logic that RulesPackLoader must implement:
    /// effective scope = ScopeOverride ?? manifest.Scope
    /// </summary>
    private static PackScope GetEffectiveScope(TestRulesPack pack) =>
        pack.ConsumerScopeOverride ?? pack.ManifestScope;

    /// <summary>
    /// Tags rules with source pack metadata, simulating what RulesPackLoader must do.
    /// Each rule gets SourcePackName = manifest name and SourcePackScope = effective scope.
    /// </summary>
    private static IReadOnlyList<SteeringRule> TagRulesFromPack(TestRulesPack pack)
    {
        var effectiveScope = GetEffectiveScope(pack);
        return pack.Rules
            .Select(r => r with
            {
                SourcePackName = pack.ManifestName,
                SourcePackScope = effectiveScope
            })
            .ToList();
    }

    // ── Property 10: Rule Source Tagging ─────────────────────────────────────────

    [Fact]
    public void TaggedRules_CarryCorrectSourcePackName_FromManifest()
    {
        // **Validates: Requirements 11.6**
        //
        // For any rule loaded from a rules pack, the resolved rule SHALL carry
        // a SourcePackName equal to the pack's manifest name field.
        GenTestPack.Sample(
            pack =>
            {
                var taggedRules = TagRulesFromPack(pack);

                foreach (var rule in taggedRules)
                {
                    Assert.Equal(pack.ManifestName, rule.SourcePackName);
                }
            },
            iter: 100,
            print: pack => $"pack={pack.ManifestName}, rules={pack.Rules.Count}");
    }

    [Fact]
    public void TaggedRules_CarryCorrectSourcePackScope_AsEffectiveScope()
    {
        // **Validates: Requirements 11.6**
        //
        // For any rule loaded from a rules pack, the resolved rule SHALL carry
        // a SourcePackScope equal to the effective scope used during merge.
        // Effective scope = consumer override ?? manifest scope.
        GenTestPack.Sample(
            pack =>
            {
                var expectedScope = GetEffectiveScope(pack);
                var taggedRules = TagRulesFromPack(pack);

                foreach (var rule in taggedRules)
                {
                    Assert.Equal(expectedScope, rule.SourcePackScope);
                }
            },
            iter: 100,
            print: pack => $"pack={pack.ManifestName}, manifestScope={pack.ManifestScope}, override={pack.ConsumerScopeOverride}, effective={GetEffectiveScope(pack)}");
    }

    [Fact]
    public void TaggedRules_ConsumerScopeOverride_TakesPrecedenceOverManifestScope()
    {
        // **Validates: Requirements 11.6**
        //
        // When a consumer scope override is specified, the effective scope used
        // for tagging SHALL be the override, not the manifest-declared scope.
        Gen.Select(GenPackName, GenPackScope, GenPackScope, GenRule.Array[1, 5])
           .Where(t => t.Item2 != t.Item3) // Ensure override differs from manifest
           .Sample(
               (packName, manifestScope, overrideScope, rules) =>
               {
                   var pack = new TestRulesPack(packName, manifestScope, overrideScope, rules.ToList());
                   var taggedRules = TagRulesFromPack(pack);

                   foreach (var rule in taggedRules)
                   {
                       Assert.Equal(overrideScope, rule.SourcePackScope);
                       Assert.NotEqual(manifestScope, rule.SourcePackScope);
                   }
               },
               iter: 100,
               print: t => $"pack={t.Item1}, manifest={t.Item2}, override={t.Item3}");
    }

    [Fact]
    public void TaggedRules_WithoutScopeOverride_UseManifestScope()
    {
        // **Validates: Requirements 11.6**
        //
        // When no consumer scope override is specified (null), the effective scope
        // used for tagging SHALL be the manifest-declared scope.
        Gen.Select(GenPackName, GenPackScope, GenRule.Array[1, 5])
           .Sample(
               (packName, manifestScope, rules) =>
               {
                   var pack = new TestRulesPack(packName, manifestScope, null, rules.ToList());
                   var taggedRules = TagRulesFromPack(pack);

                   foreach (var rule in taggedRules)
                   {
                       Assert.Equal(manifestScope, rule.SourcePackScope);
                   }
               },
               iter: 100,
               print: t => $"pack={t.Item1}, scope={t.Item2}, rules={t.Item3.Length}");
    }

    [Fact]
    public void TaggedRules_FromMultiplePacks_EachCarryOwnPackMetadata()
    {
        // **Validates: Requirements 11.6**
        //
        // For any set of rules loaded from multiple packs, each rule SHALL carry
        // the SourcePackName and SourcePackScope of its own originating pack,
        // not any other pack's metadata.
        GenTestPack.Array[2, 5]
            .Where(packs => packs.Select(p => p.ManifestName).Distinct().Count() == packs.Length) // unique names
            .Sample(
                packs =>
                {
                    var allTaggedRules = new List<(TestRulesPack Pack, SteeringRule Rule)>();

                    foreach (var pack in packs)
                    {
                        var tagged = TagRulesFromPack(pack);
                        foreach (var rule in tagged)
                        {
                            allTaggedRules.Add((pack, rule));
                        }
                    }

                    foreach (var (pack, rule) in allTaggedRules)
                    {
                        var expectedScope = GetEffectiveScope(pack);
                        Assert.Equal(pack.ManifestName, rule.SourcePackName);
                        Assert.Equal(expectedScope, rule.SourcePackScope);
                    }
                },
                iter: 100,
                print: packs => $"packs={packs.Length}, totalRules={packs.Sum(p => p.Rules.Count)}");
    }

    [Fact]
    public void TaggedRules_PreserveOriginalRuleFields_AfterTagging()
    {
        // **Validates: Requirements 11.6**
        //
        // Tagging a rule with source pack metadata SHALL NOT alter any other
        // fields of the rule (Id, PrimaryText, Category, etc.).
        GenTestPack.Sample(
            pack =>
            {
                var taggedRules = TagRulesFromPack(pack);

                for (var i = 0; i < pack.Rules.Count; i++)
                {
                    var original = pack.Rules[i];
                    var tagged = taggedRules[i];

                    // Original fields preserved
                    Assert.Equal(original.Id, tagged.Id);
                    Assert.Equal(original.PrimaryText, tagged.PrimaryText);
                    Assert.Equal(original.Category, tagged.Category);
                    Assert.Equal(original.Tags, tagged.Tags);
                    Assert.Equal(original.AppliesTo, tagged.AppliesTo);
                    Assert.Equal(original.Mandatory, tagged.Mandatory);
                    Assert.Equal(original.Deprecated, tagged.Deprecated);

                    // Source metadata added
                    Assert.Equal(pack.ManifestName, tagged.SourcePackName);
                    Assert.Equal(GetEffectiveScope(pack), tagged.SourcePackScope);
                }
            },
            iter: 100,
            print: pack => $"pack={pack.ManifestName}, rules={pack.Rules.Count}");
    }

    // ── Property 9: Rules Merge with Scope-Based Precedence ──────────────────────

    /// <summary>
    /// Creates a SteeringRule with the given ID and content, tagged with pack metadata.
    /// </summary>
    private static SteeringRule MakeMergeRule(string id, string content, string? packName = null, PackScope? packScope = null) =>
        new()
        {
            Id = id,
            PrimaryText = content,
            SourcePackName = packName,
            SourcePackScope = packScope
        };

    /// <summary>
    /// Creates a SteeringDocument containing the given rules.
    /// </summary>
    private static SteeringDocument MakeMergeDocument(string docId, IReadOnlyList<SteeringRule> rules) =>
        new()
        {
            Id = docId,
            Rules = rules
        };

    /// <summary>
    /// Returns the numeric precedence for a scope (higher = wins).
    /// Project-local is highest at 4.
    /// </summary>
    private static int ScopePrecedence(PackScope? scope) => scope switch
    {
        null => 4,                    // project-local (highest)
        PackScope.Project => 3,
        PackScope.Supplemental => 2,
        PackScope.Global => 1,
        _ => 0
    };

    [Fact]
    public void Merge_SelectsHighestPrecedenceSource_ForDuplicateRuleIds()
    {
        // **Validates: Requirements 10.3, 10.4, 11.5, 11.7**
        //
        // For any set of rules from project-local sources and rules packs at various
        // scopes, the merge resolves duplicate rule IDs by selecting the rule from
        // the highest-precedence source. Project-local always wins.
        Gen.Select(
            GenRuleId.Array[2, 6],
            GenRuleText,
            GenRuleText,
            GenRuleText,
            GenRuleText)
        .Sample(
            (ruleIds, localContent, projectContent, supplementalContent, globalContent) =>
            {
                var resolver = new SteeringResolver();

                // Create project-local documents with all rule IDs
                var localRules = ruleIds.Select(id => MakeMergeRule(id, $"local:{localContent}")).ToList();
                var localDoc = MakeMergeDocument("local-doc", localRules);

                // Create pack documents at each scope with the same rule IDs
                var globalRules = ruleIds.Select(id => MakeMergeRule(id, $"global:{globalContent}", "global-pack", PackScope.Global)).ToList();
                var globalDoc = MakeMergeDocument("global-doc", globalRules);

                var supplementalRules = ruleIds.Select(id => MakeMergeRule(id, $"supplemental:{supplementalContent}", "supp-pack", PackScope.Supplemental)).ToList();
                var supplementalDoc = MakeMergeDocument("supplemental-doc", supplementalRules);

                var projectScopeRules = ruleIds.Select(id => MakeMergeRule(id, $"project-pack:{projectContent}", "proj-pack", PackScope.Project)).ToList();
                var projectScopeDoc = MakeMergeDocument("project-scope-doc", projectScopeRules);

                var packDocuments = new List<ScopedPackDocuments>
                {
                    new() { Scope = PackScope.Global, Documents = [globalDoc] },
                    new() { Scope = PackScope.Supplemental, Documents = [supplementalDoc] },
                    new() { Scope = PackScope.Project, Documents = [projectScopeDoc] }
                };

                var result = resolver.Resolve(
                    projectDocuments: new[] { localDoc },
                    packDocuments: packDocuments,
                    activeProfiles: Array.Empty<string>());

                // Project-local rules should win over all pack rules
                foreach (var ruleId in ruleIds)
                {
                    var resolved = result.Rules.FirstOrDefault(r => r.Id == ruleId);
                    Assert.NotNull(resolved);
                    Assert.StartsWith($"local:{localContent}", resolved!.PrimaryText!);
                }
            },
            iter: 100,
            print: t => $"ruleIds=[{string.Join(",", t.Item1)}]");
    }

    [Fact]
    public void Merge_ProjectScopedPackWins_OverSupplementalAndGlobal()
    {
        // **Validates: Requirements 10.4, 11.5, 11.7**
        //
        // When project-local rules are absent, project-scoped pack rules
        // take precedence over supplemental and global pack rules.
        Gen.Select(
            GenRuleId.Array[2, 6],
            GenRuleText,
            GenRuleText,
            GenRuleText)
        .Sample(
            (ruleIds, projectContent, supplementalContent, globalContent) =>
            {
                var resolver = new SteeringResolver();

                var globalRules = ruleIds.Select(id => MakeMergeRule(id, $"global:{globalContent}", "global-pack", PackScope.Global)).ToList();
                var globalDoc = MakeMergeDocument("global-doc", globalRules);

                var supplementalRules = ruleIds.Select(id => MakeMergeRule(id, $"supplemental:{supplementalContent}", "supp-pack", PackScope.Supplemental)).ToList();
                var supplementalDoc = MakeMergeDocument("supplemental-doc", supplementalRules);

                var projectScopeRules = ruleIds.Select(id => MakeMergeRule(id, $"project-pack:{projectContent}", "proj-pack", PackScope.Project)).ToList();
                var projectScopeDoc = MakeMergeDocument("project-scope-doc", projectScopeRules);

                var packDocuments = new List<ScopedPackDocuments>
                {
                    new() { Scope = PackScope.Global, Documents = [globalDoc] },
                    new() { Scope = PackScope.Supplemental, Documents = [supplementalDoc] },
                    new() { Scope = PackScope.Project, Documents = [projectScopeDoc] }
                };

                var result = resolver.Resolve(
                    projectDocuments: Array.Empty<SteeringDocument>(),
                    packDocuments: packDocuments,
                    activeProfiles: Array.Empty<string>());

                // Project-scoped pack rules should win
                foreach (var ruleId in ruleIds)
                {
                    var resolved = result.Rules.FirstOrDefault(r => r.Id == ruleId);
                    Assert.NotNull(resolved);
                    Assert.StartsWith($"project-pack:{projectContent}", resolved!.PrimaryText!);
                }
            },
            iter: 100,
            print: t => $"ruleIds=[{string.Join(",", t.Item1)}]");
    }

    [Fact]
    public void Merge_SupplementalWins_OverGlobal()
    {
        // **Validates: Requirements 10.4, 11.5, 11.7**
        //
        // When neither project-local nor project-scoped pack rules exist,
        // supplemental-scoped pack rules take precedence over global.
        Gen.Select(
            GenRuleId.Array[2, 6],
            GenRuleText,
            GenRuleText)
        .Sample(
            (ruleIds, supplementalContent, globalContent) =>
            {
                var resolver = new SteeringResolver();

                var globalRules = ruleIds.Select(id => MakeMergeRule(id, $"global:{globalContent}", "global-pack", PackScope.Global)).ToList();
                var globalDoc = MakeMergeDocument("global-doc", globalRules);

                var supplementalRules = ruleIds.Select(id => MakeMergeRule(id, $"supplemental:{supplementalContent}", "supp-pack", PackScope.Supplemental)).ToList();
                var supplementalDoc = MakeMergeDocument("supplemental-doc", supplementalRules);

                var packDocuments = new List<ScopedPackDocuments>
                {
                    new() { Scope = PackScope.Global, Documents = [globalDoc] },
                    new() { Scope = PackScope.Supplemental, Documents = [supplementalDoc] }
                };

                var result = resolver.Resolve(
                    projectDocuments: Array.Empty<SteeringDocument>(),
                    packDocuments: packDocuments,
                    activeProfiles: Array.Empty<string>());

                // Supplemental rules should win over global
                foreach (var ruleId in ruleIds)
                {
                    var resolved = result.Rules.FirstOrDefault(r => r.Id == ruleId);
                    Assert.NotNull(resolved);
                    Assert.StartsWith($"supplemental:{supplementalContent}", resolved!.PrimaryText!);
                }
            },
            iter: 100,
            print: t => $"ruleIds=[{string.Join(",", t.Item1)}]");
    }

    [Fact]
    public void Merge_DeclarationOrderWins_WithinSameScope()
    {
        // **Validates: Requirements 10.5**
        //
        // Within the same scope level, for any two packs declaring the same rule ID,
        // the rule from the pack declared earlier in the rulesPacks list wins.
        Gen.Select(
            GenRuleId.Array[2, 6],
            GenPackName,
            GenPackName,
            GenRuleText,
            GenRuleText,
            GenPackScope)
        .Where(t => t.Item2 != t.Item3) // Ensure distinct pack names
        .Sample(
            (ruleIds, packName1, packName2, content1, content2, scope) =>
            {
                var resolver = new SteeringResolver();

                // First pack (earlier in declaration order)
                var rules1 = ruleIds.Select(id => MakeMergeRule(id, $"first:{content1}", packName1, scope)).ToList();
                var doc1 = MakeMergeDocument($"doc-{packName1}", rules1);

                // Second pack (later in declaration order)
                var rules2 = ruleIds.Select(id => MakeMergeRule(id, $"second:{content2}", packName2, scope)).ToList();
                var doc2 = MakeMergeDocument($"doc-{packName2}", rules2);

                // Both packs at the same scope, doc1 declared first
                var packDocuments = new List<ScopedPackDocuments>
                {
                    new() { Scope = scope, Documents = [doc1, doc2] }
                };

                var result = resolver.Resolve(
                    projectDocuments: Array.Empty<SteeringDocument>(),
                    packDocuments: packDocuments,
                    activeProfiles: Array.Empty<string>());

                // First-declared pack's rules should win
                foreach (var ruleId in ruleIds)
                {
                    var resolved = result.Rules.FirstOrDefault(r => r.Id == ruleId);
                    Assert.NotNull(resolved);
                    Assert.StartsWith($"first:{content1}", resolved!.PrimaryText!);
                }
            },
            iter: 100,
            print: t => $"ruleIds=[{string.Join(",", t.Item1)}], pack1={t.Item2}, pack2={t.Item3}, scope={t.Item6}");
    }

    [Fact]
    public void Merge_DeclarationOrderWins_MultiplePacks_SameScope()
    {
        // **Validates: Requirements 10.3, 10.5**
        //
        // For any number of packs at the same scope with overlapping rule IDs,
        // the first-declared pack always wins for each duplicate rule ID.
        Gen.Select(
            GenRuleId,
            GenPackScope,
            Gen.Int[2, 4])
        .Sample(
            (ruleId, scope, packCount) =>
            {
                var resolver = new SteeringResolver();
                var packDocs = new List<SteeringDocument>();

                for (var i = 0; i < packCount; i++)
                {
                    var packName = $"pack-{i}";
                    var rule = MakeMergeRule(ruleId, $"content-from-pack-{i}", packName, scope);
                    var doc = MakeMergeDocument($"doc-{packName}", new[] { rule });
                    packDocs.Add(doc);
                }

                var packDocuments = new List<ScopedPackDocuments>
                {
                    new() { Scope = scope, Documents = packDocs }
                };

                var result = resolver.Resolve(
                    projectDocuments: Array.Empty<SteeringDocument>(),
                    packDocuments: packDocuments,
                    activeProfiles: Array.Empty<string>());

                // First pack (index 0) should win
                var resolved = result.Rules.FirstOrDefault(r => r.Id == ruleId);
                Assert.NotNull(resolved);
                Assert.Equal("content-from-pack-0", resolved!.PrimaryText);
            },
            iter: 100,
            print: t => $"ruleId={t.Item1}, scope={t.Item2}, packCount={t.Item3}");
    }

    [Fact]
    public void Merge_ConsumerScopeOverride_ElevatesPrecedence()
    {
        // **Validates: Requirements 10.6**
        //
        // When a consumer scope override elevates a pack (e.g., global → project),
        // the elevated pack wins over lower-scoped packs.
        Gen.Select(
            GenRuleId.Array[2, 6],
            GenRuleText,
            GenRuleText)
        .Sample(
            (ruleIds, elevatedContent, supplementalContent) =>
            {
                var resolver = new SteeringResolver();

                // A pack elevated to project scope via consumer override
                var elevatedRules = ruleIds.Select(id => MakeMergeRule(id, $"elevated:{elevatedContent}", "elevated-pack", PackScope.Project)).ToList();
                var elevatedDoc = MakeMergeDocument("elevated-doc", elevatedRules);

                // A supplemental pack (lower precedence than project)
                var supplementalRules = ruleIds.Select(id => MakeMergeRule(id, $"supplemental:{supplementalContent}", "supp-pack", PackScope.Supplemental)).ToList();
                var supplementalDoc = MakeMergeDocument("supplemental-doc", supplementalRules);

                var packDocuments = new List<ScopedPackDocuments>
                {
                    new() { Scope = PackScope.Project, Documents = [elevatedDoc] },
                    new() { Scope = PackScope.Supplemental, Documents = [supplementalDoc] }
                };

                var result = resolver.Resolve(
                    projectDocuments: Array.Empty<SteeringDocument>(),
                    packDocuments: packDocuments,
                    activeProfiles: Array.Empty<string>());

                // Elevated pack (now project scope) should win over supplemental
                foreach (var ruleId in ruleIds)
                {
                    var resolved = result.Rules.FirstOrDefault(r => r.Id == ruleId);
                    Assert.NotNull(resolved);
                    Assert.StartsWith($"elevated:{elevatedContent}", resolved!.PrimaryText!);
                }
            },
            iter: 100,
            print: t => $"ruleIds=[{string.Join(",", t.Item1)}]");
    }

    [Fact]
    public void Merge_ConsumerScopeOverride_DemotesPrecedence()
    {
        // **Validates: Requirements 10.6**
        //
        // When a consumer scope override demotes a pack (e.g., project → global),
        // the demoted pack loses to higher-scoped packs.
        Gen.Select(
            GenRuleId.Array[2, 6],
            GenRuleText,
            GenRuleText)
        .Sample(
            (ruleIds, demotedContent, supplementalContent) =>
            {
                var resolver = new SteeringResolver();

                // A pack demoted to global scope via consumer override
                var demotedRules = ruleIds.Select(id => MakeMergeRule(id, $"demoted:{demotedContent}", "demoted-pack", PackScope.Global)).ToList();
                var demotedDoc = MakeMergeDocument("demoted-doc", demotedRules);

                // A supplemental pack (higher precedence than global)
                var supplementalRules = ruleIds.Select(id => MakeMergeRule(id, $"supplemental:{supplementalContent}", "supp-pack", PackScope.Supplemental)).ToList();
                var supplementalDoc = MakeMergeDocument("supplemental-doc", supplementalRules);

                var packDocuments = new List<ScopedPackDocuments>
                {
                    new() { Scope = PackScope.Global, Documents = [demotedDoc] },
                    new() { Scope = PackScope.Supplemental, Documents = [supplementalDoc] }
                };

                var result = resolver.Resolve(
                    projectDocuments: Array.Empty<SteeringDocument>(),
                    packDocuments: packDocuments,
                    activeProfiles: Array.Empty<string>());

                // Supplemental should win over demoted (now global) pack
                foreach (var ruleId in ruleIds)
                {
                    var resolved = result.Rules.FirstOrDefault(r => r.Id == ruleId);
                    Assert.NotNull(resolved);
                    Assert.StartsWith($"supplemental:{supplementalContent}", resolved!.PrimaryText!);
                }
            },
            iter: 100,
            print: t => $"ruleIds=[{string.Join(",", t.Item1)}]");
    }

    [Fact]
    public void Merge_FullPrecedenceChain_RandomScopes_HighestWins()
    {
        // **Validates: Requirements 10.3, 10.4, 10.5, 10.6, 11.5, 11.7**
        //
        // For any random assignment of rules to scopes, the merge always selects
        // the rule from the highest-precedence source.
        Gen.Select(
            GenRuleId,
            GenRuleText,
            GenPackScope,
            GenPackScope,
            Gen.Bool)
        .Sample(
            (ruleId, content, scopeA, scopeB, hasLocal) =>
            {
                var resolver = new SteeringResolver();

                // Project-local rule (if present)
                var localDocs = hasLocal
                    ? new[] { MakeMergeDocument("local-doc", new[] { MakeMergeRule(ruleId, "local-rule") }) }
                    : Array.Empty<SteeringDocument>();

                // Pack A
                var ruleA = MakeMergeRule(ruleId, $"packA:{content}", "pack-a", scopeA);
                var docA = MakeMergeDocument("doc-a", new[] { ruleA });

                // Pack B
                var ruleB = MakeMergeRule(ruleId, $"packB:{content}", "pack-b", scopeB);
                var docB = MakeMergeDocument("doc-b", new[] { ruleB });

                // Group by scope — documents within same scope maintain declaration order
                var scopeGroups = new[] { (scopeA, docA), (scopeB, docB) }
                    .GroupBy(x => x.Item1)
                    .Select(g => new ScopedPackDocuments
                    {
                        Scope = g.Key,
                        Documents = g.Select(x => x.Item2).ToList()
                    })
                    .ToList();

                var result = resolver.Resolve(
                    projectDocuments: localDocs,
                    packDocuments: scopeGroups,
                    activeProfiles: Array.Empty<string>());

                var resolved = result.Rules.FirstOrDefault(r => r.Id == ruleId);
                Assert.NotNull(resolved);

                if (hasLocal)
                {
                    // Project-local always wins
                    Assert.Equal("local-rule", resolved!.PrimaryText);
                }
                else if (scopeA == scopeB)
                {
                    // Same scope: first declared (pack A) wins
                    Assert.Equal($"packA:{content}", resolved!.PrimaryText);
                }
                else if (ScopePrecedence(scopeA) > ScopePrecedence(scopeB))
                {
                    // Pack A has higher scope precedence
                    Assert.Equal($"packA:{content}", resolved!.PrimaryText);
                }
                else
                {
                    // Pack B has higher scope precedence
                    Assert.Equal($"packB:{content}", resolved!.PrimaryText);
                }
            },
            iter: 200,
            print: t => $"ruleId={t.Item1}, scopeA={t.Item3}, scopeB={t.Item4}, hasLocal={t.Item5}");
    }

    [Fact]
    public void Merge_NonOverlappingRules_AllPreserved()
    {
        // **Validates: Requirements 10.3, 10.4**
        //
        // Rules with unique IDs across all scopes are all preserved in the
        // merged result regardless of their source scope.
        Gen.Select(
            Gen.Int[2, 5],
            GenRuleText)
        .Sample(
            (rulesPerScope, content) =>
            {
                var resolver = new SteeringResolver();

                // Create unique rule IDs per scope (no overlaps)
                var localRules = Enumerable.Range(0, rulesPerScope)
                    .Select(i => MakeMergeRule($"local-{i}", $"local:{content}"))
                    .ToList();
                var localDoc = MakeMergeDocument("local-doc", localRules);

                var globalRules = Enumerable.Range(0, rulesPerScope)
                    .Select(i => MakeMergeRule($"global-{i}", $"global:{content}", "global-pack", PackScope.Global))
                    .ToList();
                var globalDoc = MakeMergeDocument("global-doc", globalRules);

                var supplementalRules = Enumerable.Range(0, rulesPerScope)
                    .Select(i => MakeMergeRule($"supp-{i}", $"supp:{content}", "supp-pack", PackScope.Supplemental))
                    .ToList();
                var supplementalDoc = MakeMergeDocument("supplemental-doc", supplementalRules);

                var packDocuments = new List<ScopedPackDocuments>
                {
                    new() { Scope = PackScope.Global, Documents = [globalDoc] },
                    new() { Scope = PackScope.Supplemental, Documents = [supplementalDoc] }
                };

                var result = resolver.Resolve(
                    projectDocuments: new[] { localDoc },
                    packDocuments: packDocuments,
                    activeProfiles: Array.Empty<string>());

                // All unique rules should be present
                var expectedCount = rulesPerScope * 3;
                var resolvedIds = result.Rules.Select(r => r.Id).ToHashSet();

                Assert.Equal(expectedCount, resolvedIds.Count);

                for (var i = 0; i < rulesPerScope; i++)
                {
                    Assert.Contains($"local-{i}", resolvedIds);
                    Assert.Contains($"global-{i}", resolvedIds);
                    Assert.Contains($"supp-{i}", resolvedIds);
                }
            },
            iter: 100,
            print: t => $"rulesPerScope={t.Item1}");
    }
}
