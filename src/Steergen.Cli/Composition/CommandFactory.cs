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

        var inspectCommand = new Command("inspect", "Inspect the merged steering model");
        inspectCommand.SetAction(_ =>
        {
            Console.WriteLine("inspect: not yet implemented");
        });

        rootCommand.Add(runCommand);
        rootCommand.Add(validateCommand);
        rootCommand.Add(inspectCommand);

        return rootCommand;
    }
}
