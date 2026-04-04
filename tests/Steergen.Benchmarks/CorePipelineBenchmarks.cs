using BenchmarkDotNet.Attributes;
using Steergen.Core.Merge;
using Steergen.Core.Model;
using Steergen.Core.Parsing;
using Steergen.Core.Validation;

namespace Steergen.Benchmarks;

[MemoryDiagnoser]
public class CorePipelineBenchmarks
{
    private string _smallDocument = string.Empty;
    private string _largeDocument = string.Empty;
    private SteeringDocument[] _globalDocs = [];
    private SteeringDocument[] _projectDocs = [];
    private SteeringDocument _fiftyRuleDocument = new();
    private readonly SteeringValidator _validator = new();
    private readonly SteeringResolver _resolver = new();

    [GlobalSetup]
    public void Setup()
    {
        _smallDocument = BuildDocument("small", 10);
        _largeDocument = BuildDocument("large", 100);
        _globalDocs = BuildDocumentArray("global", 5, 10);
        _projectDocs = BuildDocumentArray("project", 5, 10);
        _fiftyRuleDocument = SteeringMarkdownParser.Parse(BuildDocument("validate", 50), "validate.md");
    }

    [Benchmark]
    public SteeringDocument ParseSmallDocument() =>
        SteeringMarkdownParser.Parse(_smallDocument, "small.md");

    [Benchmark]
    public SteeringDocument ParseLargeDocument() =>
        SteeringMarkdownParser.Parse(_largeDocument, "large.md");

    [Benchmark]
    public ResolvedSteeringModel ResolveModel() =>
        _resolver.Resolve(_globalDocs, _projectDocs, ["default"]);

    [Benchmark]
    public IReadOnlyList<Steergen.Core.Validation.Diagnostic> ValidateDocument() =>
        _validator.Validate(_fiftyRuleDocument);

    private static string BuildDocument(string prefix, int ruleCount)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"id: {prefix.ToUpperInvariant()}-001");
        sb.AppendLine($"title: {prefix} document");
        sb.AppendLine("---");
        for (int i = 1; i <= ruleCount; i++)
        {
            sb.AppendLine($":::rule id=\"{prefix.ToUpperInvariant()}-R{i:D3}\" severity=\"info\" domain=\"core\"");
            sb.AppendLine($"Rule {i} primary text for {prefix} document.");
            sb.AppendLine(":::");
        }
        return sb.ToString();
    }

    private static SteeringDocument[] BuildDocumentArray(string prefix, int docCount, int rulesPerDoc)
    {
        return Enumerable.Range(1, docCount)
            .Select(i =>
            {
                var content = BuildDocument($"{prefix}-{i:D2}", rulesPerDoc);
                return SteeringMarkdownParser.Parse(content, $"{prefix}-{i:D2}.md");
            })
            .ToArray();
    }
}
