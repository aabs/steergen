using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Merge;
using Steergen.Core.Validation;

namespace Steergen.Core.Generation;

/// <summary>
/// The result of a generation pipeline run, including optional deterministic manifest
/// for CI pipeline consumption.
/// </summary>
public sealed record GenerationResult(
    bool Success,
    IReadOnlyList<Diagnostic> Diagnostics,
    int TargetsExecuted,
    DeterministicOutputManifest? Manifest = null,
    IReadOnlyDictionary<string, WritePlan>? WritePlans = null,
    IReadOnlyDictionary<string, IReadOnlyList<RouteResolutionResult>>? RouteResolutions = null)
{
    /// <summary>
    /// Formats diagnostics as CI-parseable lines suitable for stderr.
    /// Errors use the format <c>error: [CODE] message</c> understood by most CI annotation parsers.
    /// </summary>
    public IReadOnlyList<string> FormatCiReport()
    {
        var lines = new List<string>();
        foreach (var d in Diagnostics)
        {
            var severity = d.Severity switch
            {
                DiagnosticSeverity.Error => "error",
                DiagnosticSeverity.Warning => "warning",
                _ => "info",
            };
            var location = d.Location is not null ? $"{d.Location.FilePath}: " : string.Empty;
            lines.Add($"{location}{severity}: [{d.Code}] {d.Message}");
        }
        return lines;
    }
}

public sealed class GenerationPipeline
{
    private readonly SteeringResolver _resolver = new();
    private readonly SteeringValidator _validator = new();
    private readonly LayoutOverrideLoader _layoutLoader = new();
    private readonly RoutePlanner _routePlanner = new();
    private readonly WritePlanBuilder _writePlanBuilder = new();

    /// <param name="manifestOutputPath">
    /// When set, a <see cref="DeterministicOutputManifest"/> is written to this directory
    /// after generation completes. The manifest captures SHA-256 hashes of all generated
    /// files and is included in the returned <see cref="GenerationResult"/>.
    /// </param>
    /// <param name="globalRoot">Optional resolved global root path for context variable substitution in layout paths.</param>
    /// <param name="projectRoot">Optional resolved project root path for context variable substitution in layout paths.</param>
    public async Task<GenerationResult> RunAsync(
        IEnumerable<SteeringDocument> globalDocuments,
        IEnumerable<SteeringDocument> projectDocuments,
        IEnumerable<string> activeProfiles,
        IReadOnlyList<Targets.ITargetComponent> targets,
        IReadOnlyList<TargetConfiguration> targetConfigs,
        CancellationToken cancellationToken = default,
        string? manifestOutputPath = null,
        string? globalRoot = null,
        string? projectRoot = null)
    {
        var allDiagnostics = new List<Diagnostic>();
        var globalList = globalDocuments.ToList();
        var projectList = projectDocuments.ToList();

        allDiagnostics.AddRange(_validator.ValidateCorpus(globalList.Concat(projectList)));

        if (allDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            var errorMessages = allDiagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"[{d.Code}] {d.Message}")
                .ToList();
            var failureManifest = DeterministicOutputManifest.Failure(errorMessages);
            if (manifestOutputPath is not null)
                await failureManifest.WriteAsync(manifestOutputPath, cancellationToken);
            return new GenerationResult(false, allDiagnostics, 0, failureManifest);
        }

        var model = _resolver.Resolve(globalList, projectList, activeProfiles);

        var configMap = targetConfigs
            .Where(t => t.Id is not null)
            .ToDictionary(t => t.Id!, StringComparer.Ordinal);

        // Build write plans for each enabled target using the layout engine.
        var writePlans = new Dictionary<string, WritePlan>(StringComparer.Ordinal);
        var allResolutions = new Dictionary<string, IReadOnlyList<RouteResolutionResult>>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            if (!configMap.TryGetValue(target.TargetId, out var config) || !config.Enabled)
                continue;

            if (!Targets.TargetRegistry.HasDefaultLayout(target.TargetId))
                continue;

            try
            {
                var layout = await _layoutLoader.LoadAsync(
                    target.TargetId,
                    config.LayoutOverridePath,
                    cancellationToken);

                var provenanceSource = config.LayoutOverridePath is not null
                    ? RouteProvenance.Merged
                    : RouteProvenance.Default;
                var resolutions = _routePlanner.Plan(model.Rules, layout)
                    .Select(r => r with { Source = provenanceSource })
                    .ToList();
                allResolutions[target.TargetId] = resolutions;
                var plan = _writePlanBuilder.Build(target.TargetId, resolutions);
                var resolvedPlan = ResolveContextVariables(plan, globalRoot, projectRoot);
                writePlans[target.TargetId] = resolvedPlan;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                allDiagnostics.Add(new Diagnostic(
                    Code: "LAYOUT-001",
                    Message: $"Failed to build write plan for target '{target.TargetId}': {ex.Message}",
                    Severity: DiagnosticSeverity.Warning));
            }
        }

        int targetsExecuted = 0;
        foreach (var target in targets)
        {
            if (!configMap.TryGetValue(target.TargetId, out var config) || !config.Enabled)
                continue;

            if (writePlans.TryGetValue(target.TargetId, out var writePlan))
                await target.GenerateWithPlanAsync(model, config, writePlan, cancellationToken);
            else
                await target.GenerateAsync(model, config, cancellationToken);

            targetsExecuted++;
        }

        DeterministicOutputManifest? manifest = null;
        if (manifestOutputPath is not null)
        {
            manifest = await DeterministicOutputManifest.FromDirectoryAsync(
                manifestOutputPath,
                success: true,
                cancellationToken: cancellationToken);
            await manifest.WriteAsync(manifestOutputPath, cancellationToken);
        }

        return new GenerationResult(
            true,
            allDiagnostics,
            targetsExecuted,
            manifest,
            writePlans.Count > 0 ? writePlans : null,
            allResolutions.Count > 0 ? allResolutions : null);
    }

    // ── Context variable resolution ─────────────────────────────────────────────

    private static WritePlan ResolveContextVariables(
        WritePlan plan,
        string? globalRoot,
        string? projectRoot)
    {
        if (globalRoot is null && projectRoot is null)
            return plan;

        var resolvedFiles = plan.Files
            .Select(f => f with { Path = ResolveContextVarsInPath(f.Path, globalRoot, projectRoot) })
            .ToList();

        return plan with { Files = resolvedFiles };
    }

    private static string ResolveContextVarsInPath(
        string path,
        string? globalRoot,
        string? projectRoot)
    {
        if (globalRoot is not null)
            path = path.Replace("${globalRoot}", globalRoot, StringComparison.OrdinalIgnoreCase);
        if (projectRoot is not null)
            path = path.Replace("${projectRoot}", projectRoot, StringComparison.OrdinalIgnoreCase);
        return path;
    }
}
