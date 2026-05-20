using System.CommandLine;
using Steergen.Core.Configuration;

namespace Steergen.Cli.Commands;

/// <summary>
/// Implements <c>steergen rules-pack remove {source}</c>.
/// Removes the matching rules pack entry from <c>steergen.config.yaml</c> by source name.
/// Exits with code 0 (success), 2 (config/IO error), or 5 (optimistic-lock conflict).
/// </summary>
public static class RulesPackRemoveCommand
{
    public static Command Create()
    {
        var sourceArg = new Argument<string>("source")
        {
            Description = "Source of the rules pack to remove (e.g. github:owner/repo)",
        };
        var configOption = new Option<string?>("--config")
        {
            Description = "Path to the steergen config file (default: steergen.config.yaml)",
        };

        var cmd = new Command("remove", "Remove a rules pack from the steergen config")
        {
            sourceArg,
            configOption,
        };

        cmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var source = parseResult.GetValue(sourceArg)!;
            var configPath = ConfigPathResolver.ResolveRequired(parseResult.GetValue(configOption));
            return await ExecuteAsync(configPath, source, cancellationToken);
        });

        return cmd;
    }

    public static async Task<int> ExecuteAsync(
        string configPath,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var svc = new RulesPackRegistrationService();
            var result = await svc.RemoveAsync(configPath, source, cancellationToken);

            if (!result.Success)
            {
                Console.Error.WriteLine($"[error] {result.ErrorMessage}");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            if (result.WasNotPresent)
                Console.Error.WriteLine($"[info] Rules pack '{source}' was not configured (no change).");
            else
                Console.Error.WriteLine($"[info] Rules pack '{source}' removed successfully.");

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
