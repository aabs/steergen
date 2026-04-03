using Steergen.Core.Model;
using Steergen.Core.Merge;
using Steergen.Core.Validation;

namespace Steergen.Core.Generation;

public sealed record GenerationResult(
    bool Success,
    IReadOnlyList<Diagnostic> Diagnostics,
    int FilesWritten);

public sealed class GenerationPipeline
{
    private readonly SteeringResolver _resolver = new();
    private readonly SteeringValidator _validator = new();

    public async Task<GenerationResult> RunAsync(
        IEnumerable<SteeringDocument> globalDocuments,
        IEnumerable<SteeringDocument> projectDocuments,
        IEnumerable<string> activeProfiles,
        IReadOnlyList<Targets.ITargetComponent> targets,
        IReadOnlyList<TargetConfiguration> targetConfigs,
        CancellationToken cancellationToken = default)
    {
        var allDiagnostics = new List<Diagnostic>();
        var globalList = globalDocuments.ToList();
        var projectList = projectDocuments.ToList();

        foreach (var doc in globalList.Concat(projectList))
        {
            allDiagnostics.AddRange(_validator.Validate(doc));
        }

        if (allDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            return new GenerationResult(false, allDiagnostics, 0);

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

        return new GenerationResult(true, allDiagnostics, filesWritten);
    }
}
