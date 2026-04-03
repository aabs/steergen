using System.CommandLine;
using Steergen.Cli.Composition;

var rootCommand = CommandFactory.CreateRootCommand();
var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync(new InvocationConfiguration()).ConfigureAwait(false);

