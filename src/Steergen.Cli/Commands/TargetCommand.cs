using System.CommandLine;
using Steergen.Core.Configuration;

namespace Steergen.Cli.Commands;

/// <summary>
/// Manages target registration in the steergen config file.
/// Subcommands: <c>target add &lt;id&gt;</c>, <c>target remove &lt;id&gt;</c>.
/// Exits with code 0 (success), 2 (config/IO error), or 5 (optimistic-lock conflict).
/// </summary>
public static class TargetCommand
{
    public static Command Create()
    {
        var cmd = new Command("target", "Manage registered targets in the steergen config");
        cmd.Add(CreateAddCommand());
        cmd.Add(CreateRemoveCommand());
        return cmd;
    }

    private static Command CreateAddCommand()
    {
        var targetIdArg = new Argument<string>("target-id")
        {
            Description = "ID of the target to register (e.g. speckit, kiro, copilot-agent, kiro-agent)",
        };
        var configOption = BuildConfigOption();

        var addCmd = new Command("add", "Register a target in the steergen config")
        {
            targetIdArg,
            configOption,
        };

        addCmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var targetId = parseResult.GetValue(targetIdArg)!;
            var configPath = parseResult.GetValue(configOption) ?? DefaultConfigPath();
            return await AddAsync(configPath, targetId, cancellationToken);
        });

        return addCmd;
    }

    private static Command CreateRemoveCommand()
    {
        var targetIdArg = new Argument<string>("target-id")
        {
            Description = "ID of the target to remove from registration",
        };
        var configOption = BuildConfigOption();

        var removeCmd = new Command("remove", "Deregister a target from the steergen config")
        {
            targetIdArg,
            configOption,
        };

        removeCmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var targetId = parseResult.GetValue(targetIdArg)!;
            var configPath = parseResult.GetValue(configOption) ?? DefaultConfigPath();
            return await RemoveAsync(configPath, targetId, cancellationToken);
        });

        return removeCmd;
    }

    public static async Task<int> AddAsync(
        string configPath,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var svc = new TargetRegistrationService();
            var result = await svc.AddAsync(configPath, targetId, cancellationToken);

            if (!result.Success)
            {
                Console.Error.WriteLine($"[error] {result.ErrorMessage}");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            if (result.WasAlreadyPresent)
                Console.Error.WriteLine($"[info] Target '{targetId}' is already registered (no change).");
            else
                Console.Error.WriteLine($"[info] Target '{targetId}' registered successfully.");

            return Composition.ExitCodeMapper.Success;
        }
        catch (Core.Configuration.ConfigWriteConflictException ex)
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

    public static async Task<int> RemoveAsync(
        string configPath,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var svc = new TargetRegistrationService();
            var result = await svc.RemoveAsync(configPath, targetId, cancellationToken);

            if (!result.Success)
            {
                Console.Error.WriteLine($"[error] {result.ErrorMessage}");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            if (result.WasNotPresent)
                Console.Error.WriteLine($"[info] Target '{targetId}' was not registered (no change).");
            else
                Console.Error.WriteLine($"[info] Target '{targetId}' deregistered successfully.");

            return Composition.ExitCodeMapper.Success;
        }
        catch (Core.Configuration.ConfigWriteConflictException ex)
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

    private static Option<string?> BuildConfigOption() =>
        new("--config")
        {
            Description = "Path to the steergen config file (default: steergen.config.yaml)",
        };

    private static string DefaultConfigPath() =>
        Path.Combine(Directory.GetCurrentDirectory(), "steergen.config.yaml");
}
