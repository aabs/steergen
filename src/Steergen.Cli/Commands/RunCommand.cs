using System.CommandLine;
using Steergen.Cli.Diagnostics;
using Steergen.Core.Configuration;
using Steergen.Core.Generation;
using Steergen.Core.Model;
using Steergen.Core.Parsing;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Agents;
using Steergen.Core.Targets.Kiro;
using Steergen.Core.Targets.Speckit;
using Steergen.Templates;

namespace Steergen.Cli.Commands;

/// <summary>
/// Runs steering document generation for one or more targets.
/// Supports explicit <c>--target</c> scoping or falls back to <c>registeredTargets</c> in the config.
/// Exits 0 (success), 1 (validation errors), 2 (config/IO error), 3 (generation error).
/// </summary>
public static class RunCommand
{
    public static Command Create()
    {
        var configOption = new Option<string?>("--config")
        {
            Description = "Path to steergen config file (default: steergen.config.yaml)",
        };
        var globalOption = new Option<string?>("--global")
        {
            Description = "Path to global steering documents directory",
        };
        var projectOption = new Option<string?>("--project")
        {
            Description = "Path to project steering documents directory",
        };
        var outputOption = new Option<string?>("--output")
        {
            Description = "Base output directory (overrides config)",
        };
        var targetOption = new Option<string[]>("--target")
        {
            Description = "Explicit target(s) to run (e.g. speckit, kiro, copilot-agent)",
            AllowMultipleArgumentsPerToken = false,
            Arity = ArgumentArity.ZeroOrMore,
        };
        var quietOption = new Option<bool>("--quiet")
        {
            Description = "Suppress informational output",
        };
        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Enable verbose diagnostics including opt-in measurement output (SC-001/SC-005)",
        };
        var debugOption = new Option<bool>("--debug")
        {
            Description = "Enable debug-level diagnostics including opt-in measurement output",
        };

        var cmd = new Command("run", "Generate outputs from steering documents")
        {
            configOption,
            globalOption,
            projectOption,
            outputOption,
            targetOption,
            quietOption,
            verboseOption,
            debugOption,
        };

        cmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var configPath = parseResult.GetValue(configOption);
            var globalRoot = parseResult.GetValue(globalOption);
            var projectRoot = parseResult.GetValue(projectOption);
            var outputBase = parseResult.GetValue(outputOption);
            var explicitTargets = parseResult.GetValue(targetOption) ?? [];
            var quiet = parseResult.GetValue(quietOption);
            var verbose = parseResult.GetValue(verboseOption);
            var debug = parseResult.GetValue(debugOption);

            return await RunAsync(
                configPath,
                globalRoot,
                projectRoot,
                outputBase,
                explicitTargets,
                quiet,
                verbose,
                debug,
                cancellationToken);
        });

        return cmd;
    }

    public static async Task<int> RunAsync(
        string? configPath,
        string? globalRoot,
        string? projectRoot,
        string? outputBase,
        IReadOnlyList<string> explicitTargets,
        bool quiet,
        bool verbose = false,
        bool debug = false,
        CancellationToken cancellationToken = default)
    {
        var reporter = new MeasurementProtocolReporter(verbose || debug);
        try
        {
            SteeringConfiguration? config = null;
            if (configPath is not null)
            {
                if (!File.Exists(configPath))
                {
                    Console.Error.WriteLine($"[error] Config file not found: {configPath}");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }
                var loader = new SteergenConfigLoader();
                config = await loader.LoadAsync(configPath, cancellationToken);
            }

            // Resolve roots: CLI args > config file > defaults
            var resolvedGlobal = globalRoot ?? config?.GlobalRoot;
            var resolvedProject = projectRoot ?? config?.ProjectRoot;
            var activeProfiles = config?.ActiveProfiles ?? [];

            if (resolvedGlobal is null && resolvedProject is null)
            {
                Console.Error.WriteLine("[error] Provide --global and/or --project (or a --config with globalRoot/projectRoot set).");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            // Resolve target IDs: explicit CLI > registered targets in config > all built-in targets
            var targetIds = explicitTargets.Count > 0
                ? explicitTargets
                : (IReadOnlyList<string>)(config?.RegisteredTargets ?? []);

            var templateProvider = new EmbeddedTemplateProvider();

            // Build map of all known built-in targets without using the static registry
            var allComponents = new Dictionary<string, ITargetComponent>(StringComparer.Ordinal)
            {
                [TargetRegistry.KnownTargets.Speckit] = new SpeckitTargetComponent(templateProvider),
                [TargetRegistry.KnownTargets.Kiro] = new KiroTargetComponent(templateProvider),
                [TargetRegistry.KnownTargets.CopilotAgent] = new CopilotAgentTargetComponent(templateProvider),
                [TargetRegistry.KnownTargets.KiroAgent] = new KiroAgentTargetComponent(templateProvider),
            };
            List<ITargetComponent> selectedComponents;
            List<TargetConfiguration> targetConfigs;

            if (targetIds.Count == 0)
            {
                if (!quiet)
                    Console.Error.WriteLine("[warning] No targets specified and no registeredTargets in config. Nothing to generate.");
                return Composition.ExitCodeMapper.Success;
            }

            selectedComponents = [];
            targetConfigs = [];

            foreach (var id in targetIds)
            {
                if (!allComponents.TryGetValue(id, out var component))
                {
                    Console.Error.WriteLine($"[error] Unknown target: '{id}'");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }
                selectedComponents.Add(component);

                // Look for existing config entry or synthesize a minimal one
                var existingConf = config?.Targets.FirstOrDefault(t =>
                    string.Equals(t.Id, id, StringComparison.Ordinal));

                targetConfigs.Add(existingConf ?? new TargetConfiguration
                {
                    Id = id,
                    Enabled = true,
                    OutputPath = outputBase is not null ? Path.Combine(outputBase, id) : id,
                });
            }

            // Override OutputPath if --output provided
            if (outputBase is not null)
            {
                targetConfigs = targetConfigs
                    .Select(t => t with { OutputPath = Path.Combine(outputBase, t.Id!) })
                    .ToList();
            }

            var (globalDocs, projectDocs) = await reporter.MeasureAsync("load-documents", () =>
            {
                var g = LoadDocuments(resolvedGlobal);
                var p = LoadDocuments(resolvedProject);
                return Task.FromResult((g, p));
            });

            var pipeline = new GenerationPipeline();
            var result = await reporter.MeasureAsync("run-pipeline", () =>
                pipeline.RunAsync(
                    globalDocs,
                    projectDocs,
                    activeProfiles,
                    selectedComponents,
                    targetConfigs,
                    cancellationToken,
                    manifestOutputPath: outputBase));

            reporter.EmitTotal();

            foreach (var diag in result.Diagnostics)
            {
                var sev = diag.Severity switch
                {
                    Core.Validation.DiagnosticSeverity.Error => "error",
                    Core.Validation.DiagnosticSeverity.Warning => "warning",
                    _ => "info",
                };
                if (diag.Severity == Core.Validation.DiagnosticSeverity.Error || !quiet)
                {
                    var loc = diag.Location is not null ? $"{diag.Location.FilePath}: " : string.Empty;
                    Console.Error.WriteLine($"{loc}[{sev}] {diag.Code}: {diag.Message}");
                }
            }

            if (!result.Success)
                return Composition.ExitCodeMapper.ValidationError;

            if (!quiet)
                Console.Error.WriteLine($"[info] Generation complete. Targets executed: {result.TargetsExecuted}");

            return Composition.ExitCodeMapper.Success;
        }
        catch (ConfigWriteConflictException ex)
        {
            Console.Error.WriteLine($"[conflict] {ex.Message}");
            return Composition.ExitCodeMapper.ConflictError;
        }
        catch (TargetGenerationException ex)
        {
            Console.Error.WriteLine($"[error] Target generation failed: {ex.Message}");
            return Composition.ExitCodeMapper.GenerationError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] {ex.Message}");
            return Composition.ExitCodeMapper.ConfigurationError;
        }
    }

    private static IReadOnlyList<Core.Model.SteeringDocument> LoadDocuments(string? root)
    {
        if (root is null || !Directory.Exists(root))
            return [];

        return Directory
            .EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(path => SteeringMarkdownParser.Parse(File.ReadAllText(path), path))
            .ToList();
    }
}
