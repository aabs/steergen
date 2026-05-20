using System.CommandLine;
using System.Reflection;
using Steergen.Cli.Diagnostics;
using Steergen.Core.Configuration;
using Steergen.Core.Generation;
using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Parsing;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Agents;
using Steergen.Core.Targets.Kiro;
using Steergen.Core.Targets.Speckit;
using Steergen.Core.Validation;
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
            var configPath = ConfigPathResolver.ResolveOptional(parseResult.GetValue(configOption));
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
            var defaultOutputPath = Directory.GetCurrentDirectory();
            SteeringConfiguration? config = null;
            if (configPath is not null)
            {
                if (!File.Exists(configPath))
                {
                    Console.Error.WriteLine($"[error] Config file not found: {configPath}");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }
                var loader = new SteergenConfigLoader();

                // Check for deprecated globalRoot field (CFG001)
                var deprecationDiag = await loader.CheckForDeprecatedFieldsAsync(configPath, cancellationToken);
                if (deprecationDiag is not null)
                {
                    Console.Error.WriteLine($"[error] {deprecationDiag.Code}: {deprecationDiag.Message}");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }

                config = await loader.LoadAsync(configPath, cancellationToken);
            }

            // Resolve roots: CLI args > config file
            var resolvedGlobal = globalRoot; // globalRoot config field removed; use rules packs instead
            var resolvedProject = projectRoot ?? config?.ProjectRoot;
            var resolvedGenerationRoot = outputBase ?? config?.GenerationRoot ?? defaultOutputPath;
            var activeProfiles = config?.ActiveProfiles ?? [];

            if (resolvedGlobal is null && resolvedProject is null)
            {
                Console.Error.WriteLine("[error] Provide --global and/or --project (or a --config with projectRoot set).");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            // Resolve target IDs: explicit CLI > registered targets in config > all built-in targets
            var targetIds = explicitTargets.Count > 0
                ? explicitTargets
                : (IReadOnlyList<string>)(config?.RegisteredTargets ?? []);

            // Construct the template provider using TemplateResolver with three-level override chain.
            // When no template pack is configured, localOverridePath and cachedPackPath are null,
            // so the resolver falls back directly to EmbeddedTemplateProvider.
            var embeddedProvider = new EmbeddedTemplateProvider();
            ITemplateProvider templateProvider;

            string? localOverridePath = null;
            string? cachedPackPath = null;
            IReadOnlySet<string>? declaredTargets = null;
            PackManifest? packManifest = null;

            if (config?.TemplatePack is { } templatePackConfig)
            {
                // Resolve local override path from config
                localOverridePath = templatePackConfig.LocalPath;
                if (localOverridePath is not null && configPath is not null && !Path.IsPathRooted(localOverridePath))
                {
                    var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
                    localOverridePath = Path.GetFullPath(Path.Combine(configDir, localOverridePath));
                }

                // Resolve cached GitHub pack path from config
                if (templatePackConfig.Source is not null)
                {
                    var packSource = GitHubPackSourceParser.Parse(
                        templatePackConfig.Source, templatePackConfig.Ref);
                    if (packSource is not null)
                    {
                        var cacheBase = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".steergen");
                        var downloader = new PackDownloader(new HttpClient(), cacheBase);
                        var resolvedCachePath = downloader.GetCachedPath(packSource, PackType.Template);

                        if (Directory.Exists(resolvedCachePath))
                        {
                            cachedPackPath = resolvedCachePath;

                            // Parse pack manifest to get declared targets
                            var manifestParser = new PackManifestParser();
                            packManifest = manifestParser.Parse(resolvedCachePath);
                            if (packManifest?.Targets is { Count: > 0 } targets)
                            {
                                declaredTargets = new HashSet<string>(targets, StringComparer.Ordinal);
                            }
                        }
                        else
                        {
                            // TP007: configured GitHub pack not in local cache
                            Console.Error.WriteLine(
                                $"[error] TP007: Configured template pack is not in the local cache. " +
                                $"Run 'steergen update --templates' to download it.");
                            return Composition.ExitCodeMapper.ConfigurationError;
                        }
                    }
                }
            }

            templateProvider = new TemplateResolver(
                localOverridePath,
                cachedPackPath,
                embeddedProvider,
                declaredTargets);

            // Build map of all known built-in targets using the resolved template provider
            var allComponents = new Dictionary<string, ITargetComponent>(StringComparer.Ordinal)
            {
                [TargetRegistry.KnownTargets.Speckit] = new SpeckitTargetComponent(templateProvider),
                [TargetRegistry.KnownTargets.Kiro] = new KiroTargetComponent(templateProvider),
                [TargetRegistry.KnownTargets.CopilotAgent] = new CopilotAgentTargetComponent(templateProvider),
                [TargetRegistry.KnownTargets.KiroAgent] = new KiroAgentTargetComponent(templateProvider),
            };

            // Register pack-provided external targets if a template pack manifest declares them
            if (packManifest?.ProvidedTargets is { Count: > 0 } && cachedPackPath is not null)
            {
                var packDiagnostics = TargetRegistry.RegisterPackTargets(
                    packManifest, cachedPackPath, templateProvider);

                foreach (var diag in packDiagnostics)
                {
                    Console.Error.WriteLine($"[error] {diag.Code}: {diag.Message}");
                }

                // Add registered pack targets to the available components map
                foreach (var providedTarget in packManifest.ProvidedTargets)
                {
                    var layoutPath = Path.Combine(cachedPackPath, providedTarget.DefaultLayout);
                    if (File.Exists(layoutPath) && !allComponents.ContainsKey(providedTarget.TargetId))
                    {
                        allComponents[providedTarget.TargetId] = new PackTargetComponent(
                            providedTarget.TargetId,
                            templateProvider,
                            layoutPath,
                            packManifest.Name,
                            providedTarget.Description);
                    }
                }
            }

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
                    OutputPath = resolvedGenerationRoot,
                });
            }

            // outputPath remains a legacy config field. Routed layout destinations are based on
            // the layout plan plus the CLI-selected base directory (or current directory when omitted).
            targetConfigs = targetConfigs
                .Select(t => t with { OutputPath = resolvedGenerationRoot })
                .ToList();

            // Resolve relative layoutOverridePath values relative to the config file directory.
            if (configPath is not null)
            {
                var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
                targetConfigs = targetConfigs
                    .Select(t => t with
                    {
                        LayoutOverridePath =
                            t.LayoutOverridePath is not null && !Path.IsPathRooted(t.LayoutOverridePath)
                                ? Path.GetFullPath(Path.Combine(configDir, t.LayoutOverridePath))
                                : t.LayoutOverridePath,
                    })
                    .ToList();
            }

            var (globalDocs, projectDocs) = await reporter.MeasureAsync("load-documents", () =>
            {
                var g = LoadDocuments(resolvedGlobal);
                var p = LoadDocuments(resolvedProject);
                return Task.FromResult((g, p));
            });

            // Load rules packs before merge step
            IReadOnlyList<ScopedPackDocuments>? packDocuments = null;
            if (config?.RulesPacks is { Count: > 0 } rulesPackEntries)
            {
                var cacheBase = GetCacheBaseDirectory();
                var runningVersion = GetRunningSteergenVersion();
                var manifestParser = new PackManifestParser();
                var validator = new SteeringValidator();
                var rulesPackLoader = new RulesPackLoader(manifestParser, validator);

                // Convert RulesPackEntry config entries to RulesPackConfiguration
                var packConfigs = new List<RulesPackConfiguration>();
                foreach (var entry in rulesPackEntries)
                {
                    var source = GitHubPackSourceParser.Parse(entry.Source, entry.Ref, entry.Path);
                    if (source is null)
                    {
                        Console.Error.WriteLine(
                            $"[error] Invalid rules pack source format: '{entry.Source}'");
                        return Composition.ExitCodeMapper.ConfigurationError;
                    }

                    packConfigs.Add(new RulesPackConfiguration
                    {
                        Source = source,
                        ScopeOverride = entry.Scope
                    });
                }

                var loadResult = rulesPackLoader.Load(packConfigs, cacheBase, runningVersion);

                // Check for fatal errors (RP005 = pack not in cache)
                var fatalErrors = loadResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();

                if (fatalErrors.Count > 0)
                {
                    foreach (var diag in fatalErrors)
                    {
                        Console.Error.WriteLine($"[error] {diag.Code}: {diag.Message}");
                    }
                    return Composition.ExitCodeMapper.ConfigurationError;
                }

                // Emit non-fatal diagnostics (warnings)
                foreach (var diag in loadResult.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Error))
                {
                    if (!quiet)
                        Console.Error.WriteLine($"[warning] {diag.Code}: {diag.Message}");
                }

                // Group loaded documents by scope for the extended resolver
                if (loadResult.Documents.Count > 0)
                {
                    packDocuments = loadResult.Documents
                        .GroupBy(d => d.Rules.FirstOrDefault()?.SourcePackScope ?? PackScope.Global)
                        .Select(g => new ScopedPackDocuments
                        {
                            Scope = g.Key,
                            Documents = g.ToList()
                        })
                        .ToList();
                }
            }

            var pipeline = new GenerationPipeline();
            var result = await reporter.MeasureAsync("run-pipeline", () =>
                pipeline.RunAsync(
                    globalDocs,
                    projectDocs,
                    activeProfiles,
                    selectedComponents,
                    targetConfigs,
                    cancellationToken,
                    manifestOutputPath: outputBase,
                    globalRoot: resolvedGlobal,
                    projectRoot: resolvedProject,
                    packDocuments: packDocuments));

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

            if (verbose && result.RouteResolutions is not null)
                EmitRoutingDiagnostics(result.RouteResolutions);

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
        catch (TemplatePackException ex)
        {
            Console.Error.WriteLine($"[error] {ex.Diagnostic.Code}: {ex.Diagnostic.Message}");
            return ex.ExitCode;
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

    private static void EmitRoutingDiagnostics(
        IReadOnlyDictionary<string, IReadOnlyList<Core.Model.RouteResolutionResult>> routeResolutions)
    {
        foreach (var (targetId, resolutions) in routeResolutions.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var resolved = resolutions.Where(r => r.IsResolved).ToList();
            var failed = resolutions.Where(r => !r.IsResolved).ToList();

            Console.Error.WriteLine(
                $"[routing] {targetId}: {resolved.Count}/{resolutions.Count} rules routed" +
                (failed.Count > 0 ? $", {failed.Count} unresolved (see below)" : ""));

            foreach (var r in resolved.OrderBy(r => r.RuleId, StringComparer.Ordinal))
            {
                var dest = Path.GetFileName(r.SelectedDestinationPath) ?? r.SelectedDestinationPath;
                Console.Error.WriteLine(
                    $"  [routing] {r.RuleId} → {dest}" +
                    $" (route: {r.SelectedRouteId}, source: {r.Source})");
            }

            foreach (var r in failed.OrderBy(r => r.RuleId, StringComparer.Ordinal))
            {
                Console.Error.WriteLine(
                    $"  [routing:fail] {r.RuleId}: {r.SelectionReason}");
            }
        }
    }

    private static string GetCacheBaseDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".steergen");
    }

    private static string GetRunningSteergenVersion()
    {
        var assembly = typeof(RunCommand).Assembly;
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (infoVersion is not null)
        {
            // Strip build metadata (e.g., "+abc123") if present
            var plusIndex = infoVersion.IndexOf('+');
            if (plusIndex >= 0)
                infoVersion = infoVersion[..plusIndex];

            // Strip prerelease suffix (e.g., "-preview1") for semver comparison
            var dashIndex = infoVersion.IndexOf('-');
            if (dashIndex >= 0)
                infoVersion = infoVersion[..dashIndex];

            return infoVersion;
        }

        var version = assembly.GetName().Version;
        return version is not null
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "0.0.0";
    }
}
