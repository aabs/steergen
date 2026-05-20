using CsCheck;
using Steergen.Core.Model;
using Steergen.Core.Targets;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for pack-provided target rendering equivalence.
///
/// Property 15: Pack-Provided Target Rendering Equivalence
/// For any set of routed rules and write plan, a PackTargetComponent SHALL produce
/// output by rendering the pack's Scriban templates with the same model fields
/// available to built-in targets. The rendered output SHALL be deterministic for
/// identical inputs.
///
/// **Validates: Requirements 16.5, 16.7**
/// </summary>
public sealed class PackTargetComponentProperties : IDisposable
{
    private readonly string _testRoot;

    public PackTargetComponentProperties()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "PackTargetProps_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    // ── Generators ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates valid target IDs (alphanumeric lowercase, 3-10 chars).
    /// </summary>
    private static readonly Gen<string> GenTargetId =
        Gen.String[Gen.Char['a', 'z'], 3, 10];

    /// <summary>
    /// Generates valid rule IDs (alphanumeric with dashes, 4-12 chars).
    /// </summary>
    private static readonly Gen<string> GenRuleId =
        Gen.String[Gen.Char['a', 'z'], 4, 12]
           .Select(s => $"rule-{s}");

    /// <summary>
    /// Generates category strings.
    /// </summary>
    private static readonly Gen<string> GenCategory =
        Gen.OneOfConst("security", "performance", "reliability", "governance", "testing");

    /// <summary>
    /// Generates primary text content for rules.
    /// </summary>
    private static readonly Gen<string> GenPrimaryText =
        Gen.String[Gen.Char.AlphaNumeric, 10, 50]
           .Select(s => $"Rule text: {s}");

    /// <summary>
    /// Generates explanatory text content for rules.
    /// </summary>
    private static readonly Gen<string> GenExplanatoryText =
        Gen.String[Gen.Char.AlphaNumeric, 10, 80]
           .Select(s => $"Explanation: {s}");

    /// <summary>
    /// Generates tags lists (0-3 tags).
    /// </summary>
    private static readonly Gen<IReadOnlyList<string>> GenTags =
        Gen.String[Gen.Char['a', 'z'], 3, 8].Array[0, 3]
           .Select(arr => (IReadOnlyList<string>)arr.ToList());

    /// <summary>
    /// Generates a single SteeringRule with random fields.
    /// </summary>
    private static readonly Gen<SteeringRule> GenRule =
        Gen.Select(GenRuleId, GenCategory, Gen.Bool, Gen.Bool, GenPrimaryText, GenExplanatoryText, GenTags)
           .Select((id, cat, mandatory, deprecated, primary, explanatory, tags) => new SteeringRule
           {
               Id = id,
               Category = cat,
               Mandatory = mandatory,
               Deprecated = deprecated,
               PrimaryText = primary,
               ExplanatoryText = explanatory,
               Tags = tags,
               InputFileStem = $"doc-{id}"
           });

    /// <summary>
    /// Generates a list of 1-5 rules with unique IDs.
    /// </summary>
    private static readonly Gen<IReadOnlyList<SteeringRule>> GenRules =
        GenRule.Array[1, 5]
            .Select(arr =>
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var unique = new List<SteeringRule>();
                foreach (var rule in arr)
                {
                    if (seen.Add(rule.Id!))
                        unique.Add(rule);
                }
                return (IReadOnlyList<SteeringRule>)unique;
            })
            .Where(list => list.Count > 0);

    /// <summary>
    /// Generates format options (0-3 key-value pairs).
    /// </summary>
    private static readonly Gen<Dictionary<string, string>> GenFormatOptions =
        Gen.Select(
            Gen.String[Gen.Char['a', 'z'], 3, 8],
            Gen.String[Gen.Char.AlphaNumeric, 3, 12])
        .Array[0, 3]
        .Select(pairs =>
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in pairs)
            {
                dict.TryAdd(key, value);
            }
            return dict;
        });

    /// <summary>
    /// Generates a relative file path for write plan output.
    /// </summary>
    private static readonly Gen<string> GenFilePath =
        Gen.Select(
            Gen.String[Gen.Char['a', 'z'], 3, 8],
            Gen.String[Gen.Char['a', 'z'], 3, 8])
        .Select((dir, name) => Path.Combine(dir, $"{name}.md"));

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private string CreateOutputDir()
    {
        var dir = Path.Combine(_testRoot, "out_" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Creates a Scriban template that echoes all model fields in a deterministic format.
    /// This allows us to verify that the correct model fields are passed to the template.
    /// </summary>
    private static string CreateEchoTemplate() =>
        """
        TARGET_ID={{ target_id }}
        FILE_PATH={{ file_path }}
        RULES_COUNT={{ rules | array.size }}
        {{~ for rule in rules ~}}
        RULE:{{ rule.id }}|{{ rule.category }}|{{ rule.mandatory }}|{{ rule.deprecated }}|{{ rule.primary_text }}|{{ rule.explanatory_text }}|{{ rule.input_file_stem }}|{{ rule.tags | array.join "," }}
        {{~ end ~}}
        FORMAT_OPTIONS_COUNT={{ format_options | object.size }}
        {{~ for key in format_options | object.keys ~}}
        OPT:{{ key }}={{ format_options[key] }}
        {{~ end ~}}
        """;

    private static WritePlan BuildWritePlan(
        string targetId,
        string filePath,
        IReadOnlyList<SteeringRule> rules) =>
        new()
        {
            TargetId = targetId,
            Files =
            [
                new WritePlanFile
                {
                    Path = filePath,
                    TruncateAtStart = true,
                    AppendUnits = rules.Select(r => new ContentUnit
                    {
                        RuleId = r.Id!,
                        RenderedContent = "",
                        OrderKey = (0, 0, r.Id!)
                    }).ToList()
                }
            ]
        };

    private static ResolvedSteeringModel BuildModel(IReadOnlyList<SteeringRule> rules) =>
        new()
        {
            Rules = rules,
            Documents = [],
            ActiveProfiles = []
        };

    private static TargetConfiguration BuildConfig(string outputPath, Dictionary<string, string> formatOptions) =>
        new()
        {
            Id = "test-target",
            Enabled = true,
            OutputPath = outputPath,
            FormatOptions = formatOptions
        };

    // ── Property 15a: Deterministic output for identical inputs ───────────────────

    [Fact]
    public void GenerateWithPlan_ProducesDeterministicOutput_ForIdenticalInputs()
    {
        // **Validates: Requirements 16.7**
        //
        // For any set of routed rules and write plan, calling GenerateWithPlanAsync
        // twice with identical inputs (same output path) SHALL produce identical output files.
        Gen.Select(GenTargetId, GenRules, GenFilePath, GenFormatOptions)
            .Sample(
                (targetId, rules, filePath, formatOptions) =>
                {
                    var templateText = CreateEchoTemplate();
                    var templateProvider = new InMemoryTemplateProvider(targetId, templateText);

                    var outputDir = CreateOutputDir();

                    var writePlan = BuildWritePlan(targetId, filePath, rules);
                    var model = BuildModel(rules);
                    var config = BuildConfig(outputDir, formatOptions);

                    var component = new PackTargetComponent(
                        targetId, templateProvider, "layout.yaml", "test-pack");

                    // First render
                    component.GenerateWithPlanAsync(model, config, writePlan, CancellationToken.None)
                        .GetAwaiter().GetResult();

                    // Capture first output
                    var outputFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories)
                        .OrderBy(f => f, StringComparer.Ordinal).ToList();
                    var firstContents = outputFiles.Select(File.ReadAllText).ToList();

                    // Delete output files and re-render with identical inputs
                    foreach (var file in outputFiles)
                        File.Delete(file);

                    // Second render (same component, same inputs)
                    component.GenerateWithPlanAsync(model, config, writePlan, CancellationToken.None)
                        .GetAwaiter().GetResult();

                    // Capture second output
                    var outputFiles2 = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories)
                        .OrderBy(f => f, StringComparer.Ordinal).ToList();
                    var secondContents = outputFiles2.Select(File.ReadAllText).ToList();

                    // Same number of files
                    Assert.Equal(firstContents.Count, secondContents.Count);

                    // Identical content
                    for (int i = 0; i < firstContents.Count; i++)
                    {
                        Assert.Equal(firstContents[i], secondContents[i]);
                    }
                },
                iter: 150,
                print: t => $"target={t.Item1}, rules={t.Item2.Count}, path={t.Item3}");
    }

    // ── Property 15b: Correct model fields available to templates ─────────────────

    [Fact]
    public void GenerateWithPlan_ExposesCorrectModelFields_ToTemplates()
    {
        // **Validates: Requirements 16.5**
        //
        // For any set of routed rules and write plan, the PackTargetComponent SHALL
        // render the pack's Scriban templates with the same model fields available
        // to built-in targets: rules, targetId, filePath, formatOptions.
        Gen.Select(GenTargetId, GenRules, GenFilePath, GenFormatOptions)
            .Sample(
                (targetId, rules, filePath, formatOptions) =>
                {
                    var templateText = CreateEchoTemplate();
                    var templateProvider = new InMemoryTemplateProvider(targetId, templateText);

                    var outputDir = CreateOutputDir();
                    var writePlan = BuildWritePlan(targetId, filePath, rules);
                    var model = BuildModel(rules);
                    var config = BuildConfig(outputDir, formatOptions);

                    var component = new PackTargetComponent(
                        targetId, templateProvider, "layout.yaml", "test-pack");

                    component.GenerateWithPlanAsync(model, config, writePlan, CancellationToken.None)
                        .GetAwaiter().GetResult();

                    // Read the rendered output
                    var outputFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
                    Assert.Single(outputFiles);

                    var content = File.ReadAllText(outputFiles[0]);

                    // Verify target_id field
                    Assert.Contains($"TARGET_ID={targetId}", content);

                    // Verify rules count
                    Assert.Contains($"RULES_COUNT={rules.Count}", content);

                    // Verify each rule's fields are present
                    foreach (var rule in rules)
                    {
                        var expectedRuleLine = $"RULE:{rule.Id}|{rule.Category}|{rule.Mandatory.ToString().ToLowerInvariant()}|{rule.Deprecated.ToString().ToLowerInvariant()}|{rule.PrimaryText}|{rule.ExplanatoryText}|{rule.InputFileStem}|{string.Join(",", rule.Tags)}";
                        Assert.Contains(expectedRuleLine, content);
                    }

                    // Verify format_options count
                    Assert.Contains($"FORMAT_OPTIONS_COUNT={formatOptions.Count}", content);

                    // Verify each format option is present
                    foreach (var (key, value) in formatOptions)
                    {
                        Assert.Contains($"OPT:{key}={value}", content);
                    }
                },
                iter: 150,
                print: t => $"target={t.Item1}, rules={t.Item2.Count}, formatOpts={t.Item4.Count}");
    }

    // ── Property 15c: file_path field matches resolved output path ────────────────

    [Fact]
    public void GenerateWithPlan_ExposesCorrectFilePath_InRenderModel()
    {
        // **Validates: Requirements 16.5, 16.7**
        //
        // For any write plan file path, the file_path field exposed to the template
        // SHALL match the resolved output path used for writing.
        Gen.Select(GenTargetId, GenRules, GenFilePath, GenFormatOptions)
            .Sample(
                (targetId, rules, filePath, formatOptions) =>
                {
                    var templateText = CreateEchoTemplate();
                    var templateProvider = new InMemoryTemplateProvider(targetId, templateText);

                    var outputDir = CreateOutputDir();
                    var writePlan = BuildWritePlan(targetId, filePath, rules);
                    var model = BuildModel(rules);
                    var config = BuildConfig(outputDir, formatOptions);

                    var component = new PackTargetComponent(
                        targetId, templateProvider, "layout.yaml", "test-pack");

                    component.GenerateWithPlanAsync(model, config, writePlan, CancellationToken.None)
                        .GetAwaiter().GetResult();

                    // Read the rendered output
                    var outputFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
                    Assert.Single(outputFiles);

                    var content = File.ReadAllText(outputFiles[0]);

                    // The file_path in the template should be the resolved output path
                    var expectedPath = Path.Combine(outputDir, filePath);
                    Assert.Contains($"FILE_PATH={expectedPath}", content);
                },
                iter: 150,
                print: t => $"target={t.Item1}, path={t.Item3}");
    }

    // ── Property 15d: Empty rules produce no output files ────────────────────────

    [Fact]
    public void GenerateWithPlan_SkipsFiles_WhenNoRulesMatch()
    {
        // **Validates: Requirements 16.5**
        //
        // For any write plan where routed rule IDs do not match any rules in the model,
        // the PackTargetComponent SHALL not produce output files for that plan entry.
        Gen.Select(GenTargetId, GenFilePath, GenFormatOptions)
            .Sample(
                (targetId, filePath, formatOptions) =>
                {
                    var templateText = CreateEchoTemplate();
                    var templateProvider = new InMemoryTemplateProvider(targetId, templateText);

                    var outputDir = CreateOutputDir();

                    // Write plan references rule IDs that don't exist in the model
                    var writePlan = new WritePlan
                    {
                        TargetId = targetId,
                        Files =
                        [
                            new WritePlanFile
                            {
                                Path = filePath,
                                TruncateAtStart = true,
                                AppendUnits =
                                [
                                    new ContentUnit
                                    {
                                        RuleId = "nonexistent-rule-id",
                                        RenderedContent = "",
                                        OrderKey = (0, 0, "nonexistent-rule-id")
                                    }
                                ]
                            }
                        ]
                    };

                    var model = new ResolvedSteeringModel
                    {
                        Rules = [],
                        Documents = [],
                        ActiveProfiles = []
                    };

                    var config = BuildConfig(outputDir, formatOptions);

                    var component = new PackTargetComponent(
                        targetId, templateProvider, "layout.yaml", "test-pack");

                    component.GenerateWithPlanAsync(model, config, writePlan, CancellationToken.None)
                        .GetAwaiter().GetResult();

                    // No output files should be produced
                    var outputFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
                    Assert.Empty(outputFiles);
                },
                iter: 100,
                print: t => $"target={t.Item1}, path={t.Item2}");
    }

    // ── In-memory template provider ─────────────────────────────────────────────

    /// <summary>
    /// A simple ITemplateProvider that returns a fixed template for a specific target.
    /// Used to verify the render model fields without filesystem dependencies.
    /// </summary>
    private sealed class InMemoryTemplateProvider : ITemplateProvider
    {
        private readonly string _targetId;
        private readonly string _templateText;

        public InMemoryTemplateProvider(string targetId, string templateText)
        {
            _targetId = targetId;
            _templateText = templateText;
        }

        public string GetTemplate(string targetId, string templateName)
        {
            if (string.Equals(targetId, _targetId, StringComparison.Ordinal))
                return _templateText;

            throw new InvalidOperationException(
                $"Template not found for target '{targetId}', template '{templateName}'.");
        }
    }
}
