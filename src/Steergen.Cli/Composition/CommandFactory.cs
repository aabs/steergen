using System.CommandLine;

namespace Steergen.Cli.Composition;

public static class CommandFactory
{
    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("specgen – steering document generator");

        var runCommand = new Command("run", "Generate outputs from steering documents");
        runCommand.SetAction(_ =>
        {
            Console.WriteLine("run: not yet implemented");
        });

        var validateCommand = Commands.ValidateCommand.Create();

        var inspectCommand = Commands.InspectCommand.Create();

        var initCommand = Commands.InitCommand.Create();

        var updateCommand = Commands.UpdateCommand.Create();

        rootCommand.Add(runCommand);
        rootCommand.Add(validateCommand);
        rootCommand.Add(inspectCommand);
        rootCommand.Add(initCommand);
        rootCommand.Add(updateCommand);

        return rootCommand;
    }
}
