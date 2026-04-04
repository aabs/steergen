using System.CommandLine;
using Steergen.Core.Configuration;
using Steergen.Core.Merge;
using Steergen.Core.Parsing;

namespace Steergen.Cli.Commands;

/// <summary>
/// Exposes the resolved steering model as deterministic JSON on stdout.
/// Exits with code 0 (success) or 2 (configuration/IO error).
/// </summary>
public static class InspectCommand
{
    public static Command Create()
    {
        var configOption = new Option<string?>("--config")
        {
            Description = "Path to steergen.config.yaml (default: steergen.config.yaml in the current directory)",
        };
        var globalOption = new Option<string?>("--global")
        {
            Description = "Path to the global steering documents directory",
        };
        var projectOption = new Option<string?>("--project")
        {
            Description = "Path to the project steering documents directory",
        };
        var profileOption = new Option<string[]>("--profile")
        {
            Description = "Active profiles to apply during resolution",
            AllowMultipleArgumentsPerToken = false,
        };
        profileOption.Arity = ArgumentArity.ZeroOrMore;

        var cmd = new Command("inspect", "Inspect the merged steering model as JSON")
        {
            configOption,
            globalOption,
            projectOption,
            profileOption,
        };

        cmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var configPath = ConfigPathResolver.ResolveOptional(parseResult.GetValue(configOption));
            var globalRoot = parseResult.GetValue(globalOption);
            var projectRoot = parseResult.GetValue(projectOption);
            var profiles = parseResult.GetValue(profileOption) ?? [];

            return await RunAsync(globalRoot, projectRoot, profiles, configPath, cancellationToken);
        });

        return cmd;
    }

    public static async Task<int> RunAsync(
        string? globalRoot,
        string? projectRoot,
        IEnumerable<string>? activeProfiles = null,
        string? configPath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Steergen.Core.Model.SteeringConfiguration? config = null;
            if (configPath is not null)
            {
                if (!File.Exists(configPath))
                {
                    Console.Error.WriteLine($"[error] Config file not found: {configPath}");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }

                var loader = new SteergenConfigLoader();
                config = await loader.LoadAsync(configPath, cancellationToken).ConfigureAwait(false);
            }

            globalRoot ??= config?.GlobalRoot;
            projectRoot ??= config?.ProjectRoot;
            activeProfiles ??= config?.ActiveProfiles ?? [];

            var globalDocuments = new List<Core.Model.SteeringDocument>();
            var projectDocuments = new List<Core.Model.SteeringDocument>();

            if (globalRoot is not null)
            {
                if (!Directory.Exists(globalRoot))
                {
                    Console.Error.WriteLine($"[error] Global directory not found: {globalRoot}");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }
                globalDocuments.AddRange(LoadDocuments(globalRoot));
            }

            if (projectRoot is not null)
            {
                if (!Directory.Exists(projectRoot))
                {
                    Console.Error.WriteLine($"[error] Project directory not found: {projectRoot}");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }
                projectDocuments.AddRange(LoadDocuments(projectRoot));
            }

            var resolver = new SteeringResolver();
            var model = resolver.Resolve(globalDocuments, projectDocuments, activeProfiles);

            var json = Core.Generation.InspectModelWriter.Write(model);
            await Console.Out.WriteLineAsync(json);

            return Composition.ExitCodeMapper.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] Unexpected error: {ex.Message}");
            return Composition.ExitCodeMapper.ConfigurationError;
        }
    }

    private static IEnumerable<Core.Model.SteeringDocument> LoadDocuments(string root) =>
        Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(path => SteeringMarkdownParser.Parse(File.ReadAllText(path), path));
}
