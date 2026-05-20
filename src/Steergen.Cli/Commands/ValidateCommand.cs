using System.CommandLine;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Parsing;
using Steergen.Core.Validation;

namespace Steergen.Cli.Commands;

/// <summary>
/// Validates one or more steering document directories and reports diagnostics.
/// Exits with code 0 (no errors), 1 (validation errors found), or 2 (configuration/IO error).
/// </summary>
public static class ValidateCommand
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
        var quietOption = new Option<bool>("--quiet")
        {
            Description = "Suppress informational output; only emit errors",
        };

        var cmd = new Command("validate", "Validate steering documents")
        {
            configOption,
            globalOption,
            projectOption,
            quietOption,
        };

        cmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var configPath = ConfigPathResolver.ResolveOptional(parseResult.GetValue(configOption));
            var globalRoot = parseResult.GetValue(globalOption);
            var projectRoot = parseResult.GetValue(projectOption);
            var quiet = parseResult.GetValue(quietOption);

            return await RunAsync(globalRoot, projectRoot, quiet, configPath, cancellationToken);
        });

        return cmd;
    }

    public static async Task<int> RunAsync(
        string? globalRoot,
        string? projectRoot,
        bool quiet,
        string? configPath = null,
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

                // Check for deprecated globalRoot field (CFG001)
                var deprecationDiag = await loader.CheckForDeprecatedFieldsAsync(configPath, cancellationToken).ConfigureAwait(false);
                if (deprecationDiag is not null)
                {
                    Console.Error.WriteLine($"[error] {deprecationDiag.Code}: {deprecationDiag.Message}");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }

                config = await loader.LoadAsync(configPath, cancellationToken).ConfigureAwait(false);
            }

            globalRoot ??= null; // globalRoot config field removed; use rules packs instead
            projectRoot ??= config?.ProjectRoot;

            var allDocuments = new List<Core.Model.SteeringDocument>();

            if (globalRoot is not null)
            {
                if (!Directory.Exists(globalRoot))
                {
                    Console.Error.WriteLine($"[error] Global directory not found: {globalRoot}");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }
                allDocuments.AddRange(LoadDocuments(globalRoot));
            }

            if (projectRoot is not null)
            {
                if (!Directory.Exists(projectRoot))
                {
                    Console.Error.WriteLine($"[error] Project directory not found: {projectRoot}");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }
                allDocuments.AddRange(LoadDocuments(projectRoot));
            }

            if (allDocuments.Count == 0 && !quiet)
            {
                Console.Error.WriteLine("[warning] No steering documents found. Provide --global or --project.");
            }

            var validator = new SteeringValidator();
            var diagnostics = validator.ValidateCorpus(allDocuments);

            int errorCount = 0;
            int warningCount = 0;

            foreach (var diag in diagnostics)
            {
                ReportDiagnostic(diag, quiet, ref errorCount, ref warningCount);
            }

            // Validate template pack if configured
            var templatePackDiagnostics = ValidateTemplatePack(config);
            foreach (var diag in templatePackDiagnostics)
            {
                ReportDiagnostic(diag, quiet, ref errorCount, ref warningCount);
            }

            if (!quiet)
            {
                Console.Error.WriteLine($"Validation complete: {errorCount} error(s), {warningCount} warning(s).");
            }

            await Task.CompletedTask;
            return errorCount > 0 ? Composition.ExitCodeMapper.ValidationError : Composition.ExitCodeMapper.Success;
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

    private static void ReportDiagnostic(Diagnostic diag, bool quiet, ref int errorCount, ref int warningCount)
    {
        var severity = diag.Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            _ => "info",
        };

        if (diag.Severity == DiagnosticSeverity.Error)
            errorCount++;
        else if (diag.Severity == DiagnosticSeverity.Warning)
            warningCount++;

        if (diag.Severity == DiagnosticSeverity.Error || !quiet)
        {
            var lineInfo = diag.Location is not null ? $"({diag.Location.LineNumber})" : string.Empty;
            var location = diag.Location is not null ? $"{diag.Location.FilePath}{lineInfo}: " : string.Empty;
            Console.Error.WriteLine($"{location}[{severity}] {diag.Code}: {diag.Message}");
        }
    }

    /// <summary>
    /// Validates a configured template pack: checks all .scriban files are parseable,
    /// validates template file names match known template names for declared targets,
    /// and reports warnings for template files targeting unregistered targets.
    /// </summary>
    private static IReadOnlyList<Diagnostic> ValidateTemplatePack(SteeringConfiguration? config)
    {
        if (config?.TemplatePack is null)
            return [];

        var packPath = ResolveTemplatePackPath(config.TemplatePack);
        if (packPath is null)
            return [];

        if (!Directory.Exists(packPath))
            return [];

        var templatePackValidator = new TemplatePackValidator();
        var manifestParser = new PackManifestParser();
        var diagnostics = new List<Diagnostic>();

        // Parse the pack manifest to get declared targets
        var manifest = manifestParser.Parse(packPath);
        var declaredTargets = manifest?.Targets;
        var registeredTargets = config.RegisteredTargets;

        // Enumerate all .scriban files in the pack directory (deterministic order)
        var scribanFiles = Directory.EnumerateFiles(packPath, "*.scriban", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        foreach (var filePath in scribanFiles)
        {
            // Skip symbolic links
            var attributes = File.GetAttributes(filePath);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            // Extract target ID and template name from file path
            // Expected structure: {packPath}/{targetId}/{templateName}.scriban
            var relativePath = Path.GetRelativePath(packPath, filePath);
            var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (parts.Length < 2)
                continue; // Skip files not in a target subdirectory

            var targetId = parts[0];
            var templateName = Path.GetFileNameWithoutExtension(parts[^1]);

            // Validate Scriban syntax (Requirement 6.1, 6.2)
            var content = File.ReadAllText(filePath);
            var contentDiagnostics = templatePackValidator.ValidateTemplateContent(content, filePath);
            diagnostics.AddRange(contentDiagnostics);

            // Validate template name matches known names for the target (Requirement 6.3)
            var nameDiagnostics = templatePackValidator.ValidateTemplateName(templateName, targetId);
            diagnostics.AddRange(nameDiagnostics);

            // Report warning for template files targeting unregistered targets (Requirement 6.4)
            if (registeredTargets.Count > 0 && !registeredTargets.Contains(targetId))
            {
                diagnostics.Add(new Diagnostic(
                    "TP006",
                    $"Template file '{relativePath}' targets '{targetId}' which is not a registered target.",
                    DiagnosticSeverity.Warning));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Resolves the template pack directory path from configuration.
    /// Returns the local path if configured, otherwise attempts to resolve the cached GitHub pack path.
    /// </summary>
    private static string? ResolveTemplatePackPath(TemplatePackConfig templatePack)
    {
        // Local path takes precedence
        if (!string.IsNullOrWhiteSpace(templatePack.LocalPath))
            return templatePack.LocalPath;

        // GitHub source: resolve to cached pack path
        if (!string.IsNullOrWhiteSpace(templatePack.Source))
        {
            var source = GitHubPackSourceParser.Parse(templatePack.Source, templatePack.Ref);
            if (source is null)
                return null;

            var cacheBase = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".steergen");

            var refValue = source.Ref ?? "HEAD";
            return Path.Combine(cacheBase, "packs", source.Owner, source.Repo, refValue);
        }

        return null;
    }
}
