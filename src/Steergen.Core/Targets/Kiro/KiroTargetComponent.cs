using Scriban;
using Steergen.Core.Generation;
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
            var proseRules = ToProseModels(activeRules);
            var kiroModel = new KiroDocumentModel
            {
                Description = description,
                Inclusion = inclusion,
                FileMatchPattern = fileMatchPattern,
                Rules = proseRules,
                Sections = BuildSections(proseRules),
            };

            var rendered = await RenderDocumentAsync(kiroModel, cancellationToken);

            var resolvedPath = PlannedOutputPathResolver.Resolve(file.Path, outputPath, writePlan.GlobalRoot, writePlan.ProjectRoot);
            var outputDir = Path.GetDirectoryName(resolvedPath)!;
            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(resolvedPath, rendered, cancellationToken);
        }
    }

    public async Task<string> RenderDocumentAsync(
        KiroDocumentModel model,
        CancellationToken cancellationToken = default)
    {
        var templateText = _templateProvider.GetTemplate("kiro", "document");
        var template = Template.Parse(templateText);
        return await template.RenderAsync(EnsureSections(model));
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
            Id = r.Id,
            Category = r.Category,
            Deprecated = r.Deprecated,
            Supersedes = r.Supersedes,
            PrimaryText = CompactMarkdownFormatter.FormatRuleText(r.PrimaryText, r.ExplanatoryText),
        }).ToList();

    private static KiroDocumentModel EnsureSections(KiroDocumentModel model)
    {
        if (model.Sections is { Count: > 0 })
        {
            return model;
        }

        return model with { Sections = BuildSections(model.Rules ?? []) };
    }

    private static IReadOnlyList<KiroRuleSectionModel> BuildSections(IReadOnlyList<KiroRuleProseModel> rules) =>
        rules
            .GroupBy(rule => CompactMarkdownFormatter.FormatSectionHeading(rule.Category), StringComparer.Ordinal)
            .Select(group => new KiroRuleSectionModel
            {
                Heading = group.Key,
                Rules = group.ToList(),
            })
            .ToList();
}
