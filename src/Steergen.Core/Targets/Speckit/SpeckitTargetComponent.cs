using Scriban;
using Steergen.Core.Generation;
using Steergen.Core.Model;
using Steergen.Core.Targets.Speckit;

namespace Steergen.Core.Targets.Speckit;

public sealed class SpeckitTargetComponent : ITargetComponent
{
    private static readonly TargetDescriptor SpeckitDescriptor =
        new("speckit", "Speckit", "Generates Speckit Markdown artefacts: core constitution.md and domain modules.");

    private readonly ITemplateProvider _templateProvider;
    private readonly Generation.CoreGuidancePartitioner _partitioner = new();

    public SpeckitTargetComponent(ITemplateProvider templateProvider)
    {
        _templateProvider = templateProvider;
    }

    public string TargetId => "speckit";
    public TargetDescriptor Descriptor => SpeckitDescriptor;

    public async Task GenerateWithPlanAsync(
        ResolvedSteeringModel model,
        TargetConfiguration config,
        WritePlan writePlan,
        CancellationToken cancellationToken)
    {
        var outputPath = config.OutputPath
            ?? throw new InvalidOperationException("Speckit target requires OutputPath to be set.");

        var ruleIndex = model.Rules.ToDictionary(r => r.Id ?? "", StringComparer.Ordinal);

        foreach (var file in writePlan.Files)
        {

            var rules = file.AppendUnits
                .Select(u => ruleIndex.TryGetValue(u.RuleId, out var r) ? r : null)
                .Where(r => r is not null)
                .Cast<SteeringRule>()
                .ToList();

            if (rules.Count == 0) continue;

            var resolvedPath = PlannedOutputPathResolver.Resolve(file.Path, outputPath, writePlan.GlobalRoot, writePlan.ProjectRoot);
            var outputDir = Path.GetDirectoryName(resolvedPath)!;
            Directory.CreateDirectory(outputDir);

            var fileName = Path.GetFileNameWithoutExtension(resolvedPath);
            string rendered;
            var ruleModels = ToRuleModels(rules);

            if (string.Equals(fileName, "constitution", StringComparison.OrdinalIgnoreCase)
                || rules.All(r => r.Domain == "core"))
            {
                var constitutionModel = new SpeckitConstitutionModel
                {
                    Rules = ruleModels,
                    Sections = BuildSections(ruleModels),
                };
                rendered = await RenderConstitutionAsync(constitutionModel, cancellationToken);
            }
            else
            {
                var domain = rules[0].Domain ?? fileName;
                var moduleModel = new SpeckitModuleModel
                {
                    Domain = domain,
                    Rules = ruleModels,
                    Sections = BuildSections(ruleModels),
                };
                rendered = await RenderModuleAsync(moduleModel, cancellationToken);
            }

            await File.WriteAllTextAsync(resolvedPath, rendered, cancellationToken);
        }
    }

    public async Task<string> RenderConstitutionAsync(
        SpeckitConstitutionModel model,
        CancellationToken cancellationToken = default)
    {
        var templateText = _templateProvider.GetTemplate("speckit", "constitution");
        var template = Template.Parse(templateText);
        return await template.RenderAsync(EnsureSections(model));
    }

    public async Task<string> RenderModuleAsync(
        SpeckitModuleModel model,
        CancellationToken cancellationToken = default)
    {
        var templateText = _templateProvider.GetTemplate("speckit", "module");
        var template = Template.Parse(templateText);
        return await template.RenderAsync(EnsureSections(model));
    }

    private static IReadOnlyList<SpeckitRuleModel> ToRuleModels(IReadOnlyList<SteeringRule> rules) =>
        rules.Select(r => new SpeckitRuleModel
        {
            Id = r.Id ?? "",
            Severity = r.Severity,
            Category = r.Category,
            Deprecated = r.Deprecated,
            Supersedes = r.Supersedes,
            PrimaryText = CompactMarkdownFormatter.FormatRuleText(r.PrimaryText, r.ExplanatoryText),
        }).ToList();

    private static SpeckitConstitutionModel EnsureSections(SpeckitConstitutionModel model)
    {
        if (model.Sections is { Count: > 0 })
        {
            return model;
        }

        return model with { Sections = BuildSections(model.Rules ?? []) };
    }

    private static SpeckitModuleModel EnsureSections(SpeckitModuleModel model)
    {
        if (model.Sections is { Count: > 0 })
        {
            return model;
        }

        return model with { Sections = BuildSections(model.Rules ?? []) };
    }

    private static IReadOnlyList<SpeckitRuleSectionModel> BuildSections(IReadOnlyList<SpeckitRuleModel> rules) =>
        rules
            .GroupBy(rule => CompactMarkdownFormatter.FormatSectionHeading(rule.Category), StringComparer.Ordinal)
            .Select(group => new SpeckitRuleSectionModel
            {
                Heading = group.Key,
                Rules = group.ToList(),
            })
            .ToList();
}
