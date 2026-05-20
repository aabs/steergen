using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Validation;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Steergen.Core.Configuration;

public sealed class SteergenConfigLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public async Task<SteeringConfiguration> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        var config = Deserializer.Deserialize<SteeringConfigurationYaml>(content);
        return MapToModel(config);
    }

    /// <summary>
    /// Checks the raw YAML for the deprecated <c>globalRoot</c> field.
    /// Returns a CFG001 diagnostic error if the field is present.
    /// </summary>
    public async Task<Diagnostic?> CheckForDeprecatedFieldsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        var config = Deserializer.Deserialize<SteeringConfigurationYaml>(content);

        if (config.GlobalRoot is not null)
        {
            return new Diagnostic(
                "CFG001",
                "The 'globalRoot' configuration field has been removed. Use rules packs with 'scope: global' instead. " +
                "See migration guide: https://github.com/aabs/steergen/docs/migration/globalroot-removal.md",
                DiagnosticSeverity.Error);
        }

        return null;
    }

    private static SteeringConfiguration MapToModel(SteeringConfigurationYaml yaml)
    {
        return new SteeringConfiguration
        {
            ProjectRoot = yaml.ProjectRoot,
            GenerationRoot = yaml.GenerationRoot,
            ActiveProfiles = yaml.ActiveProfiles ?? [],
            Targets = (yaml.Targets ?? [])
                .Select(t => new TargetConfiguration
                {
                    Id = t.Id,
                    Enabled = t.Enabled,
                    OutputPath = t.OutputPath,
                    LayoutOverridePath = t.LayoutOverridePath,
                    FormatOptions = t.FormatOptions ?? [],
                    RequiredMetadata = t.RequiredMetadata ?? [],
                }).ToList(),
            RegisteredTargets = yaml.RegisteredTargets ?? [],
            TemplatePackVersion = yaml.TemplatePackVersion,
            TemplatePack = yaml.TemplatePack is not null
                ? new TemplatePackConfig
                {
                    Source = yaml.TemplatePack.Source,
                    Ref = yaml.TemplatePack.Ref,
                    LocalPath = yaml.TemplatePack.LocalPath,
                }
                : null,
            RulesPacks = (yaml.RulesPacks ?? [])
                .Select(r => new RulesPackEntry
                {
                    Source = r.Source ?? string.Empty,
                    Ref = r.Ref,
                    Path = r.Path,
                    Scope = r.Scope,
                }).ToList(),
        };
    }

    internal sealed class SteeringConfigurationYaml
    {
        public string? GlobalRoot { get; set; }
        public string? ProjectRoot { get; set; }
        public string? GenerationRoot { get; set; }
        public List<string>? ActiveProfiles { get; set; }
        public List<TargetConfigurationYaml>? Targets { get; set; }
        public List<string>? RegisteredTargets { get; set; }
        public string? TemplatePackVersion { get; set; }
        public TemplatePackConfigYaml? TemplatePack { get; set; }
        public List<RulesPackEntryYaml>? RulesPacks { get; set; }
    }

    internal sealed class TargetConfigurationYaml
    {
        public string? Id { get; set; }
        public bool Enabled { get; set; } = true;
        public string? OutputPath { get; set; }
        public string? LayoutOverridePath { get; set; }
        public Dictionary<string, string>? FormatOptions { get; set; }
        public List<string>? RequiredMetadata { get; set; }
    }

    internal sealed class TemplatePackConfigYaml
    {
        public string? Source { get; set; }
        public string? Ref { get; set; }
        public string? LocalPath { get; set; }
    }

    internal sealed class RulesPackEntryYaml
    {
        public string? Source { get; set; }
        public string? Ref { get; set; }
        public string? Path { get; set; }
        public PackScope? Scope { get; set; }
    }
}
