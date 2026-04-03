using Scriban;
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

    public async Task GenerateAsync(
        ResolvedSteeringModel model,
        TargetConfiguration config,
        CancellationToken cancellationToken)
    {
        var outputPath = config.OutputPath
            ?? throw new InvalidOperationException("Speckit target requires OutputPath to be set.");

        var partition = _partitioner.Partition(model.Rules);

        var constitutionModel = new SpeckitConstitutionModel
        {
            Rules = ToRuleModels(partition.CoreRules),
        };

        var rendered = await RenderConstitutionAsync(constitutionModel, cancellationToken);

        Directory.CreateDirectory(outputPath);
        var constitutionPath = Path.Combine(outputPath, "constitution.md");
        await File.WriteAllTextAsync(constitutionPath, rendered, cancellationToken);

        foreach (var (domain, rules) in partition.DomainModules)
        {
            var moduleModel = new SpeckitModuleModel
            {
                Domain = domain,
                Rules = ToRuleModels(rules),
            };
            var moduleRendered = await RenderModuleAsync(moduleModel, cancellationToken);
            var modulePath = Path.Combine(outputPath, $"{domain}.md");
            await File.WriteAllTextAsync(modulePath, moduleRendered, cancellationToken);
        }
    }

    public async Task<string> RenderConstitutionAsync(
        SpeckitConstitutionModel model,
        CancellationToken cancellationToken = default)
    {
        var templateText = _templateProvider.GetTemplate("speckit", "constitution");
        var template = Template.Parse(templateText);
        return await template.RenderAsync(model);
    }

    public async Task<string> RenderModuleAsync(
        SpeckitModuleModel model,
        CancellationToken cancellationToken = default)
    {
        var templateText = _templateProvider.GetTemplate("speckit", "module");
        var template = Template.Parse(templateText);
        return await template.RenderAsync(model);
    }

    private static IReadOnlyList<SpeckitRuleModel> ToRuleModels(IReadOnlyList<SteeringRule> rules) =>
        rules.Select(r => new SpeckitRuleModel
        {
            Id = r.Id ?? "",
            Severity = r.Severity,
            Category = r.Category,
            Deprecated = r.Deprecated,
            Supersedes = r.Supersedes,
            PrimaryText = r.PrimaryText ?? "",
            ExplanatoryText = r.ExplanatoryText,
        }).ToList();
}
