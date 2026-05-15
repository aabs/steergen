using CsCheck;
using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.PropertyTests.Generation;

/// <summary>
/// Property tests for category-based route matching in <see cref="RouteResolver"/>.
/// Feature: simplify-rule-attributes, Property 6: Route matching depends only on category, mandatory, and tags
/// Validates: Requirements 2.5, 2.7, 3.4, 5.2, 12.3, 12.4
/// </summary>
public sealed class CategoryRoutingProperties
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

    private static readonly Gen<string?> GenPrimaryText =
        Gen.OneOf(
            Gen.Const((string?)null),
            Gen.String[Gen.Char.AlphaNumeric, 5, 50].Select(s => (string?)s));

    private static readonly Gen<IReadOnlyList<string>> GenAppliesTo =
        Gen.String[Gen.Char.AlphaNumeric, 3, 10]
           .Array[0, 3]
           .Select(arr => (IReadOnlyList<string>)arr.ToList());

    private static readonly Gen<bool> GenDeprecated =
        Gen.Bool;

    private static readonly Gen<IReadOnlyList<string>> GenExprCategory =
        Gen.OneOf(
            Gen.Const((IReadOnlyList<string>)Array.Empty<string>()),
            Gen.Const((IReadOnlyList<string>)new[] { "*" }),
            Gen.Const((IReadOnlyList<string>)new[] { "core" }),
            Gen.Const((IReadOnlyList<string>)new[] { "security" }),
            Gen.Const((IReadOnlyList<string>)new[] { "quality" }),
            Gen.Const((IReadOnlyList<string>)new[] { "api" }),
            Gen.String[Gen.Char.AlphaNumeric, 3, 12].Select(s => (IReadOnlyList<string>)new[] { s }));

    private static readonly Gen<bool?> GenExprMandatory =
        Gen.OneOf(
            Gen.Const((bool?)null),
            Gen.Const((bool?)true),
            Gen.Const((bool?)false));

    private static readonly Gen<IReadOnlyList<string>> GenExprTagsAny =
        Gen.String[Gen.Char.AlphaNumeric, 2, 8]
           .Array[0, 4]
           .Select(arr => (IReadOnlyList<string>)arr.ToList());

    /// <summary>
    /// Generates a SteeringRule with all properties randomized.
    /// </summary>
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

    /// <summary>
    /// Generates a RouteMatchExpression with randomized filter fields.
    /// </summary>
    private static readonly Gen<RouteMatchExpression> GenExpression =
        Gen.Select(GenExprCategory, GenExprMandatory, GenExprTagsAny)
           .Select((category, mandatory, tagsAny) =>
               new RouteMatchExpression
               {
                   Category = category,
                   Mandatory = mandatory,
                   TagsAny = tagsAny,
               });

    // ── Property 6: Route matching depends only on category, mandatory, and tags ─
    //
    // For any SteeringRule and for any RouteMatchExpression, the result of
    // Matches(expr, rule) SHALL depend only on the rule's Category, Mandatory,
    // and Tags values (and the expression's Category, Mandatory, TagsAny, and
    // SourceContext fields). Changing any other rule metadata (e.g., Id, AppliesTo,
    // Deprecated, PrimaryText) SHALL not affect the match result.

    [Fact]
    public void Matches_IsIndependentOfRuleId()
    {
        // Validates: Requirements 2.5, 2.7, 3.4, 12.3, 12.4
        // Changing the rule's Id does not affect the match result.
        Gen.Select(GenRule, GenExpression, GenRuleId)
            .Sample(
                (rule, expr, altId) =>
                {
                    var originalResult = RouteResolver.Matches(expr, rule);
                    var modifiedRule = rule with { Id = altId };
                    var modifiedResult = RouteResolver.Matches(expr, modifiedRule);

                    Assert.Equal(originalResult, modifiedResult);
                },
                iter: 100,
                print: t => $"rule.Id={t.Item1.Id}, expr.Category=[{string.Join(",", t.Item2.Category)}], altId={t.Item3}");
    }

    [Fact]
    public void Matches_IsIndependentOfAppliesTo()
    {
        // Validates: Requirements 2.5, 2.7, 3.4, 12.3, 12.4
        // Changing the rule's AppliesTo does not affect the match result.
        Gen.Select(GenRule, GenExpression, GenAppliesTo)
            .Sample(
                (rule, expr, altAppliesTo) =>
                {
                    var originalResult = RouteResolver.Matches(expr, rule);
                    var modifiedRule = rule with { AppliesTo = altAppliesTo };
                    var modifiedResult = RouteResolver.Matches(expr, modifiedRule);

                    Assert.Equal(originalResult, modifiedResult);
                },
                iter: 100,
                print: t => $"rule.Id={t.Item1.Id}, expr.Category=[{string.Join(",", t.Item2.Category)}], altAppliesTo=[{string.Join(",", t.Item3)}]");
    }

    [Fact]
    public void Matches_IsIndependentOfDeprecated()
    {
        // Validates: Requirements 2.5, 2.7, 3.4, 12.3, 12.4
        // Changing the rule's Deprecated flag does not affect the match result.
        Gen.Select(GenRule, GenExpression)
            .Sample(
                (rule, expr) =>
                {
                    var originalResult = RouteResolver.Matches(expr, rule);
                    var modifiedRule = rule with { Deprecated = !rule.Deprecated };
                    var modifiedResult = RouteResolver.Matches(expr, modifiedRule);

                    Assert.Equal(originalResult, modifiedResult);
                },
                iter: 100,
                print: t => $"rule.Id={t.Item1.Id}, rule.Deprecated={t.Item1.Deprecated}, expr.Category=[{string.Join(",", t.Item2.Category)}]");
    }

    [Fact]
    public void Matches_IsIndependentOfPrimaryText()
    {
        // Validates: Requirements 2.5, 2.7, 3.4, 12.3, 12.4
        // Changing the rule's PrimaryText does not affect the match result.
        Gen.Select(GenRule, GenExpression, GenPrimaryText)
            .Sample(
                (rule, expr, altPrimaryText) =>
                {
                    var originalResult = RouteResolver.Matches(expr, rule);
                    var modifiedRule = rule with { PrimaryText = altPrimaryText };
                    var modifiedResult = RouteResolver.Matches(expr, modifiedRule);

                    Assert.Equal(originalResult, modifiedResult);
                },
                iter: 100,
                print: t => $"rule.Id={t.Item1.Id}, expr.Category=[{string.Join(",", t.Item2.Category)}], altPrimaryText={t.Item3 ?? "(null)"}");
    }

    [Fact]
    public void Matches_DependsOnlyOnCategoryMandatoryAndTags()
    {
        // Validates: Requirements 2.5, 2.7, 3.4, 5.2, 12.3, 12.4
        // Combined property: two rules with the same Category, Mandatory, and Tags
        // but different Id, AppliesTo, Deprecated, and PrimaryText produce the same
        // match result for any expression.
        var genRulePair = Gen.Select(GenCategory, GenMandatory, GenTags, GenRule, GenRule)
            .Select((category, mandatory, tags, baseRule1, baseRule2) => (
                Rule1: baseRule1 with { Category = category, Mandatory = mandatory, Tags = tags },
                Rule2: baseRule2 with { Category = category, Mandatory = mandatory, Tags = tags }
            ));

        Gen.Select(genRulePair, GenExpression)
            .Sample(
                (rulePair, expr) =>
                {
                    var result1 = RouteResolver.Matches(expr, rulePair.Rule1);
                    var result2 = RouteResolver.Matches(expr, rulePair.Rule2);

                    Assert.Equal(result1, result2);
                },
                iter: 100,
                print: t => $"rule1.Id={t.Item1.Rule1.Id}, rule2.Id={t.Item1.Rule2.Id}, category={t.Item1.Rule1.Category ?? "(null)"}, expr.Category=[{string.Join(",", t.Item2.Category)}]");
    }
}
