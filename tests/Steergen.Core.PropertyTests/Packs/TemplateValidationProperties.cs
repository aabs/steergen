using CsCheck;
using Scriban;
using Steergen.Core.Packs;
using Steergen.Core.Validation;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for template pack validation correctness.
///
/// Property 7: Template Pack Validation
/// For any string content, the template validation SHALL report it as valid if and only if
/// the Scriban parser can parse it without errors. Additionally, for any template file name
/// in a pack, validation SHALL report a warning if the file name does not match a known
/// template name for the declared target IDs.
///
/// **Validates: Requirements 6.1, 6.3**
/// </summary>
public sealed class TemplateValidationProperties
{
    // ── Generators ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates valid Scriban template strings (plain text, simple expressions, control flow).
    /// These should always parse without errors.
    /// </summary>
    private static readonly Gen<string> GenValidScribanTemplate =
        Gen.OneOf(
            // Plain text (no Scriban syntax)
            Gen.String[Gen.Char.AlphaNumeric, 1, 50],
            // Simple variable expressions
            Gen.String[Gen.Char['a', 'z'], 1, 10]
               .Select(name => $"Hello {{{{ {name} }}}}!"),
            // If/end blocks
            Gen.String[Gen.Char['a', 'z'], 1, 8]
               .Select(name => $"{{{{ if {name} }}}}yes{{{{ end }}}}"),
            // For/end blocks
            Gen.String[Gen.Char['a', 'z'], 1, 8]
               .Select(name => $"{{{{ for item in {name} }}}}{{{{ item }}}}{{{{ end }}}}"),
            // String literals
            Gen.String[Gen.Char.AlphaNumeric, 1, 20]
               .Select(s => $"{{{{ \"{s}\" }}}}"),
            // Pipe expressions
            Gen.String[Gen.Char['a', 'z'], 1, 8]
               .Select(name => $"{{{{ {name} | string.upcase }}}}"),
            // Multi-line templates with mixed content
            Gen.String[Gen.Char['a', 'z'], 1, 8]
               .Select(name => $"Header\n{{{{ {name} }}}}\nFooter"),
            // Empty string (valid — no template syntax)
            Gen.Const(""),
            // Whitespace only (valid — no template syntax)
            Gen.Const("   \n\t  "));

    /// <summary>
    /// Generates invalid Scriban template strings that should produce parse errors.
    /// These contain malformed Scriban syntax verified against Scriban 7.0.6 parser.
    /// Note: "{{ name" (unclosed delimiter) is actually valid Scriban (code block).
    /// </summary>
    private static readonly Gen<string> GenInvalidScribanTemplate =
        Gen.OneOf(
            // Unclosed if block (no end)
            Gen.Const("{{ if true }}yes"),
            // Unclosed for block (no end)
            Gen.Const("{{ for x in items }}item"),
            // Invalid expression syntax — missing condition
            Gen.Const("{{ if }}"),
            // Mismatched blocks — if not closed
            Gen.Const("{{ if true }}{{ for x in y }}{{ end }}"),
            // Unclosed string literal in expression
            Gen.Const("{{ \"unclosed }}"),
            // Invalid operator usage — missing right operand
            Gen.Const("{{ 1 + }}"),
            // Nested unclosed blocks — inner if closed but outer not
            Gen.Const("{{ if a }}{{ if b }}{{ end }}"),
            // Empty expression with pipe to nothing
            Gen.Const("{{ | }}"),
            // Unclosed func block
            Gen.Const("{{ func test }}body"));

    /// <summary>
    /// Generates random strings that may or may not be valid Scriban templates.
    /// Used for the biconditional property test.
    /// </summary>
    private static readonly Gen<string> GenArbitraryContent =
        Gen.OneOf(
            GenValidScribanTemplate,
            GenInvalidScribanTemplate,
            // Random strings with Scriban-like delimiters
            Gen.String[Gen.Char.AlphaNumeric, 0, 30]
               .Select(s => $"{{{{ {s} }}}}"),
            // Random strings without any Scriban syntax
            Gen.String[Gen.Char.AlphaNumeric, 0, 60]);

    /// <summary>
    /// Generates known built-in target IDs.
    /// </summary>
    private static readonly Gen<string> GenKnownTargetId =
        Gen.OneOf(
            Gen.Const("kiro"),
            Gen.Const("speckit"),
            Gen.Const("agents"),
            Gen.Const("copilot-agent"),
            Gen.Const("kiro-agent"));

    /// <summary>
    /// Generates unknown target IDs (not in the built-in set).
    /// </summary>
    private static readonly Gen<string> GenUnknownTargetId =
        Gen.String[Gen.Char['a', 'z'], 4, 12]
           .Where(s => s != "kiro" && s != "speckit" && s != "agents"
                    && s != "copilot-agent" && s != "kiro-agent");

    /// <summary>
    /// Generates known template names for specific built-in targets.
    /// </summary>
    private static Gen<string> GenKnownTemplateNameFor(string targetId) =>
        targetId switch
        {
            "kiro" => Gen.Const("document"),
            "speckit" => Gen.OneOf(Gen.Const("constitution"), Gen.Const("module")),
            "agents" => Gen.OneOf(Gen.Const("copilot.agent"), Gen.Const("kiro.agent")),
            "copilot-agent" => Gen.Const("copilot.agent"),
            "kiro-agent" => Gen.Const("kiro.agent"),
            _ => Gen.Const("document") // External targets default to "document"
        };

    /// <summary>
    /// Generates template names that are NOT known for any built-in target.
    /// </summary>
    private static readonly Gen<string> GenUnknownTemplateName =
        Gen.String[Gen.Char['a', 'z'], 4, 15]
           .Where(s => s != "document" && s != "constitution" && s != "module"
                    && s != "copilot.agent" && s != "kiro.agent");

    /// <summary>
    /// Generates a file path string for diagnostic reporting.
    /// </summary>
    private static readonly Gen<string> GenFilePath =
        Gen.Select(
            Gen.String[Gen.Char['a', 'z'], 3, 8],
            Gen.String[Gen.Char['a', 'z'], 3, 10])
        .Select((dir, name) => $"{dir}/{name}.scriban");

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Uses Scriban's parser directly to determine if content is valid.
    /// This is the oracle for the biconditional property.
    /// </summary>
    private static bool ScribanParserAccepts(string content)
    {
        var template = Template.Parse(content, "test.scriban");
        return !template.HasErrors;
    }

    // ── Property 7a: Valid Scriban templates produce no errors ────────────────────

    [Fact]
    public void ValidateTemplateContent_ProducesNoDiagnostics_ForValidScribanTemplates()
    {
        // **Validates: Requirements 6.1**
        //
        // For any valid Scriban template string, the template validation SHALL
        // report it as valid (no error diagnostics).
        Gen.Select(GenValidScribanTemplate, GenFilePath)
            .Sample(
                (content, filePath) =>
                {
                    var validator = new TemplatePackValidator();
                    var diagnostics = validator.ValidateTemplateContent(content, filePath);

                    var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                    Assert.Empty(errors);
                },
                iter: 100,
                print: t => $"content=\"{Truncate(t.Item1, 40)}\", file=\"{t.Item2}\"");
    }

    // ── Property 7b: Invalid Scriban templates produce errors ─────────────────────

    [Fact]
    public void ValidateTemplateContent_ProducesDiagnostics_ForInvalidScribanTemplates()
    {
        // **Validates: Requirements 6.1**
        //
        // For any invalid Scriban template string (one that the Scriban parser
        // cannot parse without errors), the template validation SHALL report
        // error diagnostics.
        Gen.Select(GenInvalidScribanTemplate, GenFilePath)
            .Sample(
                (content, filePath) =>
                {
                    var validator = new TemplatePackValidator();
                    var diagnostics = validator.ValidateTemplateContent(content, filePath);

                    var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                    Assert.NotEmpty(errors);
                    Assert.All(errors, d => Assert.Equal("TP003", d.Code));
                },
                iter: 100,
                print: t => $"content=\"{Truncate(t.Item1, 40)}\", file=\"{t.Item2}\"");
    }

    // ── Property 7c: Biconditional — valid iff Scriban parser succeeds ───────────

    [Fact]
    public void ValidateTemplateContent_ReportsValidIffScribanParserSucceeds()
    {
        // **Validates: Requirements 6.1**
        //
        // For any string content, the template validation SHALL report it as valid
        // if and only if the Scriban parser can parse it without errors.
        // This is the core biconditional property.
        Gen.Select(GenArbitraryContent, GenFilePath)
            .Sample(
                (content, filePath) =>
                {
                    var validator = new TemplatePackValidator();
                    var diagnostics = validator.ValidateTemplateContent(content, filePath);

                    var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                    var scribanAccepts = ScribanParserAccepts(content);

                    if (scribanAccepts)
                    {
                        Assert.Empty(errors);
                    }
                    else
                    {
                        Assert.NotEmpty(errors);
                    }
                },
                iter: 200,
                print: t => $"content=\"{Truncate(t.Item1, 40)}\", file=\"{t.Item2}\", scribanAccepts={ScribanParserAccepts(t.Item1)}");
    }

    // ── Property 7d: Known template names produce no warnings ────────────────────

    [Fact]
    public void ValidateTemplateName_ProducesNoWarning_ForKnownTemplateNames()
    {
        // **Validates: Requirements 6.3**
        //
        // For any template file name that matches a known template name for the
        // declared target ID, validation SHALL NOT report a warning.
        GenKnownTargetId.SelectMany(targetId =>
            GenKnownTemplateNameFor(targetId).Select(templateName => (targetId, templateName)))
        .Sample(
            pair =>
            {
                var (targetId, templateName) = pair;
                var validator = new TemplatePackValidator();
                var diagnostics = validator.ValidateTemplateName(templateName, targetId);

                var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
                Assert.Empty(warnings);
            },
            iter: 100,
            print: t => $"target=\"{t.Item1}\", template=\"{t.Item2}\"");
    }

    // ── Property 7e: Unknown template names produce warnings ─────────────────────

    [Fact]
    public void ValidateTemplateName_ProducesWarning_ForUnknownTemplateNames()
    {
        // **Validates: Requirements 6.3**
        //
        // For any template file name that does NOT match a known template name
        // for the declared target ID, validation SHALL report a warning.
        Gen.Select(GenKnownTargetId, GenUnknownTemplateName)
            .Sample(
                (targetId, templateName) =>
                {
                    var validator = new TemplatePackValidator();
                    var diagnostics = validator.ValidateTemplateName(templateName, targetId);

                    var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
                    Assert.NotEmpty(warnings);
                    Assert.All(warnings, d => Assert.Equal("TP006", d.Code));
                },
                iter: 100,
                print: t => $"target=\"{t.Item1}\", template=\"{t.Item2}\"");
    }

    // ── Property 7f: Unknown targets accept "document" as default ────────────────

    [Fact]
    public void ValidateTemplateName_AcceptsDocument_ForUnknownTargets()
    {
        // **Validates: Requirements 6.3**
        //
        // For unknown/external target IDs, "document" is the conventional default
        // template name and SHALL NOT produce a warning.
        GenUnknownTargetId.Sample(
            targetId =>
            {
                var validator = new TemplatePackValidator();
                var diagnostics = validator.ValidateTemplateName("document", targetId);

                var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
                Assert.Empty(warnings);
            },
            iter: 100,
            print: id => $"target=\"{id}\"");
    }

    // ── Property 7g: Unknown targets warn for non-document names ─────────────────

    [Fact]
    public void ValidateTemplateName_ProducesWarning_ForUnknownTargetsWithNonDocumentName()
    {
        // **Validates: Requirements 6.3**
        //
        // For unknown/external target IDs, any template name other than "document"
        // SHALL produce a warning.
        Gen.Select(GenUnknownTargetId, GenUnknownTemplateName)
            .Where(t => t.Item2 != "document")
            .Sample(
                (targetId, templateName) =>
                {
                    var validator = new TemplatePackValidator();
                    var diagnostics = validator.ValidateTemplateName(templateName, targetId);

                    var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
                    Assert.NotEmpty(warnings);
                    Assert.All(warnings, d => Assert.Equal("TP006", d.Code));
                },
                iter: 100,
                print: t => $"target=\"{t.Item1}\", template=\"{t.Item2}\"");
    }

    // ── Utility ──────────────────────────────────────────────────────────────────

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
