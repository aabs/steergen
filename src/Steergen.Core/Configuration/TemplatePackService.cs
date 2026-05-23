using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Targets;

namespace Steergen.Core.Configuration;

/// <summary>
/// Manages template pack configuration in the steergen config file.
/// Uses optimistic locking to detect concurrent modifications.
/// </summary>
public sealed class TemplatePackService
{
    private readonly SteergenConfigLoader _loader = new();
    private readonly SteergenConfigWriter _writer = new();
    private readonly PackManifestParser _manifestParser = new();
    private readonly PackSelectorResolver _selectorResolver = new();

    /// <summary>
    /// Removes the template pack configuration from the config file.
    /// Sets <see cref="SteeringConfiguration.TemplatePack"/> to null.
    /// Emits TP010 error if the pack provides targets that are still registered.
    /// </summary>
    public async Task<TemplatePackResult> RemoveAsync(
        string configPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configPath))
            return TemplatePackResult.Fail($"Config file not found: {configPath}");

        var (config, hash) = await ReadWithHash(configPath, cancellationToken);

        if (config.TemplatePack is null)
            return TemplatePackResult.NotConfigured();

        // Check if removing the pack would orphan registered targets (TP010)
        var packName = ResolvePackName(config.TemplatePack);
        if (packName is not null)
        {
            var diagnostics = TargetRegistry.ValidatePackRemoval(packName, config.RegisteredTargets.ToList());
            if (diagnostics.Count > 0)
            {
                // Return the first TP010 error message
                return TemplatePackResult.Fail(diagnostics[0].Message);
            }
        }

        var updated = config with { TemplatePack = null };

        await _writer.WriteAsync(configPath, updated, hash, cancellationToken);
        return TemplatePackResult.Removed();
    }

    /// <summary>
    /// Resolves the pack name from the configured template pack by parsing its manifest.
    /// Checks local path first, then falls back to the cached GitHub pack path.
    /// Returns null if the manifest cannot be found or parsed.
    /// </summary>
    private string? ResolvePackName(TemplatePackConfig templatePack)
    {
        // Try local path first
        if (!string.IsNullOrWhiteSpace(templatePack.LocalPath))
        {
            var manifest = _manifestParser.Parse(templatePack.LocalPath);
            return manifest?.Name;
        }

        // Try GitHub source via cache
        if (!string.IsNullOrWhiteSpace(templatePack.Source))
        {
            var source = GitHubPackSourceParser.Parse(templatePack.Source, templatePack.Ref);
            if (source is not null)
            {
                var cacheBase = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".steergen");
                var downloader = new PackDownloader(new HttpClient(), cacheBase);
                var cachedPath = downloader.GetCachedPath(source, PackType.Template);

                if (Directory.Exists(cachedPath))
                {
                    var manifest = _manifestParser.Parse(cachedPath);
                    return manifest?.Name;
                }
            }
        }

        return null;
    }

    private async Task<(SteeringConfiguration Config, string Hash)> ReadWithHash(
        string configPath,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(configPath, cancellationToken);
        var hash = SteergenConfigWriter.ComputeFileHash(bytes);
        var config = await _loader.LoadAsync(configPath, cancellationToken);
        return (config, hash);
    }

    public async Task<TemplatePackUpgradeMutationResult> UpdatePinBySelectorAsync(
        string configPath,
        CanonicalPackSelector selector,
        string tag,
        string commitSha,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configPath))
            return TemplatePackUpgradeMutationResult.Fail($"Config file not found: {configPath}");

        var (config, hash) = await ReadWithHash(configPath, cancellationToken);
        if (!_selectorResolver.TryResolveTemplate(config, selector, out var error))
            return TemplatePackUpgradeMutationResult.Fail(error);

        var existing = config.TemplatePack!;
        var updatedTemplate = existing with
        {
            Ref = tag,
            Pin = new PackPin
            {
                Tag = tag,
                CommitSha = commitSha,
            },
        };

        var updated = config with
        {
            TemplatePack = updatedTemplate,
        };

        await _writer.WriteAsync(configPath, updated, hash, cancellationToken);
        return TemplatePackUpgradeMutationResult.Updated(existing.Source ?? string.Empty);
    }
}

/// <summary>
/// Result of a template pack management operation.
/// </summary>
public sealed record TemplatePackResult
{
    public bool Success { get; init; }
    public bool WasNotConfigured { get; init; }
    public string? ErrorMessage { get; init; }

    public static TemplatePackResult Removed() =>
        new() { Success = true };

    public static TemplatePackResult NotConfigured() =>
        new() { Success = true, WasNotConfigured = true };

    public static TemplatePackResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}

public sealed record TemplatePackUpgradeMutationResult
{
    public bool Success { get; init; }
    public string? Source { get; init; }
    public string? ErrorMessage { get; init; }

    public static TemplatePackUpgradeMutationResult Updated(string source) =>
        new() { Success = true, Source = source };

    public static TemplatePackUpgradeMutationResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}
