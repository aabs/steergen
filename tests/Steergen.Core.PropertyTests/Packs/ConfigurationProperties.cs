using CsCheck;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for configuration round-trip serialization.
///
/// Property 8: Configuration Round-Trip
/// For any valid SteeringConfiguration containing template pack and rules pack entries,
/// serializing to YAML and deserializing back SHALL produce an equivalent configuration
/// (all fields preserved including source, ref, path, and scope for each pack entry).
///
/// **Validates: Requirements 3.1, 10.1, 10.2**
/// </summary>
public sealed class ConfigurationProperties : IDisposable
{
    private readonly string _testDir;

    public ConfigurationProperties()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ConfigProps_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    // ── Generators ───────────────────────────────────────────────────────────

    private static readonly Gen<string> GenAlphaString =
        Gen.String[Gen.Char['a', 'z'], 2, 10];

    private static readonly Gen<string> GenOwnerRepo =
        Gen.Select(GenAlphaString, GenAlphaString)
           .Select((owner, repo) => $"github:{owner}/{repo}");

    private static readonly Gen<string> GenRef =
        Gen.OneOf(
            Gen.String[Gen.Char['a', 'f'], 40, 40], // SHA-like
            GenAlphaString.Select(s => $"v1.{s.Length}.0"), // tag-like
            GenAlphaString); // branch-like

    private static readonly Gen<PackScope> GenScope =
        Gen.OneOf(Gen.Const(PackScope.Global), Gen.Const(PackScope.Supplemental), Gen.Const(PackScope.Project));

    private static readonly Gen<TemplatePackConfig> GenTemplatePackConfig =
        Gen.Select(GenOwnerRepo, GenRef)
           .Select((source, refVal) => new TemplatePackConfig
           {
               Source = source,
               Ref = refVal,
               LocalPath = null
           });

    private static readonly Gen<RulesPackEntry> GenRulesPackEntry =
        Gen.Select(GenOwnerRepo, GenRef, Gen.Bool, GenAlphaString, GenScope)
           .Select((source, refVal, hasPath, path, scope) => new RulesPackEntry
           {
               Source = source,
               Ref = refVal,
               Path = hasPath ? path : null,
               Scope = scope
           });

    private static readonly Gen<SteeringConfiguration> GenConfiguration =
        Gen.Select(
            GenAlphaString, // projectRoot
            GenAlphaString, // generationRoot
            GenAlphaString.Array[0, 3], // activeProfiles
            GenAlphaString.Array[0, 3], // registeredTargets
            Gen.Bool, // hasTemplatePack
            GenTemplatePackConfig,
            GenRulesPackEntry.Array[0, 3])
        .Select((projectRoot, genRoot, profiles, targets, hasTp, tp, rulesPacks) =>
            new SteeringConfiguration
            {
                ProjectRoot = projectRoot,
                GenerationRoot = genRoot,
                ActiveProfiles = profiles.ToList(),
                RegisteredTargets = targets.ToList(),
                TemplatePack = hasTp ? tp : null,
                RulesPacks = rulesPacks.ToList()
            });

    // ── Property 8: Configuration Round-Trip ─────────────────────────────────

    [Fact]
    public void Configuration_RoundTrip_PreservesAllFields()
    {
        // **Validates: Requirements 3.1, 10.1, 10.2**
        //
        // For any valid SteeringConfiguration with template pack and rules pack entries,
        // serializing to YAML and deserializing back produces an equivalent configuration.
        var writer = new SteergenConfigWriter();
        var loader = new SteergenConfigLoader();

        GenConfiguration.Sample(
            config =>
            {
                var filePath = Path.Combine(_testDir, $"config_{Guid.NewGuid():N}.yaml");

                // Serialize
                writer.WriteAsync(filePath, config).GetAwaiter().GetResult();

                // Deserialize
                var loaded = loader.LoadAsync(filePath).GetAwaiter().GetResult();

                // Assert equivalence of core fields
                Assert.Equal(config.ProjectRoot, loaded.ProjectRoot);
                Assert.Equal(config.GenerationRoot, loaded.GenerationRoot);
                Assert.Equal(config.ActiveProfiles, loaded.ActiveProfiles);
                Assert.Equal(config.RegisteredTargets, loaded.RegisteredTargets);

                // Assert template pack round-trip
                if (config.TemplatePack is null)
                {
                    Assert.Null(loaded.TemplatePack);
                }
                else
                {
                    Assert.NotNull(loaded.TemplatePack);
                    Assert.Equal(config.TemplatePack.Source, loaded.TemplatePack.Source);
                    Assert.Equal(config.TemplatePack.Ref, loaded.TemplatePack.Ref);
                    Assert.Equal(config.TemplatePack.LocalPath, loaded.TemplatePack.LocalPath);
                }

                // Assert rules packs round-trip
                Assert.Equal(config.RulesPacks.Count, loaded.RulesPacks.Count);
                for (var i = 0; i < config.RulesPacks.Count; i++)
                {
                    Assert.Equal(config.RulesPacks[i].Source, loaded.RulesPacks[i].Source);
                    Assert.Equal(config.RulesPacks[i].Ref, loaded.RulesPacks[i].Ref);
                    Assert.Equal(config.RulesPacks[i].Path, loaded.RulesPacks[i].Path);
                    Assert.Equal(config.RulesPacks[i].Scope, loaded.RulesPacks[i].Scope);
                }

                // Cleanup
                File.Delete(filePath);
            },
            iter: 100,
            print: config => $"projectRoot={config.ProjectRoot}, tp={config.TemplatePack is not null}, rulesPacks={config.RulesPacks.Count}");
    }

    [Fact]
    public void Configuration_RoundTrip_PreservesRulesPackSourceRefPath()
    {
        // **Validates: Requirements 10.1, 10.2**
        //
        // Specifically validates that source, ref, and path fields on rules pack entries
        // survive the round-trip through YAML serialization.
        var writer = new SteergenConfigWriter();
        var loader = new SteergenConfigLoader();

        GenRulesPackEntry.Array[1, 5].Sample(
            entries =>
            {
                var config = new SteeringConfiguration
                {
                    ProjectRoot = "test",
                    RulesPacks = entries.ToList()
                };

                var filePath = Path.Combine(_testDir, $"rules_rt_{Guid.NewGuid():N}.yaml");

                writer.WriteAsync(filePath, config).GetAwaiter().GetResult();
                var loaded = loader.LoadAsync(filePath).GetAwaiter().GetResult();

                Assert.Equal(entries.Length, loaded.RulesPacks.Count);
                for (var i = 0; i < entries.Length; i++)
                {
                    Assert.Equal(entries[i].Source, loaded.RulesPacks[i].Source);
                    Assert.Equal(entries[i].Ref, loaded.RulesPacks[i].Ref);
                    Assert.Equal(entries[i].Path, loaded.RulesPacks[i].Path);
                    Assert.Equal(entries[i].Scope, loaded.RulesPacks[i].Scope);
                }

                File.Delete(filePath);
            },
            iter: 100,
            print: entries => $"count={entries.Length}");
    }

    [Fact]
    public void Configuration_RoundTrip_PreservesTemplatePackFields()
    {
        // **Validates: Requirements 3.1**
        //
        // Specifically validates that template pack source and ref fields
        // survive the round-trip through YAML serialization.
        var writer = new SteergenConfigWriter();
        var loader = new SteergenConfigLoader();

        GenTemplatePackConfig.Sample(
            tp =>
            {
                var config = new SteeringConfiguration
                {
                    ProjectRoot = "test",
                    TemplatePack = tp
                };

                var filePath = Path.Combine(_testDir, $"tp_rt_{Guid.NewGuid():N}.yaml");

                writer.WriteAsync(filePath, config).GetAwaiter().GetResult();
                var loaded = loader.LoadAsync(filePath).GetAwaiter().GetResult();

                Assert.NotNull(loaded.TemplatePack);
                Assert.Equal(tp.Source, loaded.TemplatePack.Source);
                Assert.Equal(tp.Ref, loaded.TemplatePack.Ref);
                Assert.Equal(tp.LocalPath, loaded.TemplatePack.LocalPath);

                File.Delete(filePath);
            },
            iter: 100,
            print: tp => $"source={tp.Source}, ref={tp.Ref}");
    }
}
