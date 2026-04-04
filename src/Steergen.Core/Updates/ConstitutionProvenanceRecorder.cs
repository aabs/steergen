using System.Text.Json;
using System.Text.Json.Serialization;

namespace Steergen.Core.Updates;

/// <summary>
/// A single entry in the constitution amendment provenance log.
/// </summary>
public sealed record ConstitutionProvenanceEntry
{
    /// <summary>Version installed before this amendment.</summary>
    public required string PreviousVersion { get; init; }

    /// <summary>Version installed by this amendment.</summary>
    public required string NewVersion { get; init; }

    /// <summary>UTC timestamp when the amendment was recorded.</summary>
    public required DateTimeOffset AmendmentDate { get; init; }

    /// <summary>Optional human-readable rationale for this version change.</summary>
    public string? VersionRationale { get; init; }

    /// <summary>
    /// Artifacts that may need to be re-synchronised after this amendment
    /// (e.g., generated Speckit or Kiro outputs that were produced from the previous version).
    /// </summary>
    public IReadOnlyList<string> ImpactedArtifacts { get; init; } = [];
}

/// <summary>
/// Persists and retrieves the ordered list of constitution amendment provenance entries.
/// </summary>
public sealed class ConstitutionProvenanceRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Default file name written alongside <c>steergen.config.yaml</c>.
    /// </summary>
    public const string DefaultFileName = "steergen.provenance.json";

    private readonly string _filePath;

    /// <summary>
    /// Creates a recorder that writes to <see cref="DefaultFileName"/> inside <paramref name="configDirectory"/>.
    /// </summary>
    public ConstitutionProvenanceRecorder(string configDirectory)
    {
        _filePath = Path.Combine(configDirectory, DefaultFileName);
    }

    /// <summary>
    /// Creates a recorder that writes to <paramref name="fileName"/> inside <paramref name="configDirectory"/>.
    /// </summary>
    public ConstitutionProvenanceRecorder(string configDirectory, string fileName)
    {
        _filePath = Path.Combine(configDirectory, fileName);
    }

    /// <summary>
    /// Appends a provenance entry and writes the log atomically.
    /// </summary>
    public async Task RecordAsync(
        ConstitutionProvenanceEntry entry,
        CancellationToken cancellationToken = default)
    {
        var existing = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var updated  = new List<ConstitutionProvenanceEntry>(existing) { entry };

        var json = JsonSerializer.Serialize(updated, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns all recorded provenance entries, or an empty list when the file does not exist.
    /// </summary>
    public async Task<IReadOnlyList<ConstitutionProvenanceEntry>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return [];

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Deserialize<List<ConstitutionProvenanceEntry>>(json, JsonOptions)
               ?? [];
    }

    /// <summary>Absolute path of the provenance log file managed by this recorder.</summary>
    public string FilePath => _filePath;
}
