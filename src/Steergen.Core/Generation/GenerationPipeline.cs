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
    int FilesWritten,
    DeterministicOutputManifest? Manifest = null)
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

    /// <param name="manifestOutputPath">
    /// When set, a <see cref="DeterministicOutputManifest"/> is written to this directory
    /// after generation completes. The manifest captures SHA-256 hashes of all generated
    /// files and is included in the returned <see cref="GenerationResult"/>.
    /// </param>
    public async Task<GenerationResult> RunAsync(
        IEnumerable<SteeringDocument> globalDocuments,
        IEnumerable<SteeringDocument> projectDocuments,
        IEnumerable<string> activeProfiles,
        IReadOnlyList<Targets.ITargetComponent> targets,
        IReadOnlyList<TargetConfiguration> targetConfigs,
        CancellationToken cancellationToken = default,
        string? manifestOutputPath = null)
    {
        var allDiagnostics = new List<Diagnostic>();
        var globalList = globalDocuments.ToList();
        var projectList = projectDocuments.ToList();

        foreach (var doc in globalList.Concat(projectList))
        {
            allDiagnostics.AddRange(_validator.Validate(doc));
        }

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

        int filesWritten = 0;
        foreach (var target in targets)
        {
            if (!configMap.TryGetValue(target.TargetId, out var config) || !config.Enabled)
                continue;

            await target.GenerateAsync(model, config, cancellationToken);
            filesWritten++;
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

        return new GenerationResult(true, allDiagnostics, filesWritten, manifest);
    }
}
