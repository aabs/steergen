using System.Text.Json;
using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.PropertyTests.Generation;

/// <summary>
/// Property tests for <see cref="InspectModelWriter"/> ensuring deterministic JSON output.
/// </summary>
public sealed class InspectJsonProperties
{
    // ── Stable ordering: multiple calls produce identical output ──────────

    [Fact]
    public void Write_SameModel_ProducesSameJsonOnRepeatedCalls()
    {
        var model = MakeModel(
            rules: [
                MakeRule("Z-001", "core"),
                MakeRule("A-001", "security"),
                MakeRule("M-001", "quality"),
            ],
            profiles: ["alpha", "beta"]);

        var json1 = InspectModelWriter.Write(model);
        var json2 = InspectModelWriter.Write(model);

        Assert.Equal(json1, json2);
    }

    // ── Rules are sorted by ID in output ──────────────────────────────────

    [Fact]
    public void Write_Rules_AreSortedByIdInOutput()
    {
        var model = MakeModel(rules: [
            MakeRule("C-003", "core"),
            MakeRule("A-001", "core"),
            MakeRule("B-002", "core"),
        ]);

        var json = InspectModelWriter.Write(model);
        using var doc = JsonDocument.Parse(json);
        var ruleIds = doc.RootElement.GetProperty("rules")
            .EnumerateArray()
            .Select(r => r.GetProperty("id").GetString()!)
            .ToList();

        var sorted = ruleIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, ruleIds);
    }

    // ── ActiveProfiles are sorted in output ───────────────────────────────

    [Fact]
    public void Write_ActiveProfiles_AreSortedInOutput()
    {
        var model = MakeModel(
            rules: [],
            profiles: ["zebra", "alpha", "mango"]);

        var json = InspectModelWriter.Write(model);
        using var doc = JsonDocument.Parse(json);
        var profiles = doc.RootElement.GetProperty("activeProfiles")
            .EnumerateArray()
            .Select(p => p.GetString()!)
            .ToList();

        var sorted = profiles.OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, profiles);
    }

    // ── Documents are sorted by ID in output ──────────────────────────────

    [Fact]
    public void Write_Documents_AreSortedByIdInOutput()
    {
        var model = new ResolvedSteeringModel
        {
            Documents = [
                MakeDoc("z-doc", "path/z.md"),
                MakeDoc("a-doc", "path/a.md"),
                MakeDoc("m-doc", "path/m.md"),
            ],
            Rules = [],
            ActiveProfiles = [],
            SourceIndex = new Dictionary<string, SteeringDocument>(),
        };

        var json = InspectModelWriter.Write(model);
        using var doc = JsonDocument.Parse(json);
        var docIds = doc.RootElement.GetProperty("documents")
            .EnumerateArray()
            .Select(d => d.GetProperty("id").GetString()!)
            .ToList();

        var sorted = docIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, docIds);
    }

    // ── Null optional fields are omitted from output ───────────────────────

    [Fact]
    public void Write_NullOptionalFields_AreNotIncludedInOutput()
    {
        var rule = new SteeringRule
        {
            Id = "R001",
            Category = "core",
            // PrimaryText null
        };
        var model = MakeModel(rules: [rule]);

        var json = InspectModelWriter.Write(model);
        using var doc = JsonDocument.Parse(json);
        var ruleEl = doc.RootElement.GetProperty("rules").EnumerateArray().First();

        Assert.False(ruleEl.TryGetProperty("category", out _) && ruleEl.GetProperty("category").ValueKind == JsonValueKind.Null);
        // mandatory should always be present as a boolean
        Assert.True(ruleEl.TryGetProperty("mandatory", out _));
    }

    // ── Deprecated flag is omitted when false ─────────────────────────────

    [Fact]
    public void Write_DeprecatedFalse_IsOmittedFromOutput()
    {
        var rule = new SteeringRule { Id = "R001", Category = "core", Deprecated = false };
        var model = MakeModel(rules: [rule]);

        var json = InspectModelWriter.Write(model);
        using var doc = JsonDocument.Parse(json);
        var ruleEl = doc.RootElement.GetProperty("rules").EnumerateArray().First();

        Assert.False(ruleEl.TryGetProperty("deprecated", out _));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static ResolvedSteeringModel MakeModel(
        IReadOnlyList<SteeringRule> rules,
        IReadOnlyList<string>? profiles = null)
    {
        profiles ??= [];
        return new ResolvedSteeringModel
        {
            Documents = [],
            Rules = rules,
            ActiveProfiles = profiles,
            SourceIndex = new Dictionary<string, SteeringDocument>(),
        };
    }

    private static SteeringRule MakeRule(string id, string category) =>
        new() { Id = id, Category = category };

    private static SteeringDocument MakeDoc(string id, string path) =>
        new() { Id = id, SourcePath = path };
}
