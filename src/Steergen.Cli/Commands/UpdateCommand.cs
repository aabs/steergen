using System.CommandLine;
using Steergen.Core.Configuration;
using Steergen.Core.Packs;
using Steergen.Core.Updates;

namespace Steergen.Cli.Commands;

/// <summary>
/// Updates the template-pack version recorded in the project configuration,
/// and/or re-downloads configured template packs from GitHub sources.
/// Exits with code 0 (success) or 2 (invalid version / config error).
/// </summary>
public static class UpdateCommand
{
    public static Command Create()
    {
        var configOption = new Option<string?>("--config")
        {
            Description = "Path to steergen.config.yaml (default: steergen.config.yaml in the current directory)",
        };

        var versionOption = new Option<string?>("--version")
        {
            Description = "Exact version to pin (e.g. 1.2.0 or 1.2.0-preview1)",
        };

        var previewOption = new Option<bool>("--preview")
        {
            Description = "Include preview versions when resolving latest",
        };

        var templatesOption = new Option<bool>("--templates")
        {
            Description = "Re-download the configured template pack from its GitHub source",
        };

        var rulesOption = new Option<bool>("--rules")
        {
            Description = "Re-download all configured rules packs from their GitHub sources",
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Force re-download even when a pack is pinned to an immutable commit SHA",
        };

        var cmd = new Command("update", "Update template-pack version in the project configuration")
        {
            configOption,
            versionOption,
            previewOption,
            templatesOption,
            rulesOption,
            forceOption,
        };

        cmd.SetAction(async (parseResult, ct) =>
        {
            var configPath = ConfigPathResolver.ResolveRequired(parseResult.GetValue(configOption));
            var version = parseResult.GetValue(versionOption);
            var preview = parseResult.GetValue(previewOption);
            var templates = parseResult.GetValue(templatesOption);
            var rules = parseResult.GetValue(rulesOption);
            var force = parseResult.GetValue(forceOption);

            if (templates)
            {
                return await RunTemplatesUpdateAsync(configPath, force, ct).ConfigureAwait(false);
            }

            if (rules)
            {
                return await RunRulesUpdateAsync(configPath, force, ct).ConfigureAwait(false);
            }

            return await RunAsync(configPath, version, preview, ct).ConfigureAwait(false);
        });

        return cmd;
    }

    public static async Task<int> RunAsync(
        string configPath,
        string? version,
        bool preview,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var updater = new TemplatePackUpdater();
            var result = await updater.UpdateAsync(configPath, version, preview, cancellationToken: cancellationToken)
                                       .ConfigureAwait(false);

            if (!result.Success)
            {
                Console.Error.WriteLine($"[error] {result.ErrorMessage}");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            Console.Error.WriteLine($"  updated  templatePackVersion → {result.NewVersion}");
            return Composition.ExitCodeMapper.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] {ex.Message}");
            return Composition.ExitCodeMapper.ConfigurationError;
        }
    }

    /// <summary>
    /// Re-downloads the configured template pack from its GitHub source.
    /// Displays pack name, version, and number of template files on success.
    /// Reports "no template pack configured" and exits 0 if none configured.
    /// Respects <paramref name="force"/> to override immutable pin skip.
    /// </summary>
    public static async Task<int> RunTemplatesUpdateAsync(
        string configPath,
        bool force,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"[error] Config file not found: {configPath}");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            var loader = new SteergenConfigLoader();
            var config = await loader.LoadAsync(configPath, cancellationToken).ConfigureAwait(false);

            // Check if a template pack is configured with a GitHub source
            if (config.TemplatePack is null || string.IsNullOrWhiteSpace(config.TemplatePack.Source))
            {
                Console.Error.WriteLine("[info] No template pack source is configured.");
                return Composition.ExitCodeMapper.Success;
            }

            var parsed = GitHubPackSourceParser.Parse(config.TemplatePack.Source, config.TemplatePack.Ref);
            if (parsed is null)
            {
                Console.Error.WriteLine($"[error] Invalid template pack source format: '{config.TemplatePack.Source}'");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            // Download (force bypasses immutable pin skip) via centralized factory
            var downloader = Composition.PackDownloaderFactory.Create();
            var downloadResult = await downloader.DownloadAsync(parsed, PackType.Template, force, cancellationToken)
                                                  .ConfigureAwait(false);

            if (!downloadResult.Success)
            {
                foreach (var diag in downloadResult.Diagnostics)
                {
                    Console.Error.WriteLine($"[{diag.Severity.ToString().ToLowerInvariant()}] {diag.Code}: {diag.Message}");
                }
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            // Parse manifest to get pack name and version
            var manifestParser = new PackManifestParser();
            var manifest = manifestParser.Parse(downloadResult.CachePath!);

            var packName = manifest?.Name ?? "unknown";
            var packVersion = manifest?.Version ?? "unknown";

            // Count template files (.scriban) in the cached pack directory
            var templateFileCount = Directory.Exists(downloadResult.CachePath)
                ? Directory.EnumerateFiles(downloadResult.CachePath, "*.scriban", SearchOption.AllDirectories).Count()
                : 0;

            Console.Error.WriteLine($"  updated  {packName} v{packVersion} ({templateFileCount} template files)");

            // Emit diagnostic warning for branch refs (recommend pinning to SHA/tag)
            Composition.PackDownloaderFactory.EmitBranchRefWarning(config.TemplatePack.Ref, PackType.Template);

            return Composition.ExitCodeMapper.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] {ex.Message}");
            return Composition.ExitCodeMapper.ConfigurationError;
        }
    }

    /// <summary>
    /// Re-downloads all configured rules packs from their GitHub sources.
    /// Respects <paramref name="force"/> to override immutable pin skip.
    /// </summary>
    public static async Task<int> RunRulesUpdateAsync(
        string configPath,
        bool force,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"[error] Config file not found: {configPath}");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            var loader = new SteergenConfigLoader();
            var config = await loader.LoadAsync(configPath, cancellationToken).ConfigureAwait(false);

            if (config.RulesPacks.Count == 0)
            {
                Console.Error.WriteLine("[info] No rules packs are configured.");
                return Composition.ExitCodeMapper.Success;
            }

            var downloader = Composition.PackDownloaderFactory.Create();
            var manifestParser = new PackManifestParser();
            var hasErrors = false;

            foreach (var entry in config.RulesPacks)
            {
                var parsed = GitHubPackSourceParser.Parse(entry.Source, entry.Ref, entry.Path);
                if (parsed is null)
                {
                    Console.Error.WriteLine($"[error] Invalid rules pack source format: '{entry.Source}'");
                    hasErrors = true;
                    continue;
                }

                var downloadResult = await downloader.DownloadAsync(parsed, PackType.Rules, force, cancellationToken)
                                                      .ConfigureAwait(false);

                if (!downloadResult.Success)
                {
                    foreach (var diag in downloadResult.Diagnostics)
                    {
                        Console.Error.WriteLine($"[{diag.Severity.ToString().ToLowerInvariant()}] {diag.Code}: {diag.Message}");
                    }
                    hasErrors = true;
                    continue;
                }

                // Parse manifest to report pack name and version
                var manifest = manifestParser.Parse(downloadResult.CachePath!);
                var packName = manifest?.Name ?? entry.Source;
                var packVersion = manifest?.Version ?? "unknown";

                // Count rules files (.md) in the cached pack directory
                var rulesFileCount = Directory.Exists(downloadResult.CachePath)
                    ? Directory.EnumerateFiles(downloadResult.CachePath, "*.md", SearchOption.AllDirectories).Count()
                    : 0;

                Console.Error.WriteLine($"  updated  {packName} v{packVersion} ({rulesFileCount} rules files)");

                // Emit diagnostic warning for branch refs (recommend pinning to SHA/tag)
                Composition.PackDownloaderFactory.EmitBranchRefWarning(entry.Ref, PackType.Rules);
            }

            return hasErrors
                ? Composition.ExitCodeMapper.ConfigurationError
                : Composition.ExitCodeMapper.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] {ex.Message}");
            return Composition.ExitCodeMapper.ConfigurationError;
        }
    }

}
