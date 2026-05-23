using System.CommandLine;
using Steergen.Core.Configuration;
using Steergen.Core.Updates;

namespace Steergen.Cli.Commands;

public static class RulesPackUpgradeCommand
{
    public static Command Create()
    {
        var selectorOption = new Option<string>("--selector")
        {
            Description = "Canonical selector in the format <source>|<path-or-entry-key>",
            Required = true,
        };

        var tagOption = new Option<string?>("--tag")
        {
            Description = "Explicit tag to upgrade to. When omitted, performs latest refresh.",
        };

        var configOption = new Option<string?>("--config")
        {
            Description = "Path to steergen.config.yaml (default: steergen.config.yaml in the current directory)",
        };

        var cmd = new Command("upgrade", "Upgrade a configured rules pack reference")
        {
            selectorOption,
            tagOption,
            configOption,
        };

        cmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var selector = parseResult.GetValue(selectorOption)!;
            var tag = parseResult.GetValue(tagOption);
            var configPath = ConfigPathResolver.ResolveRequired(parseResult.GetValue(configOption));
            return await ExecuteAsync(configPath, selector, tag, cancellationToken: cancellationToken).ConfigureAwait(false);
        });

        return cmd;
    }

    public static async Task<int> ExecuteAsync(
        string configPath,
        string selector,
        string? tag,
        ExternalPackUpgradeService? service = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var selectorResolver = new PackSelectorResolver();
            if (!selectorResolver.TryParse(selector, out _, out var selectorError))
            {
                Console.Error.WriteLine($"[error] {selectorError}");
                return Composition.ExitCodeMapper.UpgradeValidationError;
            }

            service ??= new ExternalPackUpgradeService();
            var result = await service.UpgradeAsync(
                configPath,
                new ExternalPackUpgradeRequest(UpgradePackKind.Rules, selector, tag),
                cancellationToken).ConfigureAwait(false);

            if (!result.Success)
            {
                Console.Error.WriteLine($"[error] {result.ErrorMessage}");
                foreach (var diagnostic in result.Diagnostics)
                    Console.Error.WriteLine($"[{diagnostic.Severity.ToString().ToLowerInvariant()}] {diagnostic.Code}: {diagnostic.Message}");

                return Composition.ExitCodeMapper.FromUpgradeResult(result);
            }

            Console.Error.WriteLine($"[info] mode={(tag is null ? "latest-refresh" : "explicit-tag")}");
            Console.Error.WriteLine($"[info] selector={selector}");
            Console.Error.WriteLine($"[info] final=({result.FinalTag},{result.FinalCommitSha})");
            return Composition.ExitCodeMapper.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] {ex.Message}");
            return Composition.ExitCodeMapper.ConfigurationError;
        }
    }
}
