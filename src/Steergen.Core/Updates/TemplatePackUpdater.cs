using Steergen.Core.Configuration;
using Steergen.Core.Model;

namespace Steergen.Core.Updates;

/// <summary>
/// Result of a template-pack update operation.
/// </summary>
public sealed record UpdateResult(bool Success, string? NewVersion, string? ErrorMessage)
{
    public static UpdateResult Ok(string newVersion)  => new(true, newVersion, null);
    public static UpdateResult Fail(string message)   => new(false, null, message);
}

/// <summary>
/// Orchestrates template-pack version resolution and config persistence.
/// </summary>
public sealed class TemplatePackUpdater
{
    /// <summary>
    /// The built-in version catalog shipped with this binary.
    /// In a real distribution this would be discovered from the template assembly;
    /// for this implementation it is a static set of known versions.
    /// </summary>
    public static readonly IReadOnlyList<string> BuiltInCatalog =
    [
        "1.0.0",
        "1.1.0",
        "1.1.0-preview1",
        "1.2.0",
        "1.2.0-preview1",
        "1.2.0-preview2",
    ];

    private readonly IReadOnlyList<string>        _catalog;
    private readonly SteergenConfigLoader         _loader;
    private readonly SteergenConfigWriter         _writer;
    private readonly ConstitutionProvenanceRecorder? _provenance;

    public TemplatePackUpdater(
        IReadOnlyList<string>?        catalog    = null,
        SteergenConfigLoader?         loader     = null,
        SteergenConfigWriter?         writer     = null,
        ConstitutionProvenanceRecorder? provenance = null)
    {
        _catalog    = catalog    ?? BuiltInCatalog;
        _loader     = loader     ?? new SteergenConfigLoader();
        _writer     = writer     ?? new SteergenConfigWriter();
        _provenance = provenance;
    }

    /// <summary>
    /// Resolves the desired version and writes the updated config.
    /// </summary>
    /// <param name="configPath">Absolute path to <c>steergen.config.yaml</c>.</param>
    /// <param name="version">Exact version requested, or <see langword="null"/> for latest.</param>
    /// <param name="preview">When <see langword="true"/> and <paramref name="version"/> is null,
    ///   considers preview versions when resolving latest.</param>
    /// <param name="versionRationale">Optional human-readable reason for this amendment, stored in the provenance log.</param>
    /// <param name="impactedArtifacts">Artifacts that may need re-synchronisation after this amendment.</param>
    public async Task<UpdateResult> UpdateAsync(
        string configPath,
        string? version,
        bool preview,
        string? versionRationale = null,
        IReadOnlyList<string>? impactedArtifacts = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configPath))
            return UpdateResult.Fail($"Configuration file not found: {configPath}");

        string? resolved;

        if (version is not null)
        {
            if (!TemplateVersionResolver.IsValidVersion(version))
                return UpdateResult.Fail($"Invalid version format '{version}'. Expected x.y.z or x.y.z-previewN.");

            resolved = TemplateVersionResolver.ResolveExact(_catalog, version);
            if (resolved is null)
                return UpdateResult.Fail($"Version '{version}' is not available in the template catalog.");
        }
        else
        {
            resolved = preview
                ? TemplateVersionResolver.ResolveLatestIncludingPreview(_catalog)
                : TemplateVersionResolver.ResolveLatestStable(_catalog);

            if (resolved is null)
                return UpdateResult.Fail("No suitable version found in the template catalog.");
        }

        var config = await _loader.LoadAsync(configPath, cancellationToken).ConfigureAwait(false);
        var previousVersion = config.TemplatePackVersion ?? "(none)";
        var updated = config with { TemplatePackVersion = resolved };
        await _writer.WriteAsync(configPath, updated, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (_provenance is not null)
        {
            var entry = new ConstitutionProvenanceEntry
            {
                PreviousVersion   = previousVersion,
                NewVersion        = resolved,
                AmendmentDate     = DateTimeOffset.UtcNow,
                VersionRationale  = versionRationale,
                ImpactedArtifacts = impactedArtifacts ?? [],
            };
            await _provenance.RecordAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        return UpdateResult.Ok(resolved);
    }
}
