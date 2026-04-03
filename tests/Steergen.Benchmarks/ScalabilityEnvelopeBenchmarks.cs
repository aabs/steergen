using BenchmarkDotNet.Attributes;
using Steergen.Core.Generation;
using Steergen.Core.Merge;
using Steergen.Core.Model;
using Steergen.Core.Parsing;
using Steergen.Core.Validation;

namespace Steergen.Benchmarks;

/// <summary>
/// Benchmarks for the scalability envelope: 100 documents × 10 rules each (1,000 rules total).
/// These measure the cost of parse, validate, and resolve at the upper supported scale.
/// </summary>
[MemoryDiagnoser]
public class ScalabilityEnvelopeBenchmarks
{
    /// <summary>Supported scale envelope: 100 documents with 10 rules each = 1,000 rules.</summary>
    private const int EnvelopeDocCount = 100;

    private const int RulesPerDoc = 10;

    /// <summary>Beyond-envelope scale: 200 documents (triggers warning-level load).</summary>
    private const int BeyondEnvelopeDocCount = 200;

    private string[] _envelopeDocStrings = [];
    private SteeringDocument[] _envelopeDocuments = [];
    private SteeringDocument[] _beyondEnvelopeDocuments = [];
    private readonly SteeringValidator _validator = new();
    private readonly SteeringResolver _resolver = new();

    [GlobalSetup]
    public void Setup()
    {
        _envelopeDocStrings = Enumerable
            .Range(1, EnvelopeDocCount)
            .Select(i => BuildDocument($"ENV-{i:D3}", RulesPerDoc))
            .ToArray();

        _envelopeDocuments = _envelopeDocStrings
            .Select((src, i) => SteeringMarkdownParser.Parse(src, $"env-{i + 1:D3}.md"))
            .ToArray();

        _beyondEnvelopeDocuments = Enumerable
            .Range(1, BeyondEnvelopeDocCount)
            .Select(i =>
            {
                var src = BuildDocument($"OVER-{i:D3}", RulesPerDoc);
                return SteeringMarkdownParser.Parse(src, $"over-{i:D3}.md");
            })
            .ToArray();
    }

    // ── Parse benchmarks ──────────────────────────────────────────────────

    /// <summary>Parse all 100 envelope documents (1,000 rules).</summary>
    [Benchmark]
    public SteeringDocument[] ParseEnvelopeDocuments()
    {
        return _envelopeDocStrings
            .Select((src, i) => SteeringMarkdownParser.Parse(src, $"env-{i + 1:D3}.md"))
            .ToArray();
    }

    // ── Validate benchmarks ───────────────────────────────────────────────

    /// <summary>Validate all 100 envelope documents (1,000 rules).</summary>
    [Benchmark]
    public IReadOnlyList<Diagnostic> ValidateEnvelopeCorpus()
    {
        return _validator.ValidateCorpus(_envelopeDocuments);
    }

    /// <summary>
    /// Validate 200 documents (beyond the 100-document envelope).
    /// Represents the warning-level scale that should still complete but with degraded performance.
    /// </summary>
    [Benchmark]
    public IReadOnlyList<Diagnostic> ValidateBeyondEnvelopeCorpus()
    {
        return _validator.ValidateCorpus(_beyondEnvelopeDocuments);
    }

    // ── Resolve benchmarks ────────────────────────────────────────────────

    /// <summary>Resolve the steering model from 100 envelope documents (1,000 rules).</summary>
    [Benchmark]
    public ResolvedSteeringModel ResolveEnvelopeModel()
    {
        return _resolver.Resolve(_envelopeDocuments, [], ["default"]);
    }

    /// <summary>Resolve the steering model from 200 beyond-envelope documents (2,000 rules).</summary>
    [Benchmark]
    public ResolvedSteeringModel ResolveBeyondEnvelopeModel()
    {
        return _resolver.Resolve(_beyondEnvelopeDocuments, [], ["default"]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string BuildDocument(string prefix, int ruleCount)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"id: {prefix.ToLowerInvariant()}-doc");
        sb.AppendLine($"title: Scalability envelope document {prefix}");
        sb.AppendLine("---");
        for (int i = 1; i <= ruleCount; i++)
        {
            sb.AppendLine($":::rule id=\"{prefix}-R{i:D3}\" severity=\"info\" domain=\"scalability\"");
            sb.AppendLine($"Scalability rule {i} for document {prefix}.");
            sb.AppendLine(":::");
        }
        return sb.ToString();
    }
}
