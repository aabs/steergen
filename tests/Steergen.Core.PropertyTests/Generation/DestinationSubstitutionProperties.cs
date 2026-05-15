using CsCheck;
using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.PropertyTests.Generation;

/// <summary>
/// Property tests for destination template substitution in <see cref="RouteResolver"/>.
/// Feature: simplify-rule-attributes, Property 7: Category template substitution
/// Validates: Requirements 2.8, 3.6, 5.4
/// </summary>
public sealed class DestinationSubstitutionProperties
{
    // ── Generators ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a non-null, non-empty category value (alphanumeric, 2-15 chars).
    /// </summary>
    private static readonly Gen<string> GenNonNullCategory =
        Gen.String[Gen.Char.AlphaNumeric, 2, 15];

    /// <summary>
    /// Generates a rule ID (alphanumeric with prefix).
    /// </summary>
    private static readonly Gen<string> GenRuleId =
        Gen.String[Gen.Char.AlphaNumeric, 1, 10]
           .Select(s => $"R-{s}");

    /// <summary>
    /// Generates an optional input file stem.
    /// </summary>
    private static readonly Gen<string?> GenInputFileStem =
        Gen.OneOf(
            Gen.Const((string?)null),
            Gen.String[Gen.Char.AlphaNumeric, 3, 12].Select(s => (string?)s));

    /// <summary>
    /// Generates a SteeringRule with a guaranteed non-null Category.
    /// </summary>
    private static readonly Gen<SteeringRule> GenRuleWithCategory =
        Gen.Select(GenRuleId, GenNonNullCategory, Gen.Bool, GenInputFileStem)
           .Select((id, category, mandatory, inputFileStem) =>
               new SteeringRule
               {
                   Id = id,
                   Category = category,
                   Mandatory = mandatory,
                   InputFileStem = inputFileStem,
               });

    /// <summary>
    /// Generates a static path segment (no variable tokens) for use as prefix/suffix.
    /// </summary>
    private static readonly Gen<string> GenPathSegment =
        Gen.String[Gen.Char.AlphaNumeric, 1, 8];

    /// <summary>
    /// Generates a file extension string.
    /// </summary>
    private static readonly Gen<string> GenExtension =
        Gen.OneOf(
            Gen.Const(".md"),
            Gen.Const(".txt"),
            Gen.Const(".yaml"));

    // ── Property 7: Category template substitution ──────────────────────────────
    //
    // For any SteeringRule with a non-null Category value and for any
    // DestinationTemplate containing ${category}, the resolved destination path
    // SHALL contain the rule's category value in place of the ${category} token.
    // The tokens ${domain}, ${severity}, and ${profile} SHALL resolve to empty string.

    [Fact]
    public void CategoryToken_ResolvesToRuleCategoryValue()
    {
        // Validates: Requirements 2.8, 3.6, 5.4
        // ${category} in directory or fileName resolves to the rule's category value.
        Gen.Select(GenRuleWithCategory, GenPathSegment, GenPathSegment, GenExtension)
            .Sample(
                (rule, prefix, suffix, ext) =>
                {
                    var dest = new DestinationTemplate
                    {
                        Directory = $"{prefix}/${{category}}/{suffix}",
                        FileName = "${category}",
                        Extension = ext,
                    };

                    var result = RouteResolver.ResolveDestination(dest, rule);

                    // The resolved path must contain the rule's category value
                    Assert.Contains(rule.Category!, result);

                    // The resolved path must NOT contain the literal token
                    Assert.DoesNotContain("${category}", result);

                    // Verify the directory portion resolved correctly
                    var expectedDir = $"{prefix}/{rule.Category}/{suffix}";
                    var expectedFileName = rule.Category;
                    var expectedPath = $"{expectedDir}/{expectedFileName}{ext}";
                    Assert.Equal(expectedPath, result);
                },
                iter: 100,
                print: t => $"rule.Category={t.Item1.Category}, prefix={t.Item2}, suffix={t.Item3}, ext={t.Item4}");
    }

    [Fact]
    public void DomainToken_ResolvesToEmptyString()
    {
        // Validates: Requirements 2.8, 3.6, 5.4
        // ${domain} in destination template resolves to empty string.
        Gen.Select(GenRuleWithCategory, GenPathSegment, GenExtension)
            .Sample(
                (rule, prefix, ext) =>
                {
                    var dest = new DestinationTemplate
                    {
                        Directory = prefix,
                        FileName = $"${{domain}}-output",
                        Extension = ext,
                    };

                    var result = RouteResolver.ResolveDestination(dest, rule);

                    // ${domain} resolves to empty string, so fileName becomes "-output"
                    Assert.DoesNotContain("${domain}", result);
                    Assert.Contains("-output", result);
                    Assert.Equal($"{prefix}/-output{ext}", result);
                },
                iter: 100,
                print: t => $"rule.Category={t.Item1.Category}, prefix={t.Item2}, ext={t.Item3}");
    }

    [Fact]
    public void SeverityToken_ResolvesToEmptyString()
    {
        // Validates: Requirements 2.8, 3.6, 5.4
        // ${severity} in destination template resolves to empty string.
        Gen.Select(GenRuleWithCategory, GenPathSegment, GenExtension)
            .Sample(
                (rule, prefix, ext) =>
                {
                    var dest = new DestinationTemplate
                    {
                        Directory = prefix,
                        FileName = $"${{severity}}-output",
                        Extension = ext,
                    };

                    var result = RouteResolver.ResolveDestination(dest, rule);

                    // ${severity} resolves to empty string, so fileName becomes "-output"
                    Assert.DoesNotContain("${severity}", result);
                    Assert.Contains("-output", result);
                    Assert.Equal($"{prefix}/-output{ext}", result);
                },
                iter: 100,
                print: t => $"rule.Category={t.Item1.Category}, prefix={t.Item2}, ext={t.Item3}");
    }

    [Fact]
    public void ProfileToken_ResolvesToEmptyString()
    {
        // Validates: Requirements 2.8, 3.6, 5.4
        // ${profile} in destination template resolves to empty string.
        Gen.Select(GenRuleWithCategory, GenPathSegment, GenExtension)
            .Sample(
                (rule, prefix, ext) =>
                {
                    var dest = new DestinationTemplate
                    {
                        Directory = prefix,
                        FileName = $"${{profile}}-output",
                        Extension = ext,
                    };

                    var result = RouteResolver.ResolveDestination(dest, rule);

                    // ${profile} resolves to empty string, so fileName becomes "-output"
                    Assert.DoesNotContain("${profile}", result);
                    Assert.Contains("-output", result);
                    Assert.Equal($"{prefix}/-output{ext}", result);
                },
                iter: 100,
                print: t => $"rule.Category={t.Item1.Category}, prefix={t.Item2}, ext={t.Item3}");
    }

    [Fact]
    public void AllLegacyTokens_ResolveToEmptyWhileCategoryResolves()
    {
        // Validates: Requirements 2.8, 3.6, 5.4
        // Combined property: a template using all legacy tokens and ${category}
        // resolves ${category} to the rule's value and all legacy tokens to empty string.
        Gen.Select(GenRuleWithCategory, GenExtension)
            .Sample(
                (rule, ext) =>
                {
                    var dest = new DestinationTemplate
                    {
                        Directory = "${domain}/${severity}/${profile}/${category}",
                        FileName = "${category}",
                        Extension = ext,
                    };

                    var result = RouteResolver.ResolveDestination(dest, rule);

                    // Legacy tokens resolve to empty, category resolves to value
                    var expectedDir = $"///{rule.Category}";
                    var expectedFileName = rule.Category;
                    var expectedPath = $"{expectedDir}/{expectedFileName}{ext}";
                    Assert.Equal(expectedPath, result);

                    // No unresolved tokens remain for the known variables
                    Assert.DoesNotContain("${domain}", result);
                    Assert.DoesNotContain("${severity}", result);
                    Assert.DoesNotContain("${profile}", result);
                    Assert.DoesNotContain("${category}", result);
                },
                iter: 100,
                print: t => $"rule.Category={t.Item1.Category}, ext={t.Item2}");
    }
}
