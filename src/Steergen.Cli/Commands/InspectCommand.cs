using System.CommandLine;
using System.Reflection;
using Steergen.Core.Configuration;
using Steergen.Core.Merge;
using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Parsing;
using Steergen.Core.Targets;
using Steergen.Core.Validation;
using Steergen.Templates;

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

        var rulesOption = new Option<bool>("--rules")
        {
            Description = "Display all configured rules packs with name, version, source, scope, and number of rules loaded",
        };

        var templatesOption = new Option<bool>("--templates")
        {
            Description = "Display active template resolution chain showing source per template",
        };

        var cmd = new Command("inspect", "Inspect the merged steering model as JSON")
        {
            configOption,
            globalOption,
            projectOption,
            profileOption,
            rulesOption,
            templatesOption,
        };

        cmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var configPath = ConfigPathResolver.ResolveOptional(parseResult.GetValue(configOption));
            var globalRoot = parseResult.GetValue(globalOption);
            var projectRoot = parseResult.GetValue(projectOption);
            var profiles = parseResult.GetValue(profileOption) ?? [];
            var rules = parseResult.GetValue(rulesOption);
            var templates = parseResult.GetValue(templatesOption);

            if (templates)
            {
                var resolvedConfigPath = ConfigPathResolver.ResolveOptional(parseResult.GetValue(configOption));
                return await RunTemplatesInspectAsync(resolvedConfigPath, cancellationToken);
            }

            if (rules)
            {
                var resolvedConfigPath = ConfigPathResolver.ResolveRequired(parseResult.GetValue(configOption));
                return await RunRulesInspectAsync(resolvedConfigPath, cancellationToken);
            }

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

            globalRoot ??= null; // globalRoot config field removed; use rules packs instead
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

    /// <summary>
    /// Displays all configured rules packs with name, version, source, scope, and number of rules loaded.
    /// </summary>
    public static async Task<int> RunRulesInspectAsync(
        string configPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"[error] Config file not found: {configPath}");
                return Composition.ExitCodeMapper.ConfigurationError;
            }

            var loader = new SteergenConfigLoader();
            var config = await loader.LoadAsync(configPath, cancellationToken).ConfigureAwait(false);

            if (config.RulesPacks.Count == 0)
            {
                Console.WriteLine("No rules packs configured.");
                return Composition.ExitCodeMapper.Success;
            }

            var cacheBaseDirectory = GetCacheBaseDirectory();
            var downloader = new PackDownloader(new HttpClient(), cacheBaseDirectory);
            var manifestParser = new PackManifestParser();
            var validator = new SteeringValidator();
            var rulesPackLoader = new RulesPackLoader(manifestParser, validator);
            var runningVersion = GetRunningSteergenVersion();

            Console.WriteLine($"{"Name",-25} {"Version",-12} {"Source",-35} {"Scope",-14} {"Rules"}");
            Console.WriteLine(new string('-', 95));

            foreach (var entry in config.RulesPacks)
            {
                var source = GitHubPackSourceParser.Parse(entry.Source, entry.Ref, entry.Path);

                if (source is null)
                {
                    Console.WriteLine($"{"(invalid)",-25} {"-",-12} {entry.Source,-35} {"-",-14} -");
                    continue;
                }

                var cachedPath = downloader.GetCachedPath(source, PackType.Rules);

                if (!Directory.Exists(cachedPath))
                {
                    var scopeDisplay = entry.Scope?.ToString().ToLowerInvariant() ?? "(manifest)";
                    Console.WriteLine($"{"(not cached)",-25} {"-",-12} {entry.Source,-35} {scopeDisplay,-14} -");
                    continue;
                }

                // Parse manifest to get name and version
                var manifest = manifestParser.Parse(cachedPath);
                var packName = manifest?.Name ?? "(unknown)";
                var packVersion = manifest?.Version ?? "-";

                // Determine effective scope
                var effectiveScope = entry.Scope ?? manifest?.Scope;
                var scopeStr = effectiveScope?.ToString().ToLowerInvariant() ?? "(none)";

                // Count rules by loading the pack
                var packConfig = new RulesPackConfiguration
                {
                    Source = source,
                    ScopeOverride = entry.Scope,
                };

                var loadResult = rulesPackLoader.Load([packConfig], cacheBaseDirectory, runningVersion);
                var ruleCount = loadResult.Documents.Sum(d => d.Rules.Count);

                Console.WriteLine($"{packName,-25} {packVersion,-12} {entry.Source,-35} {scopeStr,-14} {ruleCount}");
            }

            return Composition.ExitCodeMapper.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] {ex.Message}");
            return Composition.ExitCodeMapper.ConfigurationError;
        }
    }

    private static IEnumerable<Core.Model.SteeringDocument> LoadDocuments(string root) =>
        Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(path => SteeringMarkdownParser.Parse(File.ReadAllText(path), path));

    /// <summary>
    /// Displays the active template resolution chain showing which source provides each template.
    /// </summary>
    public static async Task<int> RunTemplatesInspectAsync(
        string? configPath,
        CancellationToken cancellationToken = default)
    {
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
                config = await loader.LoadAsync(configPath, cancellationToken).ConfigureAwait(false);
            }

            // Resolve template pack configuration
            var templatePackConfig = config?.TemplatePack;
            string? localOverridePath = templatePackConfig?.LocalPath;
            string? cachedPackPath = null;
            IReadOnlySet<string>? declaredTargets = null;

            if (templatePackConfig?.Source is not null)
            {
                var parsed = GitHubPackSourceParser.Parse(templatePackConfig.Source, templatePackConfig.Ref);
                if (parsed is not null)
                {
                    var cacheBase = GetCacheBaseDirectory();
                    var downloader = new PackDownloader(new HttpClient(), cacheBase);
                    cachedPackPath = downloader.GetCachedPath(parsed, PackType.Template);

                    // Check if the cache directory actually exists
                    if (!Directory.Exists(cachedPackPath))
                    {
                        cachedPackPath = null;
                    }
                    else
                    {
                        // Try to parse pack manifest for declared targets
                        var manifestParser = new PackManifestParser();
                        var manifest = manifestParser.Parse(cachedPackPath);
                        if (manifest?.Targets is { Count: > 0 } targets)
                        {
                            declaredTargets = new HashSet<string>(targets, StringComparer.Ordinal);
                        }
                    }
                }
            }

            var embeddedProvider = new EmbeddedTemplateProvider();
            var templateResolver = new TemplateResolver(
                localOverridePath,
                cachedPackPath,
                embeddedProvider,
                declaredTargets);

            // Determine which targets and templates to inspect
            var templateMap = GetKnownTemplateMap();

            // Display header
            Console.Out.WriteLine("Template Resolution Chain");
            Console.Out.WriteLine("=========================");
            Console.Out.WriteLine();

            // Display configuration summary
            Console.Out.WriteLine("Configuration:");
            if (localOverridePath is not null)
                Console.Out.WriteLine($"  Local override path: {localOverridePath}");
            else
                Console.Out.WriteLine("  Local override path: (none)");

            if (templatePackConfig?.Source is not null)
            {
                Console.Out.WriteLine($"  GitHub pack source:  {templatePackConfig.Source}");
                Console.Out.WriteLine($"  GitHub pack ref:     {templatePackConfig.Ref ?? "(default branch)"}");
                Console.Out.WriteLine($"  Cached pack path:    {cachedPackPath ?? "(not cached)"}");
            }
            else
            {
                Console.Out.WriteLine("  GitHub pack source:  (none)");
            }

            if (declaredTargets is not null)
                Console.Out.WriteLine($"  Declared targets:    {string.Join(", ", declaredTargets.OrderBy(t => t, StringComparer.Ordinal))}");
            else
                Console.Out.WriteLine("  Declared targets:    (all)");

            Console.Out.WriteLine();
            Console.Out.WriteLine("Resolution per template:");
            Console.Out.WriteLine("------------------------");

            foreach (var (targetId, templateNames) in templateMap.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                Console.Out.WriteLine($"  {targetId}/");
                foreach (var templateName in templateNames.OrderBy(t => t, StringComparer.Ordinal))
                {
                    var source = templateResolver.GetTemplateSource(targetId, templateName);
                    var sourceLabel = FormatTemplateSource(source);
                    Console.Out.WriteLine($"    {templateName}.scriban → {sourceLabel}");
                }
            }

            Console.Out.WriteLine();
            return Composition.ExitCodeMapper.Success;
        }
        catch (TemplatePackException ex)
        {
            Console.Error.WriteLine($"[{ex.Diagnostic.Severity.ToString().ToLowerInvariant()}] {ex.Diagnostic.Code}: {ex.Diagnostic.Message}");
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] Unexpected error: {ex.Message}");
            return Composition.ExitCodeMapper.ConfigurationError;
        }
    }

    /// <summary>
    /// Returns the known template names for each built-in target.
    /// </summary>
    private static Dictionary<string, List<string>> GetKnownTemplateMap()
    {
        return new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            [TargetRegistry.KnownTargets.Kiro] = ["document"],
            [TargetRegistry.KnownTargets.Speckit] = ["constitution", "module"],
            [TargetRegistry.KnownTargets.CopilotAgent] = ["copilot.agent"],
            [TargetRegistry.KnownTargets.KiroAgent] = ["kiro.agent"],
        };
    }

    private static string FormatTemplateSource(TemplateSource source) =>
        source switch
        {
            TemplateSource.LocalOverride => "local override",
            TemplateSource.CachedGitHubPack => "cached GitHub pack",
            TemplateSource.BuiltInEmbedded => "built-in embedded",
            TemplateSource.ProvidedTarget => "provided target pack",
            _ => "unknown",
        };

    private static string GetCacheBaseDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".steergen");
    }

    private static string GetRunningSteergenVersion()
    {
        var assembly = typeof(InspectCommand).Assembly;
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
