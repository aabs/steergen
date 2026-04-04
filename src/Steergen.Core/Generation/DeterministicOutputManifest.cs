using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Steergen.Core.Generation;

/// <summary>
/// Records SHA-256 content hashes for all files produced during a generation run.
/// Written as <c>generation-manifest.json</c> to the output directory so that CI
/// pipelines can (a) verify deterministic output and (b) surface structured failure
/// details without parsing stderr.
/// </summary>
public sealed record DeterministicOutputManifest(
    string GenerationId,
    DateTimeOffset GeneratedAt,
    bool Success,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ManifestEntry> Entries)
{
    public const string ManifestFileName = "generation-manifest.json";

    /// <summary>
    /// Creates a manifest by hashing all files in <paramref name="outputDirectory"/>,
    /// excluding any previously-written manifest file.
    /// </summary>
    public static async Task<DeterministicOutputManifest> FromDirectoryAsync(
        string outputDirectory,
        bool success,
        IReadOnlyList<string>? errors = null,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<ManifestEntry>();

        if (Directory.Exists(outputDirectory))
        {
            foreach (var path in Directory
                .EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
                .Where(p => !p.EndsWith(ManifestFileName, StringComparison.Ordinal))
                .OrderBy(p => p, StringComparer.Ordinal))
            {
                var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                var relativePath = Path.GetRelativePath(outputDirectory, path)
                    .Replace('\\', '/');
                var sizeBytes = bytes.Length;
                entries.Add(new ManifestEntry(relativePath, hash, sizeBytes));
            }
        }

        return new DeterministicOutputManifest(
            GenerationId: Guid.NewGuid().ToString("N"),
            GeneratedAt: DateTimeOffset.UtcNow,
            Success: success,
            Errors: errors ?? [],
            Entries: entries);
    }

    /// <summary>
    /// Creates a failure manifest with no file entries.
    /// </summary>
    public static DeterministicOutputManifest Failure(IReadOnlyList<string> errors) =>
        new(
            GenerationId: Guid.NewGuid().ToString("N"),
            GeneratedAt: DateTimeOffset.UtcNow,
            Success: false,
            Errors: errors,
            Entries: []);

    /// <summary>
    /// Writes <c>generation-manifest.json</c> to <paramref name="outputDirectory"/>.
    /// </summary>
    public async Task WriteAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var manifestPath = Path.Combine(outputDirectory, ManifestFileName);
        var json = JsonSerializer.Serialize(this, DeterministicOutputManifestJsonContext.Default.DeterministicOutputManifest);
        await File.WriteAllTextAsync(manifestPath, json, cancellationToken);
    }

    /// <summary>
    /// Returns true when two manifests contain the same set of files with the same hashes,
    /// independent of generation ID or timestamp.
    /// </summary>
    public bool HasIdenticalContentTo(DeterministicOutputManifest other)
    {
        if (Entries.Count != other.Entries.Count)
            return false;

        var sorted = Entries.OrderBy(e => e.RelativePath, StringComparer.Ordinal).ToList();
        var otherSorted = other.Entries.OrderBy(e => e.RelativePath, StringComparer.Ordinal).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].RelativePath != otherSorted[i].RelativePath)
                return false;
            if (sorted[i].Sha256 != otherSorted[i].Sha256)
                return false;
        }

        return true;
    }
}

/// <summary>
/// A single file entry in a <see cref="DeterministicOutputManifest"/>.
/// </summary>
public sealed record ManifestEntry(string RelativePath, string Sha256, long SizeBytes);

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DeterministicOutputManifest))]
internal sealed partial class DeterministicOutputManifestJsonContext : JsonSerializerContext;
