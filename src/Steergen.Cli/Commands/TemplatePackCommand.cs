using System.CommandLine;

namespace Steergen.Cli.Commands;

/// <summary>
/// Parent command for template pack management.
/// Subcommands: <c>template-pack add</c>, <c>template-pack upgrade</c>, <c>template-pack remove</c>.
/// </summary>
public static class TemplatePackCommand
{
    public static Command Create()
    {
        var cmd = new Command("template-pack", "Manage the template pack configuration");
        cmd.Add(TemplatePackAddCommand.Create());
        cmd.Add(TemplatePackUpgradeCommand.Create());
        cmd.Add(TemplatePackRemoveCommand.Create());
        return cmd;
    }
}
