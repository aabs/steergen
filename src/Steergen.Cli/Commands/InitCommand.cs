using System.CommandLine;
using Steergen.Core.Targets;

namespace Steergen.Cli.Commands;

/// <summary>
/// Bootstraps steering and target output folders for a project root.
/// Exits with code 0 (success) or 2 (invalid target / IO error).
/// </summary>
public static class InitCommand
{
    public static Command Create()
    {
        var projectRootArg = new Argument<string>("project-root")
        {
            Description = "Root directory of the project to initialise",
            DefaultValueFactory = _ => ".",
        };

        var targetOption = new Option<string[]>("--target", "Target(s) to bootstrap (e.g. speckit, kiro, copilot-agent, kiro-agent)")
        {
            AllowMultipleArgumentsPerToken = false,
            Arity = ArgumentArity.ZeroOrMore,
        };

        var cmd = new Command("init", "Bootstrap steering and target output folders")
        {
            projectRootArg,
            targetOption,
        };

        cmd.SetAction((parseResult, _) =>
        {
            var projectRoot = parseResult.GetValue(projectRootArg)!;
            var targets = parseResult.GetValue(targetOption) ?? [];

            return Task.FromResult(RunAsync(projectRoot, targets));
        });

        return cmd;
    }

    public static int RunAsync(string projectRoot, IEnumerable<string> targetIds)
    {
        try
        {
            if (!Directory.Exists(projectRoot))
            {
                Console.Error.WriteLine($"[error] Project root not found: {projectRoot}");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            var result = TargetLayoutInitializer.Initialize(projectRoot, targetIds);

            if (!result.Success)
            {
                Console.Error.WriteLine($"[error] {result.ErrorMessage}");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            foreach (var folder in result.CreatedFolders)
                Console.Error.WriteLine($"  created  {folder}");

            foreach (var folder in result.ExistingFolders)
                Console.Error.WriteLine($"  exists   {folder}");

            return Composition.ExitCodeMapper.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] {ex.Message}");
            return Composition.ExitCodeMapper.ConfigurationError;
        }
    }
}
