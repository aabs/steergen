using Steergen.Core.Model;
using Steergen.Core.Parsing;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Kiro;

namespace Steergen.Core.Generation;

/// <summary>
/// High-level service used by the <c>run</c> command to produce Kiro steering artefacts.
/// </summary>
public sealed class KiroGenerationService
{
    private readonly GenerationPipeline _pipeline = new();

    public async Task<GenerationResult> GenerateAsync(
        string globalRoot,
        string projectRoot,
        IReadOnlyList<string> activeProfiles,
        string outputPath,
        ITemplateProvider templateProvider,
        IReadOnlyDictionary<string, string>? formatOptions = null,
        CancellationToken cancellationToken = default)
    {
        var globalDocs = LoadDocumentsFromDirectory(globalRoot);
        var projectDocs = LoadDocumentsFromDirectory(projectRoot);

        var targetComponent = new KiroTargetComponent(templateProvider);
        var targetConfig = new TargetConfiguration
        {
            Id = "kiro",
            Enabled = true,
            OutputPath = outputPath,
            FormatOptions = formatOptions is null
                ? []
                : new Dictionary<string, string>(formatOptions),
        };

        return await _pipeline.RunAsync(
            globalDocs,
            projectDocs,
            activeProfiles,
            [targetComponent],
            [targetConfig],
            cancellationToken,
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
