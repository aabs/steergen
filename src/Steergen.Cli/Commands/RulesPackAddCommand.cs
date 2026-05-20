using System.CommandLine;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;

namespace Steergen.Cli.Commands;

/// <summary>
/// Implements <c>steergen rules-pack add github:{owner}/{repo}</c>.
/// Appends a rules pack entry to the <c>rulesPacks</c> list in <c>steergen.config.yaml</c>
/// and triggers download to the local pack cache.
/// Accepts <c>--ref</c> for tag/branch/SHA, <c>--path</c> for subdirectory within repo,
/// and <c>--scope</c> for consumer scope override.
/// Exits with code 0 (success), 2 (config/IO error), or 5 (optimistic-lock conflict).
/// </summary>
public static class RulesPackAddCommand
{
    public static Command Create()
    {
        var sourceArg = new Argument<string>("source")
        {
            Description = "Rules pack source in the format github:{owner}/{repo}",
        };

        var refOption = new Option<string?>("--ref")
        {
            Description = "Git tag, branch, or 40-character commit SHA to pin the rules pack version",
        };

        var pathOption = new Option<string?>("--path")
        {
            Description = "Subdirectory within the repository containing the rules pack",
        };

        var scopeOption = new Option<string?>("--scope")
        {
            Description = "Scope override for the rules pack (global, supplemental, or project)",
        };

        var configOption = new Option<string?>("--config")
        {
            Description = "Path to the steergen config file (default: steergen.config.yaml)",
        };

        var cmd = new Command("add", "Add a rules pack to the steergen config and download it")
        {
            sourceArg,
            refOption,
            pathOption,
            scopeOption,
            configOption,
        };

        cmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var source = parseResult.GetValue(sourceArg)!;
            var refValue = parseResult.GetValue(refOption);
            var path = parseResult.GetValue(pathOption);
            var scope = parseResult.GetValue(scopeOption);
            var configPath = ConfigPathResolver.ResolveRequired(parseResult.GetValue(configOption));

            return await ExecuteAsync(configPath, source, refValue, path, scope, cancellationToken);
        });

        return cmd;
    }

    public static async Task<int> ExecuteAsync(
        string configPath,
        string source,
        string? refValue,
        string? path,
        string? scopeStr,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse the scope option if provided
            PackScope? scope = null;
            if (scopeStr is not null)
            {
                if (!TryParseScope(scopeStr, out var parsedScope))
                {
                    Console.Error.WriteLine($"[error] Invalid scope '{scopeStr}'. Must be one of: global, supplemental, project.");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }
                scope = parsedScope;
            }

            // Parse the GitHub source
            var parsed = GitHubPackSourceParser.Parse(source, refValue, path);
            if (parsed is null)
            {
                Console.Error.WriteLine($"[error] Invalid source format: '{source}'. Expected format: github:{{owner}}/{{repo}}");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            // Trigger download via centralized factory
            var downloader = Composition.PackDownloaderFactory.Create();
            var downloadResult = await downloader.DownloadAsync(parsed, PackType.Rules, force: false, cancellationToken);

            if (!downloadResult.Success)
            {
                foreach (var diag in downloadResult.Diagnostics)
                {
                    Console.Error.WriteLine($"[{diag.Severity.ToString().ToLowerInvariant()}] {diag.Code}: {diag.Message}");
                }
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            // Emit diagnostic warning for branch refs (recommend pinning to SHA/tag)
            Composition.PackDownloaderFactory.EmitBranchRefWarning(refValue, PackType.Rules);

            // Add to config
            var entry = new RulesPackEntry
            {
                Source = source,
                Ref = refValue,
                Path = path,
                Scope = scope,
            };

            var svc = new RulesPackRegistrationService();
            var result = await svc.AddAsync(configPath, entry, cancellationToken);

            if (!result.Success)
            {
                Console.Error.WriteLine($"[error] {result.ErrorMessage}");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            if (result.WasAlreadyPresent)
            {
                Console.Error.WriteLine($"[info] Rules pack '{source}' is already configured (no change).");
            }
            else
            {
                Console.Error.WriteLine($"[info] Rules pack '{source}' added and downloaded successfully.");
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

    private static bool TryParseScope(string value, out PackScope scope)
    {
        scope = default;
        if (string.Equals(value, "global", StringComparison.OrdinalIgnoreCase))
        {
            scope = PackScope.Global;
            return true;
        }
        if (string.Equals(value, "supplemental", StringComparison.OrdinalIgnoreCase))
        {
            scope = PackScope.Supplemental;
            return true;
        }
        if (string.Equals(value, "project", StringComparison.OrdinalIgnoreCase))
        {
            scope = PackScope.Project;
            return true;
        }
        return false;
    }
}
