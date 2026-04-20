using Steergen.Core.Model;
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

    private static SteeringConfiguration MapToModel(SteeringConfigurationYaml yaml)
    {
        return new SteeringConfiguration
        {
            GlobalRoot = yaml.GlobalRoot,
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
}
