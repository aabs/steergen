using Steergen.Core.Model;
using Steergen.Core.Parsing;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Agents;

namespace Steergen.Core.Generation;

public sealed class KiroAgentGenerationService
{
    private readonly GenerationPipeline _pipeline = new();

    public async Task<GenerationResult> RunAsync(
        string globalRoot,
        string projectRoot,
        IReadOnlyList<string> activeProfiles,
        string outputPath,
        ITemplateProvider templateProvider,
        IReadOnlyDictionary<string, string>? formatOptions = null,
        IReadOnlyList<string>? requiredMetadata = null,
        CancellationToken cancellationToken = default)
    {
        var globalDocs = LoadDocumentsFromDirectory(globalRoot);
        var projectDocs = LoadDocumentsFromDirectory(projectRoot);

        var targetComponent = new KiroAgentTargetComponent(templateProvider);
        var targetConfig = new TargetConfiguration
        {
            Id = "kiro-agent",
            Enabled = true,
            OutputPath = outputPath,
            FormatOptions = formatOptions is null
                ? []
                : new Dictionary<string, string>(formatOptions),
            RequiredMetadata = requiredMetadata is null
                ? []
                : [.. requiredMetadata],
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
