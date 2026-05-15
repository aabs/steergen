using CsCheck;
using Steergen.Core.Model;
using Steergen.Core.Validation;

namespace Steergen.Core.PropertyTests.Validation;

/// <summary>
/// Property tests verifying that removed diagnostics (V003, V004, V008) are never produced.
/// Feature: simplify-rule-attributes, Property 4: Removed diagnostics never produced
/// Validates: Requirements 2.3, 4.3, 7.1, 7.2, 7.3
/// </summary>
public sealed class RemovedDiagnosticProperties
{
    private static readonly string[] RemovedCodes = ["V003", "V004", "V008"];

    // ── Generators ───────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenRuleId =
        Gen.String[Gen.Char.AlphaNumeric, 1, 12]
           .Select(s => $"R-{s}");

    private static readonly Gen<string?> GenNullableRuleId =
        Gen.OneOf(
            GenRuleId.Select(id => (string?)id),
            Gen.Const((string?)null));

    private static readonly Gen<string?> GenCategory =
        Gen.OneOf(
            Gen.Const((string?)"core"),
            Gen.Const((string?)"security"),
            Gen.Const((string?)"quality"),
            Gen.Const((string?)"operations"),
            Gen.String[Gen.Char.AlphaNumeric, 3, 15].Select(s => (string?)s),
            Gen.Const((string?)null));

    private static readonly Gen<string?> GenPrimaryText =
        Gen.OneOf(
            Gen.String[Gen.Char.AlphaNumeric, 5, 80]
               .Select(s => (string?)s.Replace('\n', ' ').Replace('\r', ' ')),
            Gen.Const((string?)null),
            Gen.Const((string?)""),
            Gen.Const((string?)"   "));

    private static readonly Gen<bool> GenMandatory = Gen.Bool;

    private static readonly Gen<bool> GenDeprecated = Gen.Bool;

    private static readonly Gen<RouteScope> GenRouteScope =
        Gen.OneOf(
            Gen.Const(RouteScope.Global),
            Gen.Const(RouteScope.Project),
            Gen.Const(RouteScope.Both));

    private static readonly Gen<IReadOnlyList<string>> GenTags =
        Gen.String[Gen.Char.AlphaNumeric, 2, 10]
           .List[0, 4]
           .Select(list => (IReadOnlyList<string>)list);

    private static readonly Gen<IReadOnlyList<string>> GenAppliesTo =
        Gen.OneOf(
            Gen.Const("backend"),
            Gen.Const("frontend"),
            Gen.Const("infra"),
            Gen.String[Gen.Char.AlphaNumeric, 3, 10])
           .List[0, 3]
           .Select(list => (IReadOnlyList<string>)list);

    /// <summary>
    /// Generates a random SteeringRule using the new simplified model (no Severity, Domain, Profile, Supersedes).
    /// </summary>
    private static readonly Gen<SteeringRule> GenRule =
        Gen.Select(GenNullableRuleId, GenMandatory, GenCategory, GenPrimaryText, GenDeprecated, GenRouteScope, GenTags, GenAppliesTo)
           .Select((id, mandatory, category, primaryText, deprecated, scope, tags, appliesTo) =>
               new SteeringRule
               {
                   Id = id,
                   Mandatory = mandatory,
                   Category = category,
                   PrimaryText = primaryText,
                   Deprecated = deprecated,
                   SourceScope = scope,
                   Tags = tags,
                   AppliesTo = appliesTo,
               });

    private static readonly Gen<string?> GenDocId =
        Gen.OneOf(
            Gen.String[Gen.Char.AlphaNumeric, 3, 12].Select(s => (string?)$"doc-{s}"),
            Gen.Const((string?)null));

    private static readonly Gen<string?> GenSourcePath =
        Gen.OneOf(
            Gen.String[Gen.Char.AlphaNumeric, 3, 15].Select(s => (string?)$"/steering/{s}.md"),
            Gen.Const((string?)null));

    /// <summary>
    /// Generates a random SteeringDocument with 0-5 rules.
    /// </summary>
    private static readonly Gen<SteeringDocument> GenDocument =
        Gen.Select(GenDocId, GenSourcePath, GenRule.List[0, 5])
           .Select((docId, sourcePath, rules) =>
               new SteeringDocument
               {
                   Id = docId,
                   SourcePath = sourcePath,
                   Rules = rules,
               });

    /// <summary>
    /// Generates a corpus of 1-5 documents.
    /// </summary>
    private static readonly Gen<List<SteeringDocument>> GenCorpus =
        GenDocument.List[1, 5];

    // ── Property 4: Removed diagnostics never produced ───────────────────────────
    //
    // For any valid or invalid SteeringRule and for any corpus of SteeringDocument
    // instances, the validator SHALL never produce diagnostics with codes V003, V004, or V008.

    [Fact]
    public void Validate_SingleDocument_NeverProducesRemovedDiagnostics()
    {
        // Validates: Requirements 2.3, 4.3, 7.1, 7.2, 7.3
        // **Validates: Requirements 2.3, 4.3, 7.1, 7.2, 7.3**
        GenDocument.Sample(
            document =>
            {
                var validator = new SteeringValidator();
                var diagnostics = validator.Validate(document);

                var removedDiagnostics = diagnostics
                    .Where(d => RemovedCodes.Contains(d.Code))
                    .ToList();

                Assert.Empty(removedDiagnostics);
            },
            iter: 100,
            print: doc => $"docId={doc.Id ?? "(null)"}, sourcePath={doc.SourcePath ?? "(null)"}, ruleCount={doc.Rules.Count}");
    }

    [Fact]
    public void ValidateCorpus_NeverProducesRemovedDiagnostics()
    {
        // Validates: Requirements 2.3, 4.3, 7.1, 7.2, 7.3
        // **Validates: Requirements 2.3, 4.3, 7.1, 7.2, 7.3**
        GenCorpus.Sample(
            corpus =>
            {
                var validator = new SteeringValidator();
                var diagnostics = validator.ValidateCorpus(corpus);

                var removedDiagnostics = diagnostics
                    .Where(d => RemovedCodes.Contains(d.Code))
                    .ToList();

                Assert.Empty(removedDiagnostics);
            },
            iter: 100,
            print: corpus => $"corpusSize={corpus.Count}, totalRules={corpus.Sum(d => d.Rules.Count)}");
    }

    [Fact]
    public void ValidateCorpus_WithMixOfValidAndInvalidRules_NeverProducesRemovedDiagnostics()
    {
        // Validates: Requirements 2.3, 4.3, 7.1, 7.2, 7.3
        // **Validates: Requirements 2.3, 4.3, 7.1, 7.2, 7.3**
        // This test specifically generates corpora that would have triggered V003/V004/V008
        // under the old model (missing IDs, empty text, duplicates) to ensure the removed
        // diagnostics are truly gone even when other diagnostics fire.
        var genProblematicRule = Gen.OneOf(
            // Rule with no ID (triggers V002)
            Gen.Select(GenMandatory, GenCategory, GenPrimaryText)
               .Select((mandatory, category, text) =>
                   new SteeringRule { Id = null, Mandatory = mandatory, Category = category, PrimaryText = text }),
            // Rule with empty body (triggers V005)
            Gen.Select(GenRuleId, GenMandatory, GenCategory)
               .Select((id, mandatory, category) =>
                   new SteeringRule { Id = id, Mandatory = mandatory, Category = category, PrimaryText = null }),
            // Normal valid rule
            GenRule);

        var genProblematicDoc = Gen.Select(GenDocId, GenSourcePath, genProblematicRule.List[1, 6])
            .Select((docId, sourcePath, rules) =>
                new SteeringDocument { Id = docId, SourcePath = sourcePath, Rules = rules });

        genProblematicDoc.List[1, 4].Sample(
            corpus =>
            {
                var validator = new SteeringValidator();
                var diagnostics = validator.ValidateCorpus(corpus);

                var removedDiagnostics = diagnostics
                    .Where(d => RemovedCodes.Contains(d.Code))
                    .ToList();

                Assert.Empty(removedDiagnostics);
            },
            iter: 100,
            print: corpus => $"corpusSize={corpus.Count}, totalRules={corpus.Sum(d => d.Rules.Count)}");
    }
}
