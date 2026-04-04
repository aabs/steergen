using System.Text.Json;
using System.Text.Json.Serialization;
using Steergen.Core.Model;

namespace Steergen.Core.Generation;

/// <summary>
/// Serializes a <see cref="ResolvedSteeringModel"/> to stable, deterministic JSON for inspection.
/// </summary>
public static class InspectModelWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Write(ResolvedSteeringModel model)
    {
        var dto = new InspectModelDto(
            ActiveProfiles: model.ActiveProfiles.OrderBy(p => p, StringComparer.Ordinal).ToList(),
            Documents: model.Documents
                .OrderBy(d => d.Id, StringComparer.Ordinal)
                .Select(d => new InspectDocumentDto(
                    Id: d.Id,
                    Title: d.Title,
                    Version: d.Version,
                    SourcePath: d.SourcePath,
                    Tags: d.Tags.OrderBy(t => t, StringComparer.Ordinal).ToList(),
                    Profiles: d.Profiles.OrderBy(p => p, StringComparer.Ordinal).ToList()))
                .ToList(),
            Rules: model.Rules
                .OrderBy(r => r.Id, StringComparer.Ordinal)
                .Select(r => new InspectRuleDto(
                    Id: r.Id,
                    Severity: r.Severity,
                    Domain: r.Domain,
                    Category: r.Category,
                    Profile: r.Profile,
                    Deprecated: r.Deprecated ? true : null,
                    Supersedes: r.Supersedes,
                    AppliesTo: r.AppliesTo.OrderBy(a => a, StringComparer.Ordinal).ToList(),
                    Tags: r.Tags.OrderBy(t => t, StringComparer.Ordinal).ToList(),
                    PrimaryText: r.PrimaryText))
                .ToList()
        );

        return JsonSerializer.Serialize(dto, SerializerOptions);
    }

    // ── Private DTOs ───────────────────────────────────────────────────────

    private sealed record InspectModelDto(
        IReadOnlyList<string> ActiveProfiles,
        IReadOnlyList<InspectDocumentDto> Documents,
        IReadOnlyList<InspectRuleDto> Rules);

    private sealed record InspectDocumentDto(
        string? Id,
        string? Title,
        string? Version,
        string? SourcePath,
        IReadOnlyList<string> Tags,
        IReadOnlyList<string> Profiles);

    private sealed record InspectRuleDto(
        string? Id,
        string Severity,
        string Domain,
        string? Category,
        string? Profile,
        bool? Deprecated,
        string? Supersedes,
        IReadOnlyList<string> AppliesTo,
        IReadOnlyList<string> Tags,
        string? PrimaryText);
}
