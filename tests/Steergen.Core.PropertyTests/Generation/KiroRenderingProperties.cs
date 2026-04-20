using Steergen.Core.Model;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Kiro;

namespace Steergen.Core.PropertyTests.Generation;

public sealed class KiroRenderingProperties
{
    private const string DocumentTemplate = """
        ---
        description: {{ description }}
        inclusion: {{ inclusion }}
        {{ if file_match_pattern -}}
        fileMatchPattern: {{ file_match_pattern }}
        {{ end -}}
        ---
        {{ for section in sections -}}
        ## {{ section.heading }}
        {{ for rule in section.rules -}}
        - {{ if rule.id }}{{ rule.id }}{{ end }}{{ if rule.supersedes }} [Supersedes: {{ rule.supersedes }}]{{ end }}{{ if rule.deprecated }} (deprecated){{ end }}
        {{ rule.primary_text }}
        {{ end -}}
        {{ end -}}
        """;

    private static readonly ITemplateProvider FakeTemplates =
        new InlineKiroTemplateProvider(DocumentTemplate);

    private static KiroRuleProseModel MakeRule(string text, string? explanatory = null) =>
        new() { PrimaryText = text, ExplanatoryText = explanatory };

    [Fact]
    public async Task RenderDocument_NeverContainsRuleIdPattern()
    {
        var target = new KiroTargetComponent(FakeTemplates);
        var ruleSets = new[]
        {
            new[] { MakeRule("Use dependency injection.", "This prevents tight coupling.") },
            new[] { MakeRule("All tests must pass."), MakeRule("No dead code in production.") },
            new[] { MakeRule("Document public APIs."), MakeRule("Avoid magic numbers."), MakeRule("Use named constants.") },
        };

        foreach (var rules in ruleSets)
        {
            var model = new KiroDocumentModel
            {
                Description = "Guidelines",
                Inclusion = "always",
                Rules = rules,
            };

            var output = await target.RenderDocumentAsync(model);

            Assert.DoesNotMatch(@"\b[A-Z]+-\d{3,}\b", output);
        }
    }

    [Fact]
    public async Task RenderDocument_NeverContainsRuleBlockSyntax()
    {
        var target = new KiroTargetComponent(FakeTemplates);
        var proseTexts = new[]
        {
            "Use structured logging.",
            "Validate all inputs at the boundary.",
            "Prefer immutable objects.",
        };
        var model = new KiroDocumentModel
        {
            Description = "Clean Code",
            Inclusion = "always",
            Rules = proseTexts.Select(t => new KiroRuleProseModel { PrimaryText = t }).ToList(),
        };

        var output = await target.RenderDocumentAsync(model);

        Assert.DoesNotContain(":::rule", output);
        Assert.DoesNotContain(":::", output);
    }

    [Fact]
    public async Task RenderDocument_NeverLeaksSeverityKeywords()
    {
        var target = new KiroTargetComponent(FakeTemplates);

        // The template should not mention severity; only prose from rules
        var model = new KiroDocumentModel
        {
            Description = "Security Guidelines",
            Inclusion = "always",
            Rules =
            [
                MakeRule("Never log credentials or secrets."),
                MakeRule("Validate JWT tokens on every request."),
            ],
        };

        var output = await target.RenderDocumentAsync(model);

        var lines = output.Split('\n');
        var bodyLines = lines.SkipWhile(l => !l.TrimStart().StartsWith("---", StringComparison.Ordinal))
            .Skip(1)
            .SkipWhile(l => !l.TrimStart().StartsWith("---", StringComparison.Ordinal))
            .Skip(1)
            .ToList();

        var body = string.Join('\n', bodyLines);
        Assert.DoesNotContain("severity:", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"error\"", body);
        Assert.DoesNotContain("\"warning\"", body);
        Assert.DoesNotContain("\"info\"", body);
    }

    [Fact]
    public async Task RenderDocument_AllProseTextsPresent_InOrder()
    {
        var target = new KiroTargetComponent(FakeTemplates);
        var texts = new[]
        {
            "First guidance sentence.",
            "Second guidance sentence.",
            "Third guidance sentence.",
        };
        var model = new KiroDocumentModel
        {
            Description = "Guidelines",
            Inclusion = "always",
            Rules = texts.Select(t => new KiroRuleProseModel { PrimaryText = t }).ToList(),
        };

        var output = await target.RenderDocumentAsync(model);

        var pos1 = output.IndexOf(texts[0], StringComparison.Ordinal);
        var pos2 = output.IndexOf(texts[1], StringComparison.Ordinal);
        var pos3 = output.IndexOf(texts[2], StringComparison.Ordinal);

        Assert.True(pos1 >= 0);
        Assert.True(pos2 >= 0);
        Assert.True(pos3 >= 0);
        Assert.True(pos1 < pos2 && pos2 < pos3, "Rules must appear in order.");
    }

    [Fact]
    public async Task RenderDocument_SameInputTwice_YieldsDeterministicOutput()
    {
        var target = new KiroTargetComponent(FakeTemplates);
        var model = new KiroDocumentModel
        {
            Description = "Determinism test",
            Inclusion = "always",
            Rules =
            [
                MakeRule("Rule A."),
                MakeRule("Rule B.", "Explanatory note for B."),
                MakeRule("Rule C."),
            ],
        };

        var output1 = await target.RenderDocumentAsync(model);
        var output2 = await target.RenderDocumentAsync(model);

        Assert.Equal(output1, output2);
    }

    [Fact]
    public async Task RenderDocument_FileMatchInclusion_PatternAppearsInFrontmatter()
    {
        var target = new KiroTargetComponent(FakeTemplates);
        var patterns = new[] { "**/*.ts", "src/**/*.cs", "*.py" };

        foreach (var pattern in patterns)
        {
            var model = new KiroDocumentModel
            {
                Description = "Test",
                Inclusion = "fileMatch",
                FileMatchPattern = pattern,
                Rules = [MakeRule("Some rule.")],
            };

            var output = await target.RenderDocumentAsync(model);

            var frontmatterEnd = output.IndexOf("---", output.IndexOf("---", StringComparison.Ordinal) + 3, StringComparison.Ordinal);
            var frontmatter = output[..frontmatterEnd];

            Assert.Contains($"fileMatchPattern: {pattern}", frontmatter);
        }
    }

    private sealed class InlineKiroTemplateProvider(string documentTemplate) : ITemplateProvider
    {
        public string GetTemplate(string targetId, string templateName) =>
            templateName switch
            {
                "document" => documentTemplate,
                _ => throw new InvalidOperationException($"Unknown template '{templateName}'."),
            };
    }
}
