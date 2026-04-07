using Steergen.Core.Model;
using Steergen.Core.Parsing;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Speckit;

namespace Steergen.Core.Generation;

/// <summary>
/// High-level service used by the <c>run</c> command to produce Speckit artefacts.
/// </summary>
public sealed class SpeckitGenerationService
{
    private readonly GenerationPipeline _pipeline = new();

    public async Task<GenerationResult> GenerateAsync(
        string globalRoot,
        string projectRoot,
        IReadOnlyList<string> activeProfiles,
        string outputPath,
        ITemplateProvider templateProvider,
        bool writeManifest = false,
        CancellationToken cancellationToken = default)
    {
        var globalDocs = LoadDocumentsFromDirectory(globalRoot);
        var projectDocs = LoadDocumentsFromDirectory(projectRoot);

        var targetComponent = new SpeckitTargetComponent(templateProvider);
        var targetConfig = new TargetConfiguration
        {
            Id = "speckit",
            Enabled = true,
            OutputPath = outputPath,
        };

        return await _pipeline.RunAsync(
            globalDocs,
            projectDocs,
            activeProfiles,
            [targetComponent],
            [targetConfig],
            cancellationToken,
            manifestOutputPath: writeManifest ? outputPath : null,
            globalRoot: globalRoot,
            projectRoot: projectRoot);
    }

    private static IReadOnlyList<SteeringDocument> LoadDocumentsFromDirectory(string root)
    {
        if (!Directory.Exists(root))
            return [];

        return Directory
            .EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(path => SteeringMarkdownParser.Parse(File.ReadAllText(path), path))
            .ToList();
    }
}
