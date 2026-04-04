using System.CommandLine;
using Steergen.Cli.Composition;

namespace Steergen.Cli.IntegrationTests;

/// <summary>
/// Regression tests that exercise root command construction and parsing.
/// These guard against invalid option alias wiring that can crash startup.
/// </summary>
public sealed class CommandFactoryRegressionTests
{
    [Fact]
    public async Task RootCommand_Help_InvokesSuccessfully()
    {
        var root = CommandFactory.CreateRootCommand();
        var parseResult = root.Parse(["--help"]);

        var exitCode = await parseResult.InvokeAsync(new InvocationConfiguration());

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task TargetAdd_Help_InvokesSuccessfully()
    {
        var root = CommandFactory.CreateRootCommand();
        var parseResult = root.Parse(["target", "add", "--help"]);

        var exitCode = await parseResult.InvokeAsync(new InvocationConfiguration());

        Assert.Equal(0, exitCode);
    }
}
