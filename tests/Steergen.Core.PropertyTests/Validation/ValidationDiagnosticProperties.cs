using Steergen.Core.Model;
using Steergen.Core.Validation;

namespace Steergen.Core.PropertyTests.Validation;

/// <summary>
/// Property tests for stable diagnostic ordering and location reporting in <see cref="SteeringValidator"/>.
/// </summary>
public sealed class ValidationDiagnosticProperties
{
    // ── Stable ordering ────────────────────────────────────────────────────

    [Fact]
    public void ValidateCorpus_DiagnosticOrder_IsStableAcrossRepeatedCalls()
    {
        var docs = new[]
        {
            MakeDoc("z-path.md", "doc-z", [MakeRule("Z-001", "error", "core"), MakeRule("A-001", "info", "core")]),
            MakeDoc("a-path.md", "doc-a", [MakeRule("M-001", "warning", "core")]),
        };

        var validator = new SteeringValidator();
        var run1 = validator.ValidateCorpus(docs);
        var run2 = validator.ValidateCorpus(docs);

        Assert.Equal(run1.Count, run2.Count);
        for (int i = 0; i < run1.Count; i++)
        {
            Assert.Equal(run1[i].Code, run2[i].Code);
            Assert.Equal(run1[i].Message, run2[i].Message);
        }
    }

    [Fact]
    public void ValidateCorpus_DiagnosticsOrderedByPathThenCode()
    {
        // Two documents, second has an error that should appear before first in sorted order
        var docs = new[]
        {
            MakeDocNoId("z-doc.md"),   // V001 missing id, source path = z-doc.md
            MakeDocNoId("a-doc.md"),   // V001 missing id, source path = a-doc.md
        };

        var validator = new SteeringValidator();
        var diagnostics = validator.ValidateCorpus(docs);

        var withLocations = diagnostics.Where(d => d.Location is not null).ToList();
        Assert.True(withLocations.Count >= 2);

        for (int i = 1; i < withLocations.Count; i++)
        {
            var prev = withLocations[i - 1].Location!.FilePath;
            var curr = withLocations[i].Location!.FilePath;
            var comparison = StringComparer.Ordinal.Compare(prev, curr);
            Assert.True(comparison <= 0,
                $"Diagnostics not sorted by path: '{prev}' came before '{curr}'");
        }
    }

    // ── Location reporting ─────────────────────────────────────────────────

    [Fact]
    public void ValidateCorpus_RuleDiagnostics_CarrySourceFilePath()
    {
        const string path = "/some/project/steering.md";
        var doc = MakeDoc(path, "doc-1", [MakeRule(null, "invalid-sev", "core")]);

        var validator = new SteeringValidator();
        var diagnostics = validator.ValidateCorpus([doc]);

        Assert.All(diagnostics.Where(d => d.Code != "V001"),
            d => Assert.Equal(path, d.Location?.FilePath));
    }

    [Fact]
    public void Validate_SingleDocument_DiagnosticsHaveLocationWhenSourcePathSet()
    {
        const string path = "/steering/rules.md";
        var rule = MakeRule("R-001", "bogus-sev", "core");
        var doc = MakeDoc(path, "doc-valid-id", [rule]);

        var validator = new SteeringValidator();
        var diagnostics = validator.Validate(doc);

        // V003 (invalid severity) should carry location
        var v003 = diagnostics.SingleOrDefault(d => d.Code == "V003");
        Assert.NotNull(v003);
        Assert.Equal(path, v003!.Location?.FilePath);
    }

    // ── Duplicate ID cross-document detection ──────────────────────────────

    [Fact]
    public void ValidateCorpus_DuplicateRuleIds_AreReportedOnce_PerDuplicate()
    {
        var docs = new[]
        {
            MakeDoc("first.md", "doc-first", [MakeRule("DUP", "info", "core")]),
            MakeDoc("second.md", "doc-second", [MakeRule("DUP", "info", "core")]),
        };

        var validator = new SteeringValidator();
        var diagnostics = validator.ValidateCorpus(docs);

        var dupes = diagnostics.Where(d => d.Code == "V007").ToList();
        Assert.Single(dupes);
    }

    [Fact]
    public void ValidateCorpus_NoDuplicateIds_ProducesNoV007Diagnostics()
    {
        var docs = new[]
        {
            MakeDoc("a.md", "doc-a", [MakeRule("R-001", "info", "core"), MakeRule("R-002", "warning", "core")]),
            MakeDoc("b.md", "doc-b", [MakeRule("R-003", "error", "core")]),
        };

        var validator = new SteeringValidator();
        var diagnostics = validator.ValidateCorpus(docs);

        Assert.DoesNotContain(diagnostics, d => d.Code == "V007");
    }

    // ── Supersedes warning ─────────────────────────────────────────────────

    [Fact]
    public void ValidateCorpus_SupersedesKnownRule_NoV008Warning()
    {
        var docs = new[]
        {
            MakeDoc("a.md", "doc-a", [MakeRule("OLD-001", "info", "core")]),
            MakeDoc("b.md", "doc-b", [MakeRuleWithSupersedes("NEW-001", "info", "core", "OLD-001")]),
        };

        var validator = new SteeringValidator();
        var diagnostics = validator.ValidateCorpus(docs);

        Assert.DoesNotContain(diagnostics, d => d.Code == "V008");
    }

    [Fact]
    public void ValidateCorpus_SupersedesUnknownRule_ProducesV008Warning()
    {
        var doc = MakeDoc("a.md", "doc-a",
            [MakeRuleWithSupersedes("NEW-001", "info", "core", "GHOST-999")]);

        var validator = new SteeringValidator();
        var diagnostics = validator.ValidateCorpus([doc]);

        var warning = diagnostics.SingleOrDefault(d => d.Code == "V008");
        Assert.NotNull(warning);
        Assert.Equal(DiagnosticSeverity.Warning, warning!.Severity);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static SteeringDocument MakeDoc(string path, string id, IReadOnlyList<SteeringRule> rules) =>
        new() { Id = id, SourcePath = path, Rules = rules };

    private static SteeringDocument MakeDocNoId(string path) =>
        new() { Id = null, SourcePath = path, Rules = [] };

    private static SteeringRule MakeRule(string? id, string severity, string domain) =>
        new() { Id = id, Severity = severity, Domain = domain, PrimaryText = "Some text." };

    private static SteeringRule MakeRuleWithSupersedes(string id, string severity, string domain, string supersedes) =>
        new() { Id = id, Severity = severity, Domain = domain, PrimaryText = "Some text.", Supersedes = supersedes };
}
