using Scriban;
using Steergen.Core.Model;

namespace Steergen.Core.Targets.Kiro;

public sealed class KiroTargetComponent : ITargetComponent
{
    private static readonly TargetDescriptor KiroDescriptor =
        new("kiro", "Kiro", "Generates Kiro-compatible steering Markdown files (one per source document).");

    private readonly ITemplateProvider _templateProvider;

    public KiroTargetComponent(ITemplateProvider templateProvider)
    {
        _templateProvider = templateProvider;
    }

    public string TargetId => "kiro";
    public TargetDescriptor Descriptor => KiroDescriptor;

    public async Task GenerateAsync(
        ResolvedSteeringModel model,
        TargetConfiguration config,
        CancellationToken cancellationToken)
    {
        var outputPath = config.OutputPath
            ?? throw new InvalidOperationException("Kiro target requires OutputPath to be set.");

        var options = KiroTargetOptions.FromFormatOptions(config.FormatOptions);

        Directory.CreateDirectory(outputPath);

        foreach (var doc in model.Documents)
        {
            var activeRules = FilterRules(doc.Rules, model.ActiveProfiles);
            if (activeRules.Count == 0)
                continue;

            var (inclusion, fileMatchPattern) = KiroInclusionMapper.Map(activeRules, options);

            var kiroModel = new KiroDocumentModel
            {
                Description = doc.Title ?? doc.Id ?? Path.GetFileNameWithoutExtension(doc.SourcePath ?? "steering"),
                Inclusion = inclusion,
                FileMatchPattern = fileMatchPattern,
                Rules = ToProseModels(activeRules),
            };

            var rendered = await RenderDocumentAsync(kiroModel, cancellationToken);

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
            ?? throw new InvalidOperationException("Kiro target requires OutputPath to be set.");

        var options = KiroTargetOptions.FromFormatOptions(config.FormatOptions);
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

            var (inclusion, fileMatchPattern) = KiroInclusionMapper.Map(activeRules, options);

            var description = activeRules[0].InputFileStem
                ?? Path.GetFileNameWithoutExtension(file.Path);
            var kiroModel = new KiroDocumentModel
            {
                Description = description,
                Inclusion = inclusion,
                FileMatchPattern = fileMatchPattern,
                Rules = ToProseModels(activeRules),
            };

            var rendered = await RenderDocumentAsync(kiroModel, cancellationToken);

            var resolvedPath = ResolveOutputPath(file.Path, outputPath);
            var outputDir = Path.GetDirectoryName(resolvedPath)!;
            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(resolvedPath, rendered, cancellationToken);
        }
    }

    private static string ResolveOutputPath(string planPath, string outputPath) =>
        Path.Combine(outputPath, Path.GetFileName(planPath));


    public async Task<string> RenderDocumentAsync(
        KiroDocumentModel model,
        CancellationToken cancellationToken = default)
    {
        var templateText = _templateProvider.GetTemplate("kiro", "document");
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

    private static IReadOnlyList<KiroRuleProseModel> ToProseModels(IReadOnlyList<SteeringRule> rules) =>
        rules.Select(r => new KiroRuleProseModel
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
