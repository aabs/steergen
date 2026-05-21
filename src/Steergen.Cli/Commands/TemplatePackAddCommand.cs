using System.CommandLine;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;

namespace Steergen.Cli.Commands;

/// <summary>
/// Implements <c>steergen template-pack add</c> command.
/// Adds a template pack source to <c>steergen.config.yaml</c> and triggers download.
/// Accepts <c>github:{owner}/{repo}</c> source argument, <c>--ref</c> for tag/branch/SHA,
/// and <c>--path</c> for local override.
/// Exits with code 0 (success), 2 (config/IO error), or 5 (optimistic-lock conflict).
/// </summary>
public static class TemplatePackAddCommand
{
    public static Command Create()
    {
        var sourceArg = new Argument<string?>("source")
        {
            Description = "Template pack source in the format github:{owner}/{repo}, or omit when using --path",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var refOption = new Option<string?>("--ref")
        {
            Description = "Git tag, branch, or 40-character commit SHA to pin the template pack version",
        };

        var pathOption = new Option<string?>("--path")
        {
            Description = "Local filesystem path to a template pack directory (alternative to GitHub source)",
        };

        var configOption = new Option<string?>("--config")
        {
            Description = "Path to the steergen config file (default: steergen.config.yaml)",
        };

        var cmd = new Command("add", "Add a template pack source to the steergen config and download it")
        {
            sourceArg,
            refOption,
            pathOption,
            configOption,
        };

        cmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var source = parseResult.GetValue(sourceArg);
            var refValue = parseResult.GetValue(refOption);
            var localPath = parseResult.GetValue(pathOption);
            var configPath = ConfigPathResolver.ResolveRequired(parseResult.GetValue(configOption));

            return await RunAsync(configPath, source, refValue, localPath, cancellationToken);
        });

        return cmd;
    }

    public static async Task<int> RunAsync(
        string configPath,
        string? source,
        string? refValue,
        string? localPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate that either source or localPath is provided, but not both
            if (source is null && localPath is null)
            {
                Console.Error.WriteLine("[error] Either a github:{owner}/{repo} source argument or --path option is required.");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            if (source is not null && localPath is not null)
            {
                Console.Error.WriteLine("[error] Cannot specify both a GitHub source and --path. Use one or the other.");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            // Load existing config
            var loader = new SteergenConfigLoader();
            var writer = new SteergenConfigWriter();

            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"[error] Config file not found: {configPath}");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            var bytes = await File.ReadAllBytesAsync(configPath, cancellationToken);
            var hash = SteergenConfigWriter.ComputeFileHash(bytes);
            var config = await loader.LoadAsync(configPath, cancellationToken);

            TemplatePackConfig templatePackConfig;

            if (localPath is not null)
            {
                // Local path mode
                templatePackConfig = new TemplatePackConfig
                {
                    LocalPath = localPath,
                };
            }
            else
            {
                // GitHub source mode
                var parsed = GitHubPackSourceParser.Parse(source!, refValue);
                if (parsed is null)
                {
                    Console.Error.WriteLine($"[error] Invalid source format: '{source}'. Expected format: github:{{owner}}/{{repo}}");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }

                templatePackConfig = new TemplatePackConfig
                {
                    Source = source,
                    Ref = refValue,
                };

                // Trigger download via centralized factory
                var downloader = Composition.PackDownloaderFactory.Create();
                var downloadResult = await downloader.DownloadAsync(parsed, PackType.Template, force: false, cancellationToken);

                if (!downloadResult.Success)
                {
                    foreach (var diag in downloadResult.Diagnostics)
                    {
                        Console.Error.WriteLine($"[{diag.Severity.ToString().ToLowerInvariant()}] {diag.Code}: {diag.Message}");
                    }
                    return Composition.ExitCodeMapper.ConfigurationError;
                }

                // Emit diagnostic warning for branch refs (recommend pinning to SHA/tag)
                Composition.PackDownloaderFactory.EmitBranchRefWarning(refValue, PackType.Template);
            }

            // Update config with the new template pack
            var updated = config with
            {
                TemplatePack = templatePackConfig,
            };

            await writer.WriteAsync(configPath, updated, hash, cancellationToken);

            if (localPath is not null)
            {
                Console.Error.WriteLine($"[info] Template pack configured with local path: {localPath}");
            }
            else
            {
                Console.Error.WriteLine($"[info] Template pack '{source}' added and downloaded successfully.");
            }

            return Composition.ExitCodeMapper.Success;
        }
        catch (ConfigWriteConflictException ex)
        {
            Console.Error.WriteLine($"[conflict] {ex.Message}");
            return Composition.ExitCodeMapper.ConflictError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] {ex.Message}");
            return Composition.ExitCodeMapper.ConfigurationError;
        }
    }

}
