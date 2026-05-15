using CsCheck;
using Steergen.Core.Model;
using Steergen.Core.Parsing;

namespace Steergen.Core.PropertyTests.Parsing;

/// <summary>
/// Property tests for legacy attribute backward compatibility.
/// Feature: simplify-rule-attributes, Property 3: Legacy attribute backward compatibility
/// Validates: Requirements 1.5, 2.2, 3.2, 4.2, 11.1, 11.2, 11.3, 11.4
/// </summary>
public sealed class LegacyAttributeProperties
{
    // ── Generators ───────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenRuleId =
        Gen.String[Gen.Char.AlphaNumeric, 1, 10]
           .Select(s => $"R-{s}");

    private static readonly Gen<string> GenCategory =
        Gen.OneOf(
            Gen.Const("core"),
            Gen.Const("security"),
            Gen.Const("quality"),
            Gen.Const("contextual-information"),
            Gen.String[Gen.Char.AlphaNumeric, 3, 12]);

    private static readonly Gen<string> GenPrimaryText =
        Gen.String[Gen.Char.AlphaNumeric, 5, 50]
           .Select(s => s.Replace('\n', ' ').Replace('\r', ' '));

    private static readonly Gen<string> GenTag =
        Gen.String[Gen.Char.AlphaNumeric, 2, 8];

    private static readonly Gen<IReadOnlyList<string>> GenTags =
        GenTag.Array[0, 3].Select(a => (IReadOnlyList<string>)a);

    // Legacy attribute generators
    private static readonly Gen<string> GenSeverityValue =
        Gen.OneOf(
            Gen.Const("error"),
            Gen.Const("warning"),
            Gen.Const("info"),
            Gen.Const("hint"));

    private static readonly Gen<string> GenDomainValue =
        Gen.OneOf(
            Gen.Const("core"),
            Gen.Const("security"),
            Gen.Const("operations"),
            Gen.String[Gen.Char.AlphaNumeric, 3, 12]);

    private static readonly Gen<string> GenProfileValue =
        Gen.OneOf(
            Gen.Const("default"),
            Gen.Const("strict"),
            Gen.Const("relaxed"),
            Gen.String[Gen.Char.AlphaNumeric, 3, 10]);

    private static readonly Gen<string> GenSupersedesValue =
        Gen.String[Gen.Char.AlphaNumeric, 1, 8]
           .Select(s => $"R-{s}");

    // New attribute generators
    private static readonly Gen<string> GenMandatoryValue =
        Gen.OneOf(
            Gen.Const("true"),
            Gen.Const("True"),
            Gen.Const("TRUE"),
            Gen.Const("false"),
            Gen.Const("False"));

    private static readonly Gen<string> GenAppliesTo =
        Gen.OneOf(
            Gen.Const("backend"),
            Gen.Const("frontend"),
            Gen.Const("all"),
            Gen.String[Gen.Char.AlphaNumeric, 3, 8]);

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a :::rule block with a random combination of legacy and new attributes.
    /// </summary>
    private static string BuildRuleBlock(
        string id,
        string? severity,
        string? domain,
        string? profile,
        string? supersedes,
        string? mandatory,
        string? category,
        IReadOnlyList<string>? tags,
        string? appliesTo,
        bool? deprecated,
        string primaryText)
    {
        var attrs = new List<string> { $"id=\"{id}\"" };

        // Legacy attributes (randomly included)
        if (severity is not null)
            attrs.Add($"severity=\"{severity}\"");
        if (domain is not null)
            attrs.Add($"domain=\"{domain}\"");
        if (profile is not null)
            attrs.Add($"profile=\"{profile}\"");
        if (supersedes is not null)
            attrs.Add($"supersedes=\"{supersedes}\"");

        // New attributes
        if (mandatory is not null)
            attrs.Add($"mandatory=\"{mandatory}\"");
        if (category is not null)
            attrs.Add($"category=\"{category}\"");
        if (tags is not null && tags.Count > 0)
            attrs.Add($"tags=\"{string.Join(",", tags)}\"");
        if (appliesTo is not null)
            attrs.Add($"appliesTo=\"{appliesTo}\"");
        if (deprecated == true)
            attrs.Add("deprecated=\"true\"");

        var attrString = string.Join(" ", attrs);
        return $":::rule {attrString}\n{primaryText}\n:::";
    }

    // ── Property 3: Legacy attribute backward compatibility ──────────────────────
    //
    // For any document containing :::rule blocks with any combination of legacy
    // attributes (severity, domain, profile, supersedes) and/or new attributes
    // (mandatory, category, id, tags, appliesTo, deprecated), the parser SHALL
    // produce a valid SteeringRule without error, with Mandatory defaulting to
    // false when the mandatory attribute is absent, and with no model properties
    // reflecting the legacy attribute values.

    [Fact]
    public void Parser_DoesNotThrow_WhenLegacyAttributesPresent()
    {
        // Validates: Requirements 1.5, 2.2, 3.2, 4.2, 11.1
        // Parser SHALL parse documents with legacy attributes without error
        Gen.Select(
            GenRuleId,
            GenSeverityValue.Null(),
            GenDomainValue.Null(),
            GenProfileValue.Null(),
            GenSupersedesValue.Null(),
            GenCategory,
            GenPrimaryText)
            .Sample(
                (id, severity, domain, profile, supersedes, category, primaryText) =>
                {
                    var ruleBlock = BuildRuleBlock(
                        id, severity, domain, profile, supersedes,
                        mandatory: null, category: category, tags: null,
                        appliesTo: null, deprecated: null, primaryText: primaryText);

                    // Parser SHALL NOT throw when legacy attributes are present
                    var doc = SteeringMarkdownParser.Parse(ruleBlock, "test.md");

                    Assert.NotNull(doc);
                    Assert.Single(doc.Rules);
                    Assert.Equal(id, doc.Rules[0].Id);
                },
                iter: 100,
                print: t => $"id={t.Item1}, severity={t.Item2 ?? "(absent)"}, domain={t.Item3 ?? "(absent)"}, profile={t.Item4 ?? "(absent)"}, supersedes={t.Item5 ?? "(absent)"}");
    }

    [Fact]
    public void Mandatory_DefaultsFalse_WhenAbsent_RegardlessOfLegacyAttributes()
    {
        // Validates: Requirements 1.5, 11.3
        // When mandatory attribute is absent, Mandatory SHALL be false even if
        // severity or other legacy attributes are present
        Gen.Select(
            GenRuleId,
            GenSeverityValue,
            GenDomainValue,
            GenProfileValue.Null(),
            GenSupersedesValue.Null(),
            GenCategory,
            GenPrimaryText)
            .Sample(
                (id, severity, domain, profile, supersedes, category, primaryText) =>
                {
                    var ruleBlock = BuildRuleBlock(
                        id, severity, domain, profile, supersedes,
                        mandatory: null, category: category, tags: null,
                        appliesTo: null, deprecated: null, primaryText: primaryText);

                    var doc = SteeringMarkdownParser.Parse(ruleBlock, "test.md");

                    Assert.Single(doc.Rules);
                    Assert.False(doc.Rules[0].Mandatory,
                        $"Expected Mandatory=false when mandatory attribute is absent (severity=\"{severity}\" was present but should be ignored)");
                },
                iter: 100,
                print: t => $"id={t.Item1}, severity={t.Item2}, domain={t.Item3}");
    }

    [Fact]
    public void NoModelProperties_ReflectLegacyValues()
    {
        // Validates: Requirements 11.2
        // The parser SHALL NOT populate any removed fields on the SteeringRule model
        // SteeringRule no longer has Severity, Domain, Profile, Supersedes properties
        var ruleType = typeof(SteeringRule);

        // Confirm the model does not expose legacy properties
        Assert.Null(ruleType.GetProperty("Severity"));
        Assert.Null(ruleType.GetProperty("Domain"));
        Assert.Null(ruleType.GetProperty("Profile"));
        Assert.Null(ruleType.GetProperty("Supersedes"));

        // Now verify parsing with legacy attributes does not cause any issues
        Gen.Select(
            GenRuleId,
            GenSeverityValue,
            GenDomainValue,
            GenProfileValue,
            GenSupersedesValue,
            GenCategory,
            GenPrimaryText)
            .Sample(
                (id, severity, domain, profile, supersedes, category, primaryText) =>
                {
                    var ruleBlock = BuildRuleBlock(
                        id, severity, domain, profile, supersedes,
                        mandatory: null, category: category, tags: null,
                        appliesTo: null, deprecated: null, primaryText: primaryText);

                    var doc = SteeringMarkdownParser.Parse(ruleBlock, "test.md");

                    Assert.Single(doc.Rules);
                    var rule = doc.Rules[0];

                    // Verify the rule is valid and has expected new-model properties
                    Assert.Equal(id, rule.Id);
                    Assert.Equal(category, rule.Category);
                    Assert.False(rule.Mandatory);
                },
                iter: 100,
                print: t => $"id={t.Item1}, severity={t.Item2}, domain={t.Item3}, profile={t.Item4}, supersedes={t.Item5}");
    }

    [Fact]
    public void MixedLegacyAndNewAttributes_ParseCorrectly()
    {
        // Validates: Requirements 1.5, 2.2, 3.2, 4.2, 11.1, 11.2, 11.3, 11.4
        // For ALL documents containing any combination of legacy and new attributes,
        // the parser SHALL produce a deterministic, valid parse result
        Gen.Select(
            GenRuleId,
            GenSeverityValue.Null(),
            GenDomainValue.Null(),
            GenProfileValue.Null(),
            GenSupersedesValue.Null(),
            GenMandatoryValue.Null(),
            GenCategory.Null(),
            GenTags)
            .SelectMany(t => Gen.Select(GenAppliesTo.Null(), GenPrimaryText)
                .Select(extra => (t.Item1, t.Item2, t.Item3, t.Item4, t.Item5, t.Item6, t.Item7, t.Item8, extra.Item1, extra.Item2)))
            .Sample(
                t =>
                {
                    var (id, severity, domain, profile, supersedes, mandatory, category, tags, appliesTo, primaryText) = t;

                    var ruleBlock = BuildRuleBlock(
                        id, severity, domain, profile, supersedes,
                        mandatory, category, tags, appliesTo,
                        deprecated: null, primaryText: primaryText);

                    var doc = SteeringMarkdownParser.Parse(ruleBlock, "test.md");

                    Assert.NotNull(doc);
                    Assert.Single(doc.Rules);
                    var rule = doc.Rules[0];

                    // Rule ID is always preserved
                    Assert.Equal(id, rule.Id);

                    // Mandatory is true IFF mandatory="true" (case-insensitive)
                    var expectedMandatory = mandatory is not null
                        && mandatory.Equals("true", StringComparison.OrdinalIgnoreCase);
                    Assert.Equal(expectedMandatory, rule.Mandatory);

                    // Category is preserved when present
                    Assert.Equal(category, rule.Category);

                    // Tags are preserved when present
                    if (tags.Count > 0)
                    {
                        Assert.Equal(tags.Count, rule.Tags.Count);
                    }

                    // AppliesTo is preserved when present
                    if (appliesTo is not null)
                    {
                        Assert.Contains(appliesTo, rule.AppliesTo);
                    }
                },
                iter: 100,
                print: t => $"id={t.Item1}, severity={t.Item2 ?? "(absent)"}, domain={t.Item3 ?? "(absent)"}, profile={t.Item4 ?? "(absent)"}, supersedes={t.Item5 ?? "(absent)"}, mandatory={t.Item6 ?? "(absent)"}, category={t.Item7 ?? "(absent)"}");
    }

    [Fact]
    public void DeterministicParsing_WithLegacyAttributes()
    {
        // Validates: Requirements 11.4
        // Parsing the same document twice SHALL produce identical results
        Gen.Select(
            GenRuleId,
            GenSeverityValue.Null(),
            GenDomainValue.Null(),
            GenProfileValue.Null(),
            GenSupersedesValue.Null(),
            GenMandatoryValue.Null(),
            GenCategory,
            GenPrimaryText)
            .Sample(
                (id, severity, domain, profile, supersedes, mandatory, category, primaryText) =>
                {
                    var ruleBlock = BuildRuleBlock(
                        id, severity, domain, profile, supersedes,
                        mandatory, category, tags: null,
                        appliesTo: null, deprecated: null, primaryText: primaryText);

                    var doc1 = SteeringMarkdownParser.Parse(ruleBlock, "test.md");
                    var doc2 = SteeringMarkdownParser.Parse(ruleBlock, "test.md");

                    Assert.Single(doc1.Rules);
                    Assert.Single(doc2.Rules);

                    var rule1 = doc1.Rules[0];
                    var rule2 = doc2.Rules[0];

                    Assert.Equal(rule1.Id, rule2.Id);
                    Assert.Equal(rule1.Mandatory, rule2.Mandatory);
                    Assert.Equal(rule1.Category, rule2.Category);
                    Assert.Equal(rule1.Deprecated, rule2.Deprecated);
                    Assert.Equal(rule1.PrimaryText, rule2.PrimaryText);
                },
                iter: 100,
                print: t => $"id={t.Item1}, severity={t.Item2 ?? "(absent)"}, domain={t.Item3 ?? "(absent)"}, mandatory={t.Item6 ?? "(absent)"}");
    }
}
