using System.CommandLine;

namespace Steergen.Cli.Commands;

/// <summary>
/// Parent command for rules pack management: <c>steergen rules-pack</c>.
/// Subcommands: <c>list</c>, <c>add</c>, <c>remove</c>.
/// </summary>
public static class RulesPackCommand
{
    public static Command Create()
    {
        var cmd = new Command("rules-pack", "Manage rules packs in the steergen config");
        cmd.Add(RulesPackAddCommand.Create());
        cmd.Add(RulesPackListCommand.Create());
        cmd.Add(RulesPackRemoveCommand.Create());
        cmd.Add(RulesPackUpgradeCommand.Create());
        return cmd;
    }
}
