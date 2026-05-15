using CsCheck;
using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.PropertyTests.Generation;

/// <summary>
/// Property tests for mandatory filter semantics in <see cref="RouteResolver"/>.
/// Feature: simplify-rule-attributes, Property 6a: Mandatory filter semantics
/// Validates: Requirements 5.2
/// </summary>
public sealed class MandatoryFilterProperties
{
    // ── Generators ───────────────────────────────────────────────────────────────

    private static readonly Gen<string?> GenCategory =
        Gen.OneOf(
            Gen.Const((string?)null),
            Gen.Const((string?)"core"),
            Gen.Const((string?)"security"),
            Gen.Const((string?)"quality"),
            Gen.String[Gen.Char.AlphaNumeric, 3, 12].Select(s => (string?)s));

    private static readonly Gen<bool> GenMandatory =
        Gen.Bool;

    private static readonly Gen<IReadOnlyList<string>> GenTags =
        Gen.String[Gen.Char.AlphaNumeric, 2, 8]
           .Array[0, 3]
           .Select(arr => (IReadOnlyList<string>)arr.ToList());

    /// <summary>
    /// Generates a SteeringRule with a randomized Mandatory value.
    /// Category is set to a known value so that category filtering does not interfere.
    /// </summary>
    private static readonly Gen<SteeringRule> GenRule =
        Gen.Select(GenMandatory, GenCategory, GenTags)
           .Select((mandatory, category, tags) =>
               new SteeringRule
               {
                   Id = "R-TEST",
                   Category = category ?? "test-category",
                   Mandatory = mandatory,
                   Tags = tags,
               });

    /// <summary>
    /// Generates a RouteMatchExpression that does not filter on category or tags,
    /// isolating the mandatory filter behavior.
    /// </summary>
    private static readonly Gen<RouteMatchExpression> GenExprMandatoryOnly =
        Gen.OneOf(
            Gen.Const((bool?)null),
            Gen.Const((bool?)true),
            Gen.Const((bool?)false))
           .Select(mandatory =>
               new RouteMatchExpression
               {
                   Category = [],
                   Mandatory = mandatory,
                   TagsAny = [],
               });

    // ── Property 6a: Mandatory filter semantics ─────────────────────────────────
    //
    // For any SteeringRule with Mandatory = M and for any RouteMatchExpression:
    // - When expr.Mandatory is null, the expression SHALL match the rule regardless of M.
    // - When expr.Mandatory is true, the expression SHALL match the rule if and only if M is true.
    // - When expr.Mandatory is false, the expression SHALL match the rule if and only if M is false.

    [Fact]
    public void NullMandatoryFilter_MatchesAllRules()
    {
        // Validates: Requirements 5.2
        // When expr.Mandatory is null, the expression matches the rule regardless of M.
        GenRule
            .Sample(
                rule =>
                {
                    var expr = new RouteMatchExpression
                    {
                        Category = [],
                        Mandatory = null,
                        TagsAny = [],
                    };

                    var result = RouteResolver.Matches(expr, rule);

                    Assert.True(result,
                        $"Expected null mandatory filter to match rule with Mandatory={rule.Mandatory}");
                },
                iter: 100,
                print: rule => $"rule.Mandatory={rule.Mandatory}, rule.Category={rule.Category}");
    }

    [Fact]
    public void TrueMandatoryFilter_MatchesOnlyMandatoryRules()
    {
        // Validates: Requirements 5.2
        // When expr.Mandatory is true, the expression matches the rule if and only if M is true.
        GenRule
            .Sample(
                rule =>
                {
                    var expr = new RouteMatchExpression
                    {
                        Category = [],
                        Mandatory = true,
                        TagsAny = [],
                    };

                    var result = RouteResolver.Matches(expr, rule);

                    if (rule.Mandatory)
                        Assert.True(result,
                            "Expected true mandatory filter to match mandatory rule");
                    else
                        Assert.False(result,
                            "Expected true mandatory filter to NOT match non-mandatory rule");
                },
                iter: 100,
                print: rule => $"rule.Mandatory={rule.Mandatory}, rule.Category={rule.Category}");
    }

    [Fact]
    public void FalseMandatoryFilter_MatchesOnlyNonMandatoryRules()
    {
        // Validates: Requirements 5.2
        // When expr.Mandatory is false, the expression matches the rule if and only if M is false.
        GenRule
            .Sample(
                rule =>
                {
                    var expr = new RouteMatchExpression
                    {
                        Category = [],
                        Mandatory = false,
                        TagsAny = [],
                    };

                    var result = RouteResolver.Matches(expr, rule);

                    if (!rule.Mandatory)
                        Assert.True(result,
                            "Expected false mandatory filter to match non-mandatory rule");
                    else
                        Assert.False(result,
                            "Expected false mandatory filter to NOT match mandatory rule");
                },
                iter: 100,
                print: rule => $"rule.Mandatory={rule.Mandatory}, rule.Category={rule.Category}");
    }

    [Fact]
    public void MandatoryFilterSemantics_CombinedProperty()
    {
        // Validates: Requirements 5.2
        // Combined property: for any rule and any expression with isolated mandatory filter,
        // the match result equals (filter is null) OR (filter.Value == rule.Mandatory).
        Gen.Select(GenRule, GenExprMandatoryOnly)
            .Sample(
                (rule, expr) =>
                {
                    var result = RouteResolver.Matches(expr, rule);

                    var expected = expr.Mandatory is null || expr.Mandatory.Value == rule.Mandatory;

                    Assert.Equal(expected, result);
                },
                iter: 100,
                print: t => $"rule.Mandatory={t.Item1.Mandatory}, expr.Mandatory={t.Item2.Mandatory?.ToString() ?? "null"}");
    }
}
