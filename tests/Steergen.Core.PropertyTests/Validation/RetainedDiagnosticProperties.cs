using CsCheck;
using Steergen.Core.Model;
using Steergen.Core.Validation;

namespace Steergen.Core.PropertyTests.Validation;

/// <summary>
/// Property tests verifying that retained diagnostics (V001, V002, V005, V006, V007) still fire
/// when their violation conditions are met.
/// Feature: simplify-rule-attributes, Property 5: Retained diagnostics still fire
/// **Validates: Requirements 7.4, 7.5**
/// </summary>
public sealed class RetainedDiagnosticProperties
{
    // ── Generators ───────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenRuleId =
        Gen.String[Gen.Char.AlphaNumeric, 1, 12]
           .Select(s => $"R-{s}");

    private static readonly Gen<string> GenDocId =
        Gen.String[Gen.Char.AlphaNumeric, 3, 12]
           .Select(s => $"doc-{s}");

    private static readonly Gen<string> GenSourcePath =
        Gen.String[Gen.Char.AlphaNumeric, 3, 15]
           .Select(s => $"/steering/{s}.md");

    private static readonly Gen<string?> GenCategory =
        Gen.OneOf(
            Gen.Const((string?)"core"),
            Gen.Const((string?)"security"),
            Gen.Const((string?)"quality"),
            Gen.String[Gen.Char.AlphaNumeric, 3, 15].Select(s => (string?)s),
            Gen.Const((string?)null));

    private static readonly Gen<bool> GenMandatory = Gen.Bool;

    private static readonly Gen<string> GenValidPrimaryText =
        Gen.String[Gen.Char.AlphaNumeric, 5, 80]
           .Select(s => s.Replace('\n', ' ').Replace('\r', ' '));

    private static readonly Gen<IReadOnlyList<string>> GenTags =
        Gen.String[Gen.Char.AlphaNumeric, 2, 10]
           .List[0, 3]
           .Select(list => (IReadOnlyList<string>)list);

    private static readonly Gen<IReadOnlyList<string>> GenAppliesTo =
        Gen.OneOf(
            Gen.Const("backend"),
            Gen.Const("frontend"),
            Gen.Const("infra"))
           .List[0, 2]
           .Select(list => (IReadOnlyList<string>)list);

    /// <summary>
    /// Generates a fully valid SteeringRule (has ID, has non-empty primary text, no control chars).
    /// </summary>
    private static readonly Gen<SteeringRule> GenValidRule =
        Gen.Select(GenRuleId, GenMandatory, GenCategory, GenValidPrimaryText, GenTags, GenAppliesTo)
           .Select((id, mandatory, category, primaryText, tags, appliesTo) =>
               new SteeringRule
               {
                   Id = id,
                   Mandatory = mandatory,
                   Category = category,
                   PrimaryText = primaryText,
                   Tags = tags,
                   AppliesTo = appliesTo,
               });

    // ── V001: Document missing an ID ─────────────────────────────────────────────

    [Fact]
    public void V001_DocumentMissingId_ProducesDiagnostic()
    {
        // **Validates: Requirements 7.4, 7.5**
        // For any SteeringDocument with a null or whitespace-only Id,
        // the validator SHALL produce diagnostic V001.
        var genDocMissingId = Gen.Select(
            Gen.OneOf(Gen.Const((string?)null), Gen.Const((string?)""), Gen.Const((string?)"   ")),
            GenSourcePath,
            GenValidRule.List[0, 3])
           .Select((id, sourcePath, rules) =>
               new SteeringDocument
               {
                   Id = id,
                   SourcePath = sourcePath,
                   Rules = rules,
               });

        genDocMissingId.Sample(
            document =>
            {
                var validator = new SteeringValidator();
                var diagnostics = validator.Validate(document);

                Assert.Contains(diagnostics, d => d.Code == "V001");
            },
            iter: 100,
            print: doc => $"docId={doc.Id ?? "(null)"}, sourcePath={doc.SourcePath ?? "(null)"}, ruleCount={doc.Rules.Count}");
    }

    // ── V002: Rule missing an ID ─────────────────────────────────────────────────

    [Fact]
    public void V002_RuleMissingId_ProducesDiagnostic()
    {
        // **Validates: Requirements 7.4, 7.5**
        // For any SteeringDocument containing a rule with null or whitespace-only Id,
        // the validator SHALL produce diagnostic V002.
        var genRuleMissingId = Gen.Select(GenMandatory, GenCategory, GenValidPrimaryText)
           .Select((mandatory, category, primaryText) =>
               new SteeringRule
               {
                   Id = null,
                   Mandatory = mandatory,
                   Category = category,
                   PrimaryText = primaryText,
               });

        var genDocWithMissingRuleId = Gen.Select(
            GenDocId,
            GenSourcePath,
            genRuleMissingId,
            GenValidRule.List[0, 3])
           .Select((docId, sourcePath, badRule, otherRules) =>
               new SteeringDocument
               {
                   Id = docId,
                   SourcePath = sourcePath,
                   Rules = [badRule, .. otherRules],
               });

        genDocWithMissingRuleId.Sample(
            document =>
            {
                var validator = new SteeringValidator();
                var diagnostics = validator.Validate(document);

                Assert.Contains(diagnostics, d => d.Code == "V002");
            },
            iter: 100,
            print: doc => $"docId={doc.Id ?? "(null)"}, sourcePath={doc.SourcePath ?? "(null)"}, ruleCount={doc.Rules.Count}");
    }

    // ── V005: Rule with empty body text ──────────────────────────────────────────

    [Fact]
    public void V005_RuleWithEmptyBody_ProducesDiagnostic()
    {
        // **Validates: Requirements 7.4, 7.5**
        // For any SteeringDocument containing a rule with null or whitespace-only PrimaryText,
        // the validator SHALL produce diagnostic V005.
        var genRuleEmptyBody = Gen.Select(
            GenRuleId,
            GenMandatory,
            GenCategory,
            Gen.OneOf(Gen.Const((string?)null), Gen.Const((string?)""), Gen.Const((string?)"   ")))
           .Select((id, mandatory, category, primaryText) =>
               new SteeringRule
               {
                   Id = id,
                   Mandatory = mandatory,
                   Category = category,
                   PrimaryText = primaryText,
               });

        var genDocWithEmptyBody = Gen.Select(
            GenDocId,
            GenSourcePath,
            genRuleEmptyBody,
            GenValidRule.List[0, 3])
           .Select((docId, sourcePath, badRule, otherRules) =>
               new SteeringDocument
               {
                   Id = docId,
                   SourcePath = sourcePath,
                   Rules = [badRule, .. otherRules],
               });

        genDocWithEmptyBody.Sample(
            document =>
            {
                var validator = new SteeringValidator();
                var diagnostics = validator.Validate(document);

                Assert.Contains(diagnostics, d => d.Code == "V005");
            },
            iter: 100,
            print: doc => $"docId={doc.Id ?? "(null)"}, sourcePath={doc.SourcePath ?? "(null)"}, ruleCount={doc.Rules.Count}");
    }

    // ── V006: Rule with control characters in primary text ───────────────────────

    [Fact]
    public void V006_RuleWithControlChars_ProducesDiagnostic()
    {
        // **Validates: Requirements 7.4, 7.5**
        // For any SteeringDocument containing a rule with control characters (other than \n, \r, \t)
        // in PrimaryText, the validator SHALL produce diagnostic V006.

        // Generate control characters that should trigger V006 (0x00-0x1F excluding \n=0x0A, \r=0x0D, \t=0x09)
        var controlChars = Enumerable.Range(0, 32)
            .Where(c => c != '\n' && c != '\r' && c != '\t')
            .Select(c => (char)c)
            .ToArray();

        var genControlChar = Gen.OneOf(controlChars.Select(Gen.Const).ToArray());

        var genTextWithControlChar = Gen.Select(
            Gen.String[Gen.Char.AlphaNumeric, 2, 20],
            genControlChar,
            Gen.String[Gen.Char.AlphaNumeric, 2, 20])
           .Select((prefix, ctrl, suffix) => $"{prefix}{ctrl}{suffix}");

        var genRuleWithControlChars = Gen.Select(
            GenRuleId,
            GenMandatory,
            GenCategory,
            genTextWithControlChar)
           .Select((id, mandatory, category, primaryText) =>
               new SteeringRule
               {
                   Id = id,
                   Mandatory = mandatory,
                   Category = category,
                   PrimaryText = primaryText,
               });

        var genDocWithControlChars = Gen.Select(
            GenDocId,
            GenSourcePath,
            genRuleWithControlChars,
            GenValidRule.List[0, 3])
           .Select((docId, sourcePath, badRule, otherRules) =>
               new SteeringDocument
               {
                   Id = docId,
                   SourcePath = sourcePath,
                   Rules = [badRule, .. otherRules],
               });

        genDocWithControlChars.Sample(
            document =>
            {
                var validator = new SteeringValidator();
                var diagnostics = validator.Validate(document);

                Assert.Contains(diagnostics, d => d.Code == "V006");
            },
            iter: 100,
            print: doc => $"docId={doc.Id ?? "(null)"}, sourcePath={doc.SourcePath ?? "(null)"}, ruleCount={doc.Rules.Count}");
    }

    // ── V007: Duplicate rule IDs across corpus ───────────────────────────────────

    [Fact]
    public void V007_DuplicateRuleIds_ProducesDiagnostic()
    {
        // **Validates: Requirements 7.4, 7.5**
        // For any corpus containing two or more rules with the same ID (across documents),
        // the validator SHALL produce diagnostic V007.
        var genCorpusWithDuplicates = Gen.Select(
            GenRuleId,
            GenDocId,
            GenDocId,
            GenSourcePath,
            GenSourcePath,
            GenMandatory,
            GenCategory,
            GenValidPrimaryText)
           .Select((sharedId, docId1, docId2, path1, path2, mandatory, category, text1) =>
           {
               var rule1 = new SteeringRule
               {
                   Id = sharedId,
                   Mandatory = mandatory,
                   Category = category,
                   PrimaryText = text1,
               };
               var rule2 = new SteeringRule
               {
                   Id = sharedId,
                   Mandatory = !mandatory,
                   Category = category,
                   PrimaryText = "Another rule with same ID.",
               };
               var doc1 = new SteeringDocument
               {
                   Id = docId1,
                   SourcePath = path1,
                   Rules = [rule1],
               };
               var doc2 = new SteeringDocument
               {
                   Id = docId2,
                   SourcePath = path2,
                   Rules = [rule2],
               };
               return new List<SteeringDocument> { doc1, doc2 };
           });

        genCorpusWithDuplicates.Sample(
            corpus =>
            {
                var validator = new SteeringValidator();
                var diagnostics = validator.ValidateCorpus(corpus);

                Assert.Contains(diagnostics, d => d.Code == "V007");
            },
            iter: 100,
            print: corpus => $"corpusSize={corpus.Count}, sharedRuleId={corpus[0].Rules[0].Id}");
    }

    // ── Combined: Mandatory value does not affect retained diagnostics ───────────

    [Fact]
    public void RetainedDiagnostics_FireRegardlessOfMandatoryValue()
    {
        // **Validates: Requirements 7.4, 7.5**
        // Verifies that the mandatory flag (true or false) does not suppress any retained diagnostic.
        // For any rule violating V002 or V005, the diagnostic fires regardless of Mandatory value.
        var genMandatoryVariant = Gen.Select(
            GenDocId,
            GenSourcePath,
            GenMandatory,
            GenCategory)
           .Select((docId, sourcePath, mandatory, category) =>
               new SteeringDocument
               {
                   Id = docId,
                   SourcePath = sourcePath,
                   Rules =
                   [
                       // Rule missing ID (V002)
                       new SteeringRule
                       {
                           Id = null,
                           Mandatory = mandatory,
                           Category = category,
                           PrimaryText = "Some valid text",
                       },
                       // Rule with empty body (V005)
                       new SteeringRule
                       {
                           Id = $"R-{docId}",
                           Mandatory = mandatory,
                           Category = category,
                           PrimaryText = null,
                       },
                   ],
               });

        genMandatoryVariant.Sample(
            document =>
            {
                var validator = new SteeringValidator();
                var diagnostics = validator.Validate(document);

                Assert.Contains(diagnostics, d => d.Code == "V002");
                Assert.Contains(diagnostics, d => d.Code == "V005");
            },
            iter: 100,
            print: doc => $"docId={doc.Id ?? "(null)"}, mandatory={doc.Rules[0].Mandatory}");
    }
}
