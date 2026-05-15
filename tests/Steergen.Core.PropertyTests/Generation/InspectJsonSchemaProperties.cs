using System.Text.Json;
using CsCheck;
using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.PropertyTests.Generation;

/// <summary>
/// Property tests for <see cref="InspectModelWriter"/> JSON schema correctness.
/// Feature: simplify-rule-attributes, Property 8: Inspect JSON schema correctness
/// Validates: Requirements 4.4, 8.1, 8.2, 8.3
/// </summary>
public sealed class InspectJsonSchemaProperties
{
    // ── Generators ───────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenRuleId =
        Gen.String[Gen.Char.AlphaNumeric, 1, 10]
           .Select(s => $"R-{s}");

    private static readonly Gen<string?> GenCategory =
        Gen.OneOf(
            Gen.Const((string?)null),
            Gen.Const((string?)"core"),
            Gen.Const((string?)"security"),
            Gen.Const((string?)"quality"),
            Gen.Const((string?)"api"),
            Gen.String[Gen.Char.AlphaNumeric, 3, 12].Select(s => (string?)s));

    private static readonly Gen<bool> GenMandatory =
        Gen.Bool;

    private static readonly Gen<IReadOnlyList<string>> GenTags =
        Gen.String[Gen.Char.AlphaNumeric, 2, 8]
           .Array[0, 4]
           .Select(arr => (IReadOnlyList<string>)arr.ToList());

    private static readonly Gen<IReadOnlyList<string>> GenAppliesTo =
        Gen.String[Gen.Char.AlphaNumeric, 3, 10]
           .Array[0, 3]
           .Select(arr => (IReadOnlyList<string>)arr.ToList());

    private static readonly Gen<bool> GenDeprecated =
        Gen.Bool;

    private static readonly Gen<string?> GenPrimaryText =
        Gen.OneOf(
            Gen.Const((string?)null),
            Gen.String[Gen.Char.AlphaNumeric, 5, 50].Select(s => (string?)s));

    private static readonly Gen<SteeringRule> GenRule =
        Gen.Select(GenRuleId, GenCategory, GenMandatory, GenTags, GenAppliesTo, GenDeprecated, GenPrimaryText)
           .Select((id, category, mandatory, tags, appliesTo, deprecated, primaryText) =>
               new SteeringRule
               {
                   Id = id,
                   Category = category,
                   Mandatory = mandatory,
                   Tags = tags,
                   AppliesTo = appliesTo,
                   Deprecated = deprecated,
                   PrimaryText = primaryText,
               });

    private static readonly Gen<string?> GenDocId =
        Gen.String[Gen.Char.AlphaNumeric, 3, 10]
           .Select(s => (string?)$"DOC-{s}");

    private static readonly Gen<SteeringDocument> GenDocument =
        Gen.Select(GenDocId, GenTags)
           .Select((id, tags) =>
               new SteeringDocument
               {
                   Id = id,
                   Title = $"Title for {id}",
                   Version = "1.0.0",
                   SourcePath = $"path/{id}.md",
                   Tags = tags,
                   Profiles = [],
                   Rules = [],
               });

    private static readonly Gen<IReadOnlyList<string>> GenProfiles =
        Gen.String[Gen.Char.AlphaNumeric, 3, 8]
           .Array[0, 3]
           .Select(arr => (IReadOnlyList<string>)arr.ToList());

    /// <summary>
    /// Generates a ResolvedSteeringModel with 1-10 random rules and 0-3 documents.
    /// </summary>
    private static readonly Gen<ResolvedSteeringModel> GenModel =
        Gen.Select(
            GenRule.Array[1, 10].Select(arr => (IReadOnlyList<SteeringRule>)arr.ToList()),
            GenDocument.Array[0, 3].Select(arr => (IReadOnlyList<SteeringDocument>)arr.ToList()),
            GenProfiles)
           .Select((rules, docs, profiles) =>
               new ResolvedSteeringModel
               {
                   Rules = rules,
                   Documents = docs,
                   ActiveProfiles = profiles,
                   SourceIndex = new Dictionary<string, SteeringDocument>(),
               });

    // ── Forbidden field names that must not appear in rule objects ────────────────

    private static readonly string[] ForbiddenRuleFields = ["severity", "domain", "profile", "supersedes"];

    // ── Property 8: Inspect JSON schema correctness ─────────────────────────────
    //
    // For any ResolvedSteeringModel, the JSON output from InspectModelWriter.Write
    // SHALL include a mandatory boolean field for every rule object, SHALL NOT include
    // severity, domain, profile, or supersedes fields in any rule object, and SHALL
    // produce rules sorted by ID.

    [Fact]
    public void EveryRuleObject_HasMandatoryBooleanField()
    {
        // Validates: Requirements 4.4, 8.1
        // Every rule object in the JSON output must have a "mandatory" field that is a boolean.
        GenModel
            .Sample(
                model =>
                {
                    var json = InspectModelWriter.Write(model);
                    using var doc = JsonDocument.Parse(json);
                    var rules = doc.RootElement.GetProperty("rules").EnumerateArray().ToList();

                    Assert.Equal(model.Rules.Count, rules.Count);

                    foreach (var ruleEl in rules)
                    {
                        Assert.True(ruleEl.TryGetProperty("mandatory", out var mandatoryProp),
                            "Rule object is missing 'mandatory' field");
                        Assert.True(
                            mandatoryProp.ValueKind == JsonValueKind.True ||
                            mandatoryProp.ValueKind == JsonValueKind.False,
                            $"'mandatory' field is not a boolean, got {mandatoryProp.ValueKind}");
                    }
                },
                iter: 100,
                print: model => $"rules.Count={model.Rules.Count}");
    }

    [Fact]
    public void NoRuleObject_ContainsForbiddenFields()
    {
        // Validates: Requirements 4.4, 8.2
        // No rule object in the JSON output may contain severity, domain, profile, or supersedes fields.
        GenModel
            .Sample(
                model =>
                {
                    var json = InspectModelWriter.Write(model);
                    using var doc = JsonDocument.Parse(json);
                    var rules = doc.RootElement.GetProperty("rules").EnumerateArray().ToList();

                    foreach (var ruleEl in rules)
                    {
                        foreach (var forbidden in ForbiddenRuleFields)
                        {
                            Assert.False(ruleEl.TryGetProperty(forbidden, out _),
                                $"Rule object unexpectedly contains forbidden field '{forbidden}'");
                        }
                    }
                },
                iter: 100,
                print: model => $"rules.Count={model.Rules.Count}");
    }

    [Fact]
    public void Rules_AreSortedByIdInOutput()
    {
        // Validates: Requirements 8.3
        // Rules in the JSON output must be sorted by ID using ordinal string comparison.
        GenModel
            .Sample(
                model =>
                {
                    var json = InspectModelWriter.Write(model);
                    using var doc = JsonDocument.Parse(json);
                    var ruleIds = doc.RootElement.GetProperty("rules")
                        .EnumerateArray()
                        .Select(r => r.GetProperty("id").GetString()!)
                        .ToList();

                    var sorted = ruleIds.OrderBy(id => id, StringComparer.Ordinal).ToList();

                    Assert.Equal(sorted, ruleIds);
                },
                iter: 100,
                print: model => $"rules.Count={model.Rules.Count}, ruleIds=[{string.Join(",", model.Rules.Select(r => r.Id))}]");
    }

    [Fact]
    public void InspectJsonSchema_CombinedProperty()
    {
        // Validates: Requirements 4.4, 8.1, 8.2, 8.3
        // Combined property: for any ResolvedSteeringModel, the JSON output includes
        // mandatory boolean for every rule, excludes forbidden fields, and rules are sorted by ID.
        GenModel
            .Sample(
                model =>
                {
                    var json = InspectModelWriter.Write(model);
                    using var doc = JsonDocument.Parse(json);
                    var rules = doc.RootElement.GetProperty("rules").EnumerateArray().ToList();

                    // All rules present
                    Assert.Equal(model.Rules.Count, rules.Count);

                    var previousId = string.Empty;

                    foreach (var ruleEl in rules)
                    {
                        // 1. mandatory field is present and is a boolean
                        Assert.True(ruleEl.TryGetProperty("mandatory", out var mandatoryProp),
                            "Rule object is missing 'mandatory' field");
                        Assert.True(
                            mandatoryProp.ValueKind == JsonValueKind.True ||
                            mandatoryProp.ValueKind == JsonValueKind.False,
                            $"'mandatory' field is not a boolean, got {mandatoryProp.ValueKind}");

                        // 2. No forbidden fields
                        foreach (var forbidden in ForbiddenRuleFields)
                        {
                            Assert.False(ruleEl.TryGetProperty(forbidden, out _),
                                $"Rule object unexpectedly contains forbidden field '{forbidden}'");
                        }

                        // 3. Rules are sorted by ID
                        var currentId = ruleEl.GetProperty("id").GetString()!;
                        Assert.True(
                            string.Compare(previousId, currentId, StringComparison.Ordinal) <= 0,
                            $"Rules not sorted: '{previousId}' should come before '{currentId}'");
                        previousId = currentId;
                    }
                },
                iter: 100,
                print: model => $"rules.Count={model.Rules.Count}");
    }
}
