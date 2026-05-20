using System.CommandLine;
using Steergen.Core.Configuration;
using Steergen.Core.Packs;

namespace Steergen.Cli.Commands;

/// <summary>
/// Implements <c>steergen rules-pack list</c>.
/// Displays all configured rules packs with their source, ref, scope, and cache status.
/// Exits with code 0 (success) or 2 (configuration error).
/// </summary>
public static class RulesPackListCommand
{
    public static Command Create()
    {
        var configOption = new Option<string?>("--config")
        {
            Description = "Path to steergen.config.yaml (default: steergen.config.yaml in the current directory)",
        };

        var cmd = new Command("list", "List all configured rules packs with status")
        {
            configOption,
        };

        cmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var configPath = ConfigPathResolver.ResolveRequired(parseResult.GetValue(configOption));
            return await RunAsync(configPath, cancellationToken);
        });

        return cmd;
    }

    public static async Task<int> RunAsync(
        string configPath,
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
                Console.WriteLine("No rules packs configured.");
                return Composition.ExitCodeMapper.Success;
            }

            var cacheBaseDirectory = GetCacheBaseDirectory();
            var downloader = new PackDownloader(new HttpClient(), cacheBaseDirectory);

            Console.WriteLine($"{"Source",-40} {"Ref",-20} {"Scope",-14} {"Cached"}");
            Console.WriteLine(new string('-', 85));

            foreach (var entry in config.RulesPacks)
            {
                var source = GitHubPackSourceParser.Parse(entry.Source, entry.Ref, entry.Path);
                var refDisplay = entry.Ref ?? "(default)";
                var scopeDisplay = entry.Scope?.ToString().ToLowerInvariant() ?? "(manifest)";

                string cachedDisplay;
                if (source is not null)
                {
                    var cachedPath = downloader.GetCachedPath(source, PackType.Rules);
                    cachedDisplay = Directory.Exists(cachedPath) ? "yes" : "no";
                }
                else
                {
                    cachedDisplay = "invalid source";
                }

                Console.WriteLine($"{entry.Source,-40} {refDisplay,-20} {scopeDisplay,-14} {cachedDisplay}");
            }

            return Composition.ExitCodeMapper.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] {ex.Message}");
            return Composition.ExitCodeMapper.ConfigurationError;
        }
    }

    private static string GetCacheBaseDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".steergen");
    }
}
