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

            var resolvedPath = ResolveOutputPath(file.Path, outputPath, writePlan.GlobalRoot, writePlan.ProjectRoot);
            var outputDir = Path.GetDirectoryName(resolvedPath)!;
            Directory.CreateDirectory(outputDir);

            var fileName = Path.GetFileNameWithoutExtension(resolvedPath);
            string rendered;

            if (string.Equals(fileName, "constitution", StringComparison.OrdinalIgnoreCase)
                || rules.All(r => r.Domain == "core"))
            {
                var constitutionModel = new SpeckitConstitutionModel
                {
                    Rules = ToRuleModels(rules),
                };
                rendered = await RenderConstitutionAsync(constitutionModel, cancellationToken);
            }
            else
            {
                var domain = rules[0].Domain ?? fileName;
                var moduleModel = new SpeckitModuleModel
                {
                    Domain = domain,
                    Rules = ToRuleModels(rules),
                };
                rendered = await RenderModuleAsync(moduleModel, cancellationToken);
            }

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
