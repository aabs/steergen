using Scriban;
using Scriban.Runtime;
using Steergen.Core.Generation;
using Steergen.Core.Model;

namespace Steergen.Core.Targets;

/// <summary>
/// Generic target component for pack-provided targets.
/// Delegates all rendering to the pack's Scriban templates and uses
/// the pack's default layout YAML for routing.
/// </summary>
public sealed class PackTargetComponent : ITargetComponent
{
    private readonly string _targetId;
    private readonly ITemplateProvider _templateProvider;
    private readonly string _defaultLayoutPath;
    private readonly TargetDescriptor _descriptor;

    public PackTargetComponent(
        string targetId,
        ITemplateProvider templateProvider,
        string defaultLayoutPath,
        string? packName = null,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetId);
        ArgumentNullException.ThrowIfNull(templateProvider);
        ArgumentException.ThrowIfNullOrEmpty(defaultLayoutPath);

        _targetId = targetId;
        _templateProvider = templateProvider;
        _defaultLayoutPath = defaultLayoutPath;
        _descriptor = new TargetDescriptor(
            targetId,
            targetId,
            description ?? $"Pack-provided target '{targetId}'.")
        {
            Origin = TargetOrigin.PackProvided,
            PackName = packName
        };
    }

    public string TargetId => _targetId;
    public TargetDescriptor Descriptor => _descriptor;

    /// <summary>
    /// The filesystem path to the pack's default layout YAML.
    /// Used by the generation pipeline to load the layout for routing.
    /// </summary>
    public string DefaultLayoutPath => _defaultLayoutPath;

    public async Task GenerateWithPlanAsync(
        ResolvedSteeringModel model,
        TargetConfiguration config,
        WritePlan writePlan,
        CancellationToken cancellationToken)
    {
        var outputPath = config.OutputPath
            ?? throw new InvalidOperationException(
                $"Pack target '{_targetId}' requires OutputPath to be set.");

        var ruleIndex = model.Rules.ToDictionary(r => r.Id ?? "", StringComparer.Ordinal);

        foreach (var file in writePlan.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rules = file.AppendUnits
                .Select(u => ruleIndex.TryGetValue(u.RuleId, out var r) ? r : null)
                .Where(r => r is not null)
                .Cast<SteeringRule>()
                .ToList();

            if (rules.Count == 0) continue;

            var resolvedPath = PlannedOutputPathResolver.Resolve(
                file.Path, outputPath, writePlan.GlobalRoot, writePlan.ProjectRoot);

            var renderModel = BuildRenderModel(rules, resolvedPath, config.FormatOptions);
            var rendered = await RenderAsync(renderModel, cancellationToken);

            var outputDir = Path.GetDirectoryName(resolvedPath)!;
            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(resolvedPath, rendered, cancellationToken);
        }
    }

    /// <summary>
    /// Builds the generic render model exposed to pack Scriban templates.
    /// Exposes the same fields available to built-in targets: rules, targetId, filePath, formatOptions.
    /// </summary>
    private ScriptObject BuildRenderModel(
        IReadOnlyList<SteeringRule> rules,
        string filePath,
        Dictionary<string, string> formatOptions)
    {
        var ruleModels = rules.Select(r => new PackRuleModel
        {
            Id = r.Id ?? "",
            Category = r.Category ?? "",
            Mandatory = r.Mandatory,
            Deprecated = r.Deprecated,
            PrimaryText = r.PrimaryText ?? "",
            ExplanatoryText = r.ExplanatoryText ?? "",
            Tags = r.Tags,
            InputFileStem = r.InputFileStem ?? "",
        }).ToList();

        var scriptObject = new ScriptObject();
        scriptObject["rules"] = ruleModels;
        scriptObject["target_id"] = _targetId;
        scriptObject["file_path"] = filePath;
        scriptObject["format_options"] = formatOptions;
        return scriptObject;
    }

    private async Task<string> RenderAsync(
        ScriptObject renderModel,
        CancellationToken cancellationToken)
    {
        var templateText = _templateProvider.GetTemplate(_targetId, "document");
        var template = Template.Parse(templateText);

        if (template.HasErrors)
        {
            var errors = string.Join("; ", template.Messages.Select(m => m.Message));
            throw new InvalidOperationException(
                $"Pack template for target '{_targetId}' has Scriban syntax errors: {errors}");
        }

        var context = new TemplateContext();
        context.PushGlobal(renderModel);

        var result = await template.RenderAsync(context);
        return result;
    }
}

/// <summary>
/// Rule model exposed to pack Scriban templates.
/// Provides the same fields available to built-in target rule models.
/// </summary>
public sealed record PackRuleModel
{
    public string Id { get; init; } = "";
    public string Category { get; init; } = "";
    public bool Mandatory { get; init; }
    public bool Deprecated { get; init; }
    public string PrimaryText { get; init; } = "";
    public string ExplanatoryText { get; init; } = "";
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string InputFileStem { get; init; } = "";
}
