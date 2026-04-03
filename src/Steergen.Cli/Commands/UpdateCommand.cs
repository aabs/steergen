using System.CommandLine;
using Steergen.Core.Updates;

namespace Steergen.Cli.Commands;

/// <summary>
/// Updates the template-pack version recorded in the project configuration.
/// Exits with code 0 (success) or 2 (invalid version / config error).
/// </summary>
public static class UpdateCommand
{
    public static Command Create()
    {
        var configOption = new Option<string>("--config", "Path to steergen.config.yaml")
        {
            DefaultValueFactory = _ => "steergen.config.yaml",
        };

        var versionOption = new Option<string?>("--version", "Exact version to pin (e.g. 1.2.0 or 1.2.0-preview1)");

        var previewOption = new Option<bool>("--preview", "Include preview versions when resolving latest");

        var cmd = new Command("update", "Update template-pack version in the project configuration")
        {
            configOption,
            versionOption,
            previewOption,
        };

        cmd.SetAction(async (parseResult, ct) =>
        {
            var configPath = parseResult.GetValue(configOption)!;
            var version    = parseResult.GetValue(versionOption);
            var preview    = parseResult.GetValue(previewOption);

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
            var result  = await updater.UpdateAsync(configPath, version, preview, cancellationToken: cancellationToken)
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
}
