using CsCheck;
using Steergen.Core.Parsing;

namespace Steergen.Core.PropertyTests.Parsing;

/// <summary>
/// Property tests for mandatory attribute parsing correctness.
/// Feature: simplify-rule-attributes, Property 2: Mandatory parsing correctness
/// Validates: Requirements 1.3, 1.4
/// </summary>
public sealed class MandatoryParsingProperties
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
            Gen.String[Gen.Char.AlphaNumeric, 3, 12]);

    private static readonly Gen<string> GenPrimaryText =
        Gen.String[Gen.Char.AlphaNumeric, 5, 50]
           .Select(s => s.Replace('\n', ' ').Replace('\r', ' '));

    /// <summary>
    /// Generates case variations of "true" that should all parse as Mandatory=true.
    /// </summary>
    private static readonly Gen<string> GenTrueCaseVariant =
        Gen.OneOf(
            Gen.Const("true"),
            Gen.Const("True"),
            Gen.Const("TRUE"),
            Gen.Const("tRuE"),
            Gen.Const("TrUe"));

    /// <summary>
    /// Generates values that are NOT "true" (case-insensitive) and should result in Mandatory=false.
    /// </summary>
    private static readonly Gen<string> GenNonTrueValue =
        Gen.OneOf(
            Gen.Const("false"),
            Gen.Const("False"),
            Gen.Const("FALSE"),
            Gen.Const("yes"),
            Gen.Const("Yes"),
            Gen.Const("YES"),
            Gen.Const("no"),
            Gen.Const("1"),
            Gen.Const("0"),
            Gen.Const("on"),
            Gen.Const("off"),
            Gen.Const("enabled"),
            Gen.Const("mandatory"),
            Gen.Const(""),
            Gen.String[Gen.Char.AlphaNumeric, 1, 8]
               .Where(s => !s.Equals("true", StringComparison.OrdinalIgnoreCase)));

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static string BuildRuleBlock(string id, string? mandatoryAttr, string? category, string primaryText)
    {
        var attrs = new List<string> { $"id=\"{id}\"" };

        if (mandatoryAttr is not null)
            attrs.Add($"mandatory=\"{mandatoryAttr}\"");

        if (category is not null)
            attrs.Add($"category=\"{category}\"");

        var attrString = string.Join(" ", attrs);
        return $":::rule {attrString}\n{primaryText}\n:::";
    }

    // ── Property 2: Mandatory parsing correctness ────────────────────────────────
    //
    // For any :::rule block with valid attributes, the parsed Mandatory value
    // SHALL equal true if and only if the attribute string contains
    // mandatory="true" (case-insensitive on the value). In all other cases
    // (attribute absent, or any value other than "true"), Mandatory SHALL be false.

    [Fact]
    public void MandatoryTrue_WhenValueIsTrueCaseInsensitive()
    {
        // Validates: Requirements 1.3
        // When mandatory="true" (any case), Mandatory SHALL be true
        Gen.Select(GenRuleId, GenTrueCaseVariant, GenCategory, GenPrimaryText)
            .Sample(
                (id, trueVariant, category, primaryText) =>
                {
                    var ruleBlock = BuildRuleBlock(id, trueVariant, category, primaryText);
                    var doc = SteeringMarkdownParser.Parse(ruleBlock, "test.md");

                    Assert.Single(doc.Rules);
                    Assert.True(doc.Rules[0].Mandatory,
                        $"Expected Mandatory=true for mandatory=\"{trueVariant}\" but got false");
                },
                iter: 100,
                print: t => $"id={t.Item1}, mandatoryValue=\"{t.Item2}\", category={t.Item3}");
    }

    [Fact]
    public void MandatoryFalse_WhenAttributeAbsent()
    {
        // Validates: Requirements 1.4
        // When mandatory attribute is absent, Mandatory SHALL be false
        Gen.Select(GenRuleId, GenCategory, GenPrimaryText)
            .Sample(
                (id, category, primaryText) =>
                {
                    var ruleBlock = BuildRuleBlock(id, null, category, primaryText);
                    var doc = SteeringMarkdownParser.Parse(ruleBlock, "test.md");

                    Assert.Single(doc.Rules);
                    Assert.False(doc.Rules[0].Mandatory,
                        $"Expected Mandatory=false when attribute is absent but got true");
                },
                iter: 100,
                print: t => $"id={t.Item1}, category={t.Item2}");
    }

    [Fact]
    public void MandatoryFalse_WhenValueIsNotTrue()
    {
        // Validates: Requirements 1.3, 1.4
        // When mandatory attribute has any value other than "true" (case-insensitive),
        // Mandatory SHALL be false
        Gen.Select(GenRuleId, GenNonTrueValue, GenCategory, GenPrimaryText)
            .Sample(
                (id, nonTrueValue, category, primaryText) =>
                {
                    var ruleBlock = BuildRuleBlock(id, nonTrueValue, category, primaryText);
                    var doc = SteeringMarkdownParser.Parse(ruleBlock, "test.md");

                    Assert.Single(doc.Rules);
                    Assert.False(doc.Rules[0].Mandatory,
                        $"Expected Mandatory=false for mandatory=\"{nonTrueValue}\" but got true");
                },
                iter: 100,
                print: t => $"id={t.Item1}, mandatoryValue=\"{t.Item2}\", category={t.Item3}");
    }

    [Fact]
    public void MandatoryParsing_BiconditionalProperty()
    {
        // Validates: Requirements 1.3, 1.4
        // Combined property: Mandatory == true IFF value is "true" (case-insensitive)
        // This is the full biconditional statement of Property 2.
        var genMandatoryValue = Gen.OneOf(
            GenTrueCaseVariant.Select(v => (string?)v),
            GenNonTrueValue.Select(v => (string?)v),
            Gen.Const((string?)null));

        Gen.Select(GenRuleId, genMandatoryValue, GenCategory, GenPrimaryText)
            .Sample(
                (id, mandatoryValue, category, primaryText) =>
                {
                    var ruleBlock = BuildRuleBlock(id, mandatoryValue, category, primaryText);
                    var doc = SteeringMarkdownParser.Parse(ruleBlock, "test.md");

                    Assert.Single(doc.Rules);

                    var expectedMandatory = mandatoryValue is not null
                        && mandatoryValue.Equals("true", StringComparison.OrdinalIgnoreCase);

                    Assert.Equal(expectedMandatory, doc.Rules[0].Mandatory);
                },
                iter: 200,
                print: t => $"id={t.Item1}, mandatoryValue={t.Item2 ?? "(absent)"}, category={t.Item3}");
    }
}
