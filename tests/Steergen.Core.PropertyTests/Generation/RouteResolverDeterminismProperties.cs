using CsCheck;
using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.PropertyTests.Generation;

/// <summary>
/// Property tests for route resolution determinism in <see cref="RouteResolver"/>.
/// Feature: simplify-rule-attributes, Property 10: Route resolution determinism
/// Validates: Requirements 5.5, 8.3
/// </summary>
public sealed class RouteResolverDeterminismProperties
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

    private static readonly Gen<string?> GenInputFileStem =
        Gen.OneOf(
            Gen.Const((string?)null),
            Gen.String[Gen.Char.AlphaNumeric, 3, 12].Select(s => (string?)s));

    private static readonly Gen<RouteScope> GenRouteScope =
        Gen.OneOf(
            Gen.Const(RouteScope.Global),
            Gen.Const(RouteScope.Project),
            Gen.Const(RouteScope.Both));

    /// <summary>
    /// Generates a SteeringRule with all properties randomized.
    /// </summary>
    private static readonly Gen<SteeringRule> GenRule =
        Gen.Select(GenRuleId, GenCategory, GenMandatory, GenTags, GenAppliesTo, GenInputFileStem, GenPrimaryText, GenRouteScope)
           .Select((id, category, mandatory, tags, appliesTo, inputFileStem, primaryText, scope) =>
               new SteeringRule
               {
                   Id = id,
                   Category = category,
                   Mandatory = mandatory,
                   Tags = tags,
                   AppliesTo = appliesTo,
                   InputFileStem = inputFileStem,
                   PrimaryText = primaryText,
                   SourceScope = scope,
               });

    // ── Route and Layout Generators ─────────────────────────────────────────────

    private static readonly Gen<string> GenRouteId =
        Gen.String[Gen.Char.AlphaNumeric, 3, 10]
           .Select(s => $"route-{s}");

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
           .Array[0, 3]
           .Select(arr => (IReadOnlyList<string>)arr.ToList());

    private static readonly Gen<RouteMatchExpression> GenExpression =
        Gen.Select(GenExprCategory, GenExprMandatory, GenExprTagsAny)
           .Select((category, mandatory, tagsAny) =>
               new RouteMatchExpression
               {
                   Category = category,
                   Mandatory = mandatory,
                   TagsAny = tagsAny,
               });

    private static readonly Gen<string> GenPathSegment =
        Gen.String[Gen.Char.AlphaNumeric, 1, 8];

    private static readonly Gen<string> GenExtension =
        Gen.OneOf(
            Gen.Const(".md"),
            Gen.Const(".txt"),
            Gen.Const(".yaml"));

    private static readonly Gen<DestinationTemplate> GenDestination =
        Gen.Select(GenPathSegment, GenPathSegment, GenExtension)
           .Select((dir, fileName, ext) =>
               new DestinationTemplate
               {
                   Directory = dir,
                   FileName = fileName,
                   Extension = ext,
               });

    private static readonly Gen<bool> GenExplicit =
        Gen.Bool;

    private static readonly Gen<int> GenOrder =
        Gen.Int[0, 100];

    /// <summary>
    /// Generates a single RouteRuleDefinition with randomized match expression and destination.
    /// </summary>
    private static readonly Gen<RouteRuleDefinition> GenRouteRule =
        Gen.Select(GenRouteId, GenRouteScope, GenExplicit, GenExpression, GenDestination, GenOrder)
           .Select((id, scope, isExplicit, match, dest, order) =>
               new RouteRuleDefinition
               {
                   Id = id,
                   Scope = scope,
                   Explicit = isExplicit,
                   Match = match,
                   Destination = dest,
                   Order = order,
               });

    /// <summary>
    /// Generates a TargetLayoutDefinition with 1-5 random routes.
    /// </summary>
    private static readonly Gen<TargetLayoutDefinition> GenLayout =
        GenRouteRule.Array[1, 5]
           .Select(routes =>
               new TargetLayoutDefinition
               {
                   TargetId = "test-target",
                   Routes = routes.ToList(),
               });

    // ── Property 10: Route resolution determinism ───────────────────────────────
    //
    // For any SteeringRule and TargetLayoutDefinition, calling RouteResolver.Resolve
    // multiple times with the same inputs SHALL produce identical RouteResolutionResult
    // values (same SelectedRouteId, same SelectedDestinationPath).

    [Fact]
    public void Resolve_ProducesIdenticalResults_WhenCalledMultipleTimes()
    {
        // **Validates: Requirements 5.5, 8.3**
        // Calling Resolve multiple times with the same rule and layout produces
        // identical SelectedRouteId and SelectedDestinationPath each time.
        Gen.Select(GenRule, GenLayout)
            .Sample(
                (rule, layout) =>
                {
                    var resolver = new RouteResolver();

                    var result1 = resolver.Resolve(rule, layout);
                    var result2 = resolver.Resolve(rule, layout);
                    var result3 = resolver.Resolve(rule, layout);

                    // All three calls must produce the same SelectedRouteId
                    Assert.Equal(result1.SelectedRouteId, result2.SelectedRouteId);
                    Assert.Equal(result1.SelectedRouteId, result3.SelectedRouteId);

                    // All three calls must produce the same SelectedDestinationPath
                    Assert.Equal(result1.SelectedDestinationPath, result2.SelectedDestinationPath);
                    Assert.Equal(result1.SelectedDestinationPath, result3.SelectedDestinationPath);

                    // All three calls must agree on resolution status
                    Assert.Equal(result1.IsResolved, result2.IsResolved);
                    Assert.Equal(result1.IsResolved, result3.IsResolved);
                },
                iter: 100,
                print: t => $"rule.Id={t.Item1.Id}, rule.Category={t.Item1.Category ?? "(null)"}, layout.Routes.Count={t.Item2.Routes.Count}");
    }

    [Fact]
    public void Resolve_ProducesIdenticalMatchedRouteIds_WhenCalledMultipleTimes()
    {
        // **Validates: Requirements 5.5, 8.3**
        // The full set of matched route IDs is also deterministic across calls.
        Gen.Select(GenRule, GenLayout)
            .Sample(
                (rule, layout) =>
                {
                    var resolver = new RouteResolver();

                    var result1 = resolver.Resolve(rule, layout);
                    var result2 = resolver.Resolve(rule, layout);
                    var result3 = resolver.Resolve(rule, layout);

                    // MatchedRouteIds must be identical across all calls
                    Assert.Equal(result1.MatchedRouteIds, result2.MatchedRouteIds);
                    Assert.Equal(result1.MatchedRouteIds, result3.MatchedRouteIds);
                },
                iter: 100,
                print: t => $"rule.Id={t.Item1.Id}, rule.Category={t.Item1.Category ?? "(null)"}, layout.Routes.Count={t.Item2.Routes.Count}");
    }

    [Fact]
    public void Resolve_WithExplicitScope_ProducesIdenticalResults()
    {
        // **Validates: Requirements 5.5, 8.3**
        // Determinism holds when using the explicit scope overload as well.
        Gen.Select(GenRule, GenLayout, GenRouteScope)
            .Sample(
                (rule, layout, scope) =>
                {
                    var resolver = new RouteResolver();

                    var result1 = resolver.Resolve(rule, layout, scope);
                    var result2 = resolver.Resolve(rule, layout, scope);
                    var result3 = resolver.Resolve(rule, layout, scope);

                    Assert.Equal(result1.SelectedRouteId, result2.SelectedRouteId);
                    Assert.Equal(result1.SelectedRouteId, result3.SelectedRouteId);

                    Assert.Equal(result1.SelectedDestinationPath, result2.SelectedDestinationPath);
                    Assert.Equal(result1.SelectedDestinationPath, result3.SelectedDestinationPath);

                    Assert.Equal(result1.IsResolved, result2.IsResolved);
                    Assert.Equal(result1.IsResolved, result3.IsResolved);

                    Assert.Equal(result1.MatchedRouteIds, result2.MatchedRouteIds);
                    Assert.Equal(result1.MatchedRouteIds, result3.MatchedRouteIds);
                },
                iter: 100,
                print: t => $"rule.Id={t.Item1.Id}, rule.Category={t.Item1.Category ?? "(null)"}, scope={t.Item3}, layout.Routes.Count={t.Item2.Routes.Count}");
    }
}
