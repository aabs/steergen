using Steergen.Core.Model;
using Steergen.Core.Parsing;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Fixtures;

namespace Steergen.Core.Generation;

/// <summary>
/// High-level service for the fixture target. Used in tests to verify the additive extension seam.
/// </summary>
public sealed class FixtureGenerationService
{
    private readonly GenerationPipeline _pipeline = new();

    public async Task<GenerationResult> GenerateAsync(
        string globalRoot,
        string projectRoot,
        IReadOnlyList<string> activeProfiles,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var globalDocs = LoadDocumentsFromDirectory(globalRoot);
        var projectDocs = LoadDocumentsFromDirectory(projectRoot);

        var targetComponent = new FixtureTargetComponent();
        var targetConfig = new TargetConfiguration
        {
            Id = "fixture",
            Enabled = true,
            OutputPath = outputPath,
        };

        return await _pipeline.RunAsync(
            globalDocs,
            projectDocs,
            activeProfiles,
            [targetComponent],
            [targetConfig],
            cancellationToken);
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
