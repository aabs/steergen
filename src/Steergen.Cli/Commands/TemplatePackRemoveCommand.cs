using System.CommandLine;
using Steergen.Core.Configuration;

namespace Steergen.Cli.Commands;

/// <summary>
/// Removes the template pack configuration from <c>steergen.config.yaml</c>.
/// Exits with code 0 (success), 2 (config/IO error), or 5 (optimistic-lock conflict).
/// </summary>
public static class TemplatePackRemoveCommand
{
    public static Command Create()
    {
        var configOption = new Option<string?>("--config")
        {
            Description = "Path to steergen.config.yaml (default: steergen.config.yaml in the current directory)",
        };

        var cmd = new Command("remove", "Remove the template pack configuration from steergen.config.yaml")
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
            var svc = new TemplatePackService();
            var result = await svc.RemoveAsync(configPath, cancellationToken);

            if (!result.Success)
            {
                Console.Error.WriteLine($"[error] {result.ErrorMessage}");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            if (result.WasNotConfigured)
                Console.Error.WriteLine("[info] No template pack is configured (no change).");
            else
                Console.Error.WriteLine("[info] Template pack configuration removed.");

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
