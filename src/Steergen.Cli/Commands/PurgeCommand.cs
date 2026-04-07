using System.CommandLine;
using Steergen.Core.Configuration;
using Steergen.Core.Generation;
using Steergen.Core.Model;
using Steergen.Core.Targets;

namespace Steergen.Cli.Commands;

/// <summary>
/// Deletes generated steering artifacts for selected targets based on configured purge roots and globs.
/// Works without a prior generation manifest.
/// Exits 0 (success), 1 (validation error), 2 (config/IO error), 3 (purge execution error).
/// </summary>
public static class PurgeCommand
{
    public static Command Create()
    {
        var configOption = new Option<string?>("--config")
        {
            Description = "Path to steergen config file (default: steergen.config.yaml)",
        };
        var targetOption = new Option<string[]>("--target")
        {
            Description = "Target(s) to purge (e.g. speckit, kiro). Defaults to all registered targets.",
            AllowMultipleArgumentsPerToken = false,
            Arity = ArgumentArity.ZeroOrMore,
        };
        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report what would be deleted without actually removing files.",
        };
        var quietOption = new Option<bool>("--quiet")
        {
            Description = "Suppress informational output.",
        };

        var cmd = new Command("purge", "Delete generated steering artifacts for selected targets")
        {
            configOption,
            targetOption,
            dryRunOption,
            quietOption,
        };

        cmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var configPath = ConfigPathResolver.ResolveOptional(parseResult.GetValue(configOption));
            var explicitTargets = parseResult.GetValue(targetOption) ?? [];
            var dryRun = parseResult.GetValue(dryRunOption);
            var quiet = parseResult.GetValue(quietOption);

            return await RunAsync(configPath, explicitTargets, dryRun, quiet, cancellationToken);
        });

        return cmd;
    }

    public static async Task<int> RunAsync(
        string? configPath,
        IReadOnlyList<string> explicitTargets,
        bool dryRun,
        bool quiet,
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
                config = await loader.LoadAsync(configPath, cancellationToken);
            }

            var resolvedGlobal = config?.GlobalRoot;
            var resolvedProject = config?.ProjectRoot;

            var targetIds = explicitTargets.Count > 0
                ? explicitTargets
                : (IReadOnlyList<string>)(config?.RegisteredTargets ?? []);

            if (targetIds.Count == 0)
            {
                if (!quiet)
                    Console.Error.WriteLine("[warning] No targets specified and no registeredTargets in config. Nothing to purge.");
                return Composition.ExitCodeMapper.Success;
            }

            // Validate target IDs are known
            foreach (var id in targetIds)
            {
                if (!TargetRegistry.HasDefaultLayout(id))
                {
                    Console.Error.WriteLine($"[error] Unknown or unsupported target: '{id}'");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }
            }

            var layoutLoader = new LayoutOverrideLoader();
            var purger = new GeneratedFilePurger();
            var results = new List<PurgeResult>();
            var anyFailure = false;

            foreach (var targetId in targetIds.OrderBy(id => id, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Find config entry for this target to get layoutOverridePath
                var targetConf = config?.Targets.FirstOrDefault(t =>
                    string.Equals(t.Id, targetId, StringComparison.Ordinal));

                var overridePath = targetConf?.LayoutOverridePath;

                // Resolve relative override path relative to config file directory
                if (configPath is not null && overridePath is not null && !Path.IsPathRooted(overridePath))
                {
                    var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
                    overridePath = Path.GetFullPath(Path.Combine(configDir, overridePath));
                }

                TargetLayoutDefinition layout;
                try
                {
                    layout = await layoutLoader.LoadAsync(targetId, overridePath, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[error] Failed to load layout for target '{targetId}': {ex.Message}");
                    results.Add(new PurgeResult
                    {
                        TargetId = targetId,
                        Success = false,
                        SafetyFailureReason = $"Layout load failed: {ex.Message}",
                    });
                    anyFailure = true;
                    continue;
                }

                if (layout.Purge is null)
                {
                    if (!quiet)
                        Console.Error.WriteLine($"[info] No purge policy configured for target '{targetId}'. Skipping.");
                    results.Add(new PurgeResult
                    {
                        TargetId = targetId,
                        Success = true,
                        NoOpReason = "No purge policy in layout definition.",
                    });
                    continue;
                }

                // Build context variables for root template resolution
                var context = BuildContext(resolvedGlobal, resolvedProject, targetConf?.OutputPath);
                var resolvedPolicy = GeneratedFilePurger.ResolvePolicy(layout.Purge, context);

                var result = purger.Purge(targetId, resolvedPolicy, dryRun);
                results.Add(result);

                if (!result.Success)
                    anyFailure = true;
            }

            // Report results
            if (!quiet)
            {
                foreach (var result in results.OrderBy(r => r.TargetId, StringComparer.Ordinal))
                {
                    if (result.NoOpReason is not null)
                    {
                        Console.Error.WriteLine($"[info] {result.TargetId}: no-op — {result.NoOpReason}");
                        continue;
                    }

                    if (!result.Success)
                    {
                        Console.Error.WriteLine($"[error] {result.TargetId}: purge failed — {result.SafetyFailureReason}");
                        continue;
                    }

                    foreach (var path in result.RemovedFiles)
                        Console.Error.WriteLine(dryRun
                            ? $"[dry-run] {result.TargetId}: would remove {path}"
                            : $"[purge] {result.TargetId}: removed {path}");

                    foreach (var skipped in result.SkippedFiles)
                        Console.Error.WriteLine($"[skip] {result.TargetId}: skipped {skipped.Path} ({skipped.Reason})");

                    var removedCount = result.RemovedFiles.Count;
                    var skippedCount = result.SkippedFiles.Count(s => s.Reason != SkippedPurgeReason.DryRun);
                    var dryRunCount = result.SkippedFiles.Count(s => s.Reason == SkippedPurgeReason.DryRun);

                    if (dryRun)
                        Console.Error.WriteLine($"[info] {result.TargetId}: dry-run complete. Would remove {dryRunCount} file(s).");
                    else
                        Console.Error.WriteLine($"[info] {result.TargetId}: purge complete. Removed {removedCount} file(s), skipped {skippedCount}.");
                }
            }

            if (anyFailure)
                return Composition.ExitCodeMapper.GenerationError;

            return Composition.ExitCodeMapper.Success;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("[error] Purge operation was cancelled.");
            return Composition.ExitCodeMapper.GenerationError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] {ex.Message}");
            return Composition.ExitCodeMapper.ConfigurationError;
        }
    }

    private static IReadOnlyDictionary<string, string> BuildContext(
        string? globalRoot,
        string? projectRoot,
        string? targetRoot)
    {
        var ctx = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (globalRoot is not null) ctx["globalRoot"] = globalRoot;
        if (projectRoot is not null) ctx["projectRoot"] = projectRoot;
        if (targetRoot is not null) ctx["targetRoot"] = targetRoot;
        return ctx;
    }
}
