using System.CommandLine;

namespace Steergen.Cli.Composition;

public static class CommandFactory
{
    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("specgen – steering document generator");

        var runCommand = Commands.RunCommand.Create();
        var validateCommand = Commands.ValidateCommand.Create();
        var inspectCommand = Commands.InspectCommand.Create();
        var initCommand = Commands.InitCommand.Create();
        var updateCommand = Commands.UpdateCommand.Create();
        var targetCommand = Commands.TargetCommand.Create();

        var purgeCommand = Commands.PurgeCommand.Create();

        rootCommand.Add(runCommand);
        rootCommand.Add(validateCommand);
        rootCommand.Add(inspectCommand);
        rootCommand.Add(initCommand);
        rootCommand.Add(updateCommand);
        rootCommand.Add(targetCommand);
        rootCommand.Add(purgeCommand);

        return rootCommand;
    }
}
