using Scriban;
using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.Targets.Agents;

public sealed class KiroAgentTargetComponent : ITargetComponent
{
    private static readonly TargetDescriptor KiroAgentDescriptor =
        new("kiro-agent", "Kiro Agent", "Generates Kiro AI agent instruction files (one per source document) from the steering model.");

    private readonly ITemplateProvider _templateProvider;

    public KiroAgentTargetComponent(ITemplateProvider templateProvider)
    {
        _templateProvider = templateProvider;
    }

    public string TargetId => "kiro-agent";
    public TargetDescriptor Descriptor => KiroAgentDescriptor;

    public async Task GenerateAsync(
        ResolvedSteeringModel model,
        TargetConfiguration config,
        CancellationToken cancellationToken)
    {
        var outputPath = config.OutputPath
            ?? throw new InvalidOperationException("Kiro agent target requires OutputPath to be set.");

        foreach (var key in config.RequiredMetadata)
        {
            if (!config.FormatOptions.ContainsKey(key))
                throw new TargetGenerationException(key);
        }

        Directory.CreateDirectory(outputPath);

        foreach (var doc in model.Documents)
        {
            var activeRules = FilterRules(doc.Rules, model.ActiveProfiles);
            if (activeRules.Count == 0)
                continue;

            var description = config.FormatOptions.TryGetValue("description", out var desc)
                ? desc
                : (doc.Title ?? doc.Id ?? Path.GetFileNameWithoutExtension(doc.SourcePath ?? "steering"));

            var documentModel = new KiroAgentDocumentModel
            {
                Name = doc.Title ?? doc.Id,
                Description = description,
                Rules = ToProseModels(activeRules),
            };

            var rendered = await RenderDocumentAsync(documentModel, cancellationToken);

            var fileName = DeriveFileName(doc);
            var filePath = Path.Combine(outputPath, fileName);
            await File.WriteAllTextAsync(filePath, rendered, cancellationToken);
        }
    }

    public async Task GenerateWithPlanAsync(
        ResolvedSteeringModel model,
        TargetConfiguration config,
        WritePlan writePlan,
        CancellationToken cancellationToken)
    {
        var outputPath = config.OutputPath
            ?? throw new InvalidOperationException("Kiro agent target requires OutputPath to be set.");

        foreach (var key in config.RequiredMetadata)
        {
            if (!config.FormatOptions.ContainsKey(key))
                throw new TargetGenerationException(key);
        }

        var ruleIndex = model.Rules.ToDictionary(r => r.Id ?? "", StringComparer.Ordinal);

        foreach (var file in writePlan.Files)
        {
            var rules = file.AppendUnits
                .Select(u => ruleIndex.TryGetValue(u.RuleId, out var r) ? r : null)
                .Where(r => r is not null)
                .Cast<SteeringRule>()
                .ToList();

            var activeRules = FilterRules(rules, model.ActiveProfiles);
            if (activeRules.Count == 0) continue;

            var stem = activeRules[0].InputFileStem ?? Path.GetFileNameWithoutExtension(file.Path);
            var description = config.FormatOptions.TryGetValue("description", out var desc)
                ? desc
                : stem;

            var documentModel = new KiroAgentDocumentModel
            {
                Name = stem,
                Description = description,
                Rules = ToProseModels(activeRules),
            };

            var rendered = await RenderDocumentAsync(documentModel, cancellationToken);

            var resolvedPath = ResolveOutputPath(file.Path, outputPath, writePlan.GlobalRoot, writePlan.ProjectRoot);
            var outputDir = Path.GetDirectoryName(resolvedPath)!;
            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(resolvedPath, rendered, cancellationToken);
        }
    }

    private static string ResolveOutputPath(string planPath, string outputPath, string? globalRoot, string? projectRoot)
    {
        if (Path.IsPathRooted(planPath))
        {
            foreach (var root in new[] { globalRoot, projectRoot }
                         .Where(r => r is not null)
                         .Select(r => r!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
            {
                if (planPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return Path.Combine(outputPath, planPath[(root.Length + 1)..]);
            }
            return Path.Combine(outputPath, Path.GetFileName(planPath));
        }
        return Path.Combine(outputPath, planPath);
    }

    public async Task<string> RenderDocumentAsync(
        KiroAgentDocumentModel model,
        CancellationToken cancellationToken = default)
    {
        var templateText = _templateProvider.GetTemplate("agents", "kiro.agent");
        var template = Template.Parse(templateText);
        return await template.RenderAsync(model);
    }

    private static IReadOnlyList<SteeringRule> FilterRules(
        IReadOnlyList<SteeringRule> rules,
        IReadOnlyList<string> activeProfiles)
    {
        return rules
            .Where(r => !r.Deprecated)
            .Where(r => activeProfiles.Count == 0
                || r.Profile is null
                || activeProfiles.Contains(r.Profile, StringComparer.Ordinal))
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<AgentRuleProseModel> ToProseModels(IReadOnlyList<SteeringRule> rules) =>
        rules.Select(r => new AgentRuleProseModel
        {
            PrimaryText = r.PrimaryText ?? "",
            ExplanatoryText = r.ExplanatoryText,
        }).ToList();

    private static string DeriveFileName(SteeringDocument doc)
    {
        if (doc.SourcePath is not null)
        {
            var baseName = Path.GetFileNameWithoutExtension(doc.SourcePath);
            return $"{baseName}.md";
        }

        return $"{doc.Id ?? "steering"}.md";
    }
}
