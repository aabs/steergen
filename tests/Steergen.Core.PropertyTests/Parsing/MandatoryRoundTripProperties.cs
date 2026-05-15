using CsCheck;
using Steergen.Core.Model;
using Steergen.Core.Parsing;

namespace Steergen.Core.PropertyTests.Parsing;

/// <summary>
/// Property tests for the Mandatory attribute on SteeringRule.
/// Feature: simplify-rule-attributes, Property 1: Mandatory attribute round-trip preservation
/// Validates: Requirements 1.1, 1.6
/// </summary>
public sealed class MandatoryRoundTripProperties
{
    // ── Requirement 1.1: Default-constructed SteeringRule has Mandatory == false ──

    [Fact]
    public void DefaultConstructed_SteeringRule_HasMandatoryFalse()
    {
        var rule = new SteeringRule();
        Assert.False(rule.Mandatory);
    }

    // ── Requirement 1.1: SteeringRule does not expose removed properties ─────────

    [Fact]
    public void SteeringRule_DoesNotExpose_RemovedProperties()
    {
        var ruleType = typeof(SteeringRule);

        Assert.Null(ruleType.GetProperty("Severity"));
        Assert.Null(ruleType.GetProperty("Domain"));
        Assert.Null(ruleType.GetProperty("Profile"));
        Assert.Null(ruleType.GetProperty("Supersedes"));
    }

    // ── Property 1: Mandatory attribute round-trip preservation ──────────────────
    //
    // For any valid SteeringRule with Mandatory set to either true or false,
    // serializing the rule to a :::rule block (with mandatory="true" when true,
    // omitted when false), then parsing that block back, SHALL produce a rule
    // with the same Mandatory value.

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

    private static readonly Gen<string> GenTag =
        Gen.String[Gen.Char.AlphaNumeric, 2, 8];

    private static readonly Gen<IReadOnlyList<string>> GenTags =
        GenTag.Array[0, 4].Select(a => (IReadOnlyList<string>)a);

    private static readonly Gen<string> GenPrimaryText =
        Gen.String[Gen.Char.AlphaNumeric, 5, 50]
           .Select(s => s.Replace('\n', ' ').Replace('\r', ' '));

    private static readonly Gen<bool> GenMandatory = Gen.Bool;

    /// <summary>
    /// Generates a :::rule block string from the given attributes.
    /// When mandatory is true, includes mandatory="true" in the attribute string.
    /// When mandatory is false, omits the mandatory attribute (relying on default).
    /// </summary>
    private static string SerializeToRuleBlock(
        string id,
        bool mandatory,
        string? category,
        IReadOnlyList<string> tags,
        string primaryText)
    {
        var attrs = new List<string> { $"id=\"{id}\"" };

        if (mandatory)
            attrs.Add("mandatory=\"true\"");

        if (category is not null)
            attrs.Add($"category=\"{category}\"");

        if (tags.Count > 0)
            attrs.Add($"tags=\"{string.Join(",", tags)}\"");

        var attrString = string.Join(" ", attrs);
        return $":::rule {attrString}\n{primaryText}\n:::";
    }

    [Fact]
    public void MandatoryAttribute_RoundTrip_PreservesValue()
    {
        // Property 1: Mandatory attribute round-trip preservation
        // Validates: Requirements 1.1, 1.6
        Gen.Select(GenRuleId, GenMandatory, GenCategory, GenTags, GenPrimaryText)
            .Sample(
                (id, mandatory, category, tags, primaryText) =>
                {
                    // Serialize to a :::rule block
                    var ruleBlock = SerializeToRuleBlock(id, mandatory, category, tags, primaryText);

                    // Parse the block back
                    var doc = SteeringMarkdownParser.Parse(ruleBlock, "test.md");

                    // Verify round-trip preservation
                    Assert.Single(doc.Rules);
                    var parsedRule = doc.Rules[0];
                    Assert.Equal(mandatory, parsedRule.Mandatory);
                    Assert.Equal(id, parsedRule.Id);
                },
                iter: 200,
                print: t => $"id={t.Item1}, mandatory={t.Item2}, category={t.Item3}, tags=[{string.Join(",", t.Item4)}]");
    }

    [Fact]
    public void MandatoryTrue_RoundTrip_AlwaysPreserved()
    {
        // Focused test: mandatory=true always round-trips correctly
        // Validates: Requirements 1.1, 1.6
        Gen.Select(GenRuleId, GenCategory, GenPrimaryText)
            .Sample(
                (id, category, primaryText) =>
                {
                    var ruleBlock = SerializeToRuleBlock(id, true, category, [], primaryText);
                    var doc = SteeringMarkdownParser.Parse(ruleBlock, "test.md");

                    Assert.Single(doc.Rules);
                    Assert.True(doc.Rules[0].Mandatory,
                        $"Expected Mandatory=true after round-trip for rule '{id}'");
                },
                iter: 100);
    }

    [Fact]
    public void MandatoryFalse_RoundTrip_AlwaysPreserved()
    {
        // Focused test: mandatory=false (omitted) always round-trips correctly
        // Validates: Requirements 1.1, 1.6
        Gen.Select(GenRuleId, GenCategory, GenPrimaryText)
            .Sample(
                (id, category, primaryText) =>
                {
                    var ruleBlock = SerializeToRuleBlock(id, false, category, [], primaryText);
                    var doc = SteeringMarkdownParser.Parse(ruleBlock, "test.md");

                    Assert.Single(doc.Rules);
                    Assert.False(doc.Rules[0].Mandatory,
                        $"Expected Mandatory=false after round-trip for rule '{id}'");
                },
                iter: 100);
    }
}
