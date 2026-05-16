using Steergen.Core.Model;
using Steergen.Core.Parsing;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Speckit;

namespace Steergen.Core.UnitTests.Targets;

public sealed class SpeckitWhitespaceReproTest
{
    // Exact copy of the real embedded Speckit constitution template
    private const string ConstitutionTemplate =
        "# Engineering Constitution\n{{ for section in sections -}}\n## {{ section.heading }}\n{{ for rule in section.rules -}}\n- {{ rule.id }}{{ if rule.mandatory }} [MANDATORY]{{ end }}{{ if rule.deprecated }} (deprecated){{ end }}: {{ rule.primary_text }}\n{{ end -}}\n{{ end -}}\n";

    private static readonly ITemplateProvider Templates = new InlineTemplateProvider(ConstitutionTemplate);

    [Fact]
    public async Task EndToEnd_ParseAndRender_PreservesNewlinesInCodeBlockRule()
    {
        // Exact reproduction of the user's reported issue
        var sourceMarkdown = "---\nid: arch-rules\nversion: \"1.0.0\"\ntitle: Architecture Rules\n---\n\n:::rule id=\"ARCH-002\" category=\"dependency\"\nProject references follow this strict DAG:\n```text\nast-model -> ast_generator -> ast-generated -> parser -> compiler -> tests\n^\nfifthlang.system\n```\nNo `.csproj` under `src/` may contain a `<ProjectReference>` pointing backward in this ordering.\nVerify: inspect project references under `src/` and reject any backward edge relative to this DAG.\n:::\n";

        // Step 1: Parse
        var doc = SteeringMarkdownParser.Parse(sourceMarkdown, "architecture.md");
        Assert.Single(doc.Rules);
        var rule = doc.Rules[0];

        Console.WriteLine("=== PARSED PRIMARY TEXT ===");
        Console.WriteLine(rule.PrimaryText);
        Console.WriteLine("=== END ===");

        // Verify parsing preserved newlines
        Assert.Contains("\n", rule.PrimaryText);
        Assert.Contains("```text\n", rule.PrimaryText);

        // Step 2: Render through Speckit pipeline
        var target = new SpeckitTargetComponent(Templates);
        var model = new ResolvedSteeringModel
        {
            Rules = [rule],
        };
        var outputDir = Path.Combine(Path.GetTempPath(), $"speckit-e2e-{Guid.NewGuid():N}");
        try
        {
            var config = new TargetConfiguration { Id = "speckit", Enabled = true, OutputPath = outputDir };
            await target.GenerateWithPlanAsync(model, config, BuildWritePlan(model), CancellationToken.None);

            var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "dependency.md"));

            Console.WriteLine("=== SPECKIT OUTPUT ===");
            Console.WriteLine(content);
            Console.WriteLine("=== END ===");

            // The output MUST preserve newlines - not collapse to single line
            var ruleStart = content.IndexOf("ARCH-002");
            var afterId = content[ruleStart..];
            var lineCount = afterId.Split('\n').Length;
            Assert.True(lineCount > 2, $"Rule body was collapsed to {lineCount} line(s). Expected multiline output.");

            // Continuation lines must be indented for valid markdown list rendering
            Assert.Contains("\n  ```text\n", content);
            Assert.Contains("\n  fifthlang.system\n", content);
            Assert.Contains("\n  No `.csproj`", content);
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task EndToEnd_ParseAndRender_WithWindowsLineEndings_PreservesNewlines()
    {
        // Same test but with \r\n line endings (Windows)
        var sourceMarkdown = "---\r\nid: arch-rules\r\nversion: \"1.0.0\"\r\ntitle: Architecture Rules\r\n---\r\n\r\n:::rule id=\"ARCH-002\" category=\"dependency\"\r\nProject references follow this strict DAG:\r\n```text\r\nast-model -> ast_generator -> ast-generated -> parser -> compiler -> tests\r\n^\r\nfifthlang.system\r\n```\r\nNo `.csproj` under `src/` may contain a `<ProjectReference>` pointing backward in this ordering.\r\nVerify: inspect project references under `src/` and reject any backward edge relative to this DAG.\r\n:::\r\n";

        // Step 1: Parse
        var doc = SteeringMarkdownParser.Parse(sourceMarkdown, "architecture.md");
        Assert.Single(doc.Rules);
        var rule = doc.Rules[0];

        Console.WriteLine("=== PARSED PRIMARY TEXT (CRLF) ===");
        Console.WriteLine(rule.PrimaryText);
        Console.WriteLine("=== END ===");

        // Step 2: Render through Speckit pipeline
        var target = new SpeckitTargetComponent(Templates);
        var model = new ResolvedSteeringModel
        {
            Rules = [rule],
        };
        var outputDir = Path.Combine(Path.GetTempPath(), $"speckit-crlf-{Guid.NewGuid():N}");
        try
        {
            var config = new TargetConfiguration { Id = "speckit", Enabled = true, OutputPath = outputDir };
            await target.GenerateWithPlanAsync(model, config, BuildWritePlan(model), CancellationToken.None);

            var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "dependency.md"));

            Console.WriteLine("=== SPECKIT OUTPUT (CRLF) ===");
            Console.WriteLine(content);
            Console.WriteLine("=== END ===");

            // The output MUST preserve newlines
            var ruleStart = content.IndexOf("ARCH-002");
            var afterId = content[ruleStart..];
            var lineCount = afterId.Split('\n').Length;
            Assert.True(lineCount > 2, $"Rule body was collapsed to {lineCount} line(s). Expected multiline output.");
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    private sealed class InlineTemplateProvider(string constitutionTemplate) : ITemplateProvider
    {
        public string GetTemplate(string targetId, string templateName) =>
            templateName switch
            {
                "constitution" => constitutionTemplate,
                "module" => constitutionTemplate,
                _ => throw new InvalidOperationException($"Unknown template '{templateName}'."),
            };
    }

    private static WritePlan BuildWritePlan(ResolvedSteeringModel model) => new()
    {
        TargetId = "speckit",
        Files = model.Rules
            .GroupBy(rule => string.Equals(rule.Category, "core", StringComparison.OrdinalIgnoreCase)
                ? "constitution.md"
                : $"{rule.Category}.md",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new WritePlanFile
            {
                Path = group.Key,
                AppendUnits = group
                    .OrderBy(rule => rule.Id, StringComparer.Ordinal)
                    .Select((rule, index) => new ContentUnit
                    {
                        RuleId = rule.Id ?? string.Empty,
                        OrderKey = (0, index, rule.Id ?? string.Empty),
                    })
                    .ToList(),
            })
            .ToList(),
    };
}
