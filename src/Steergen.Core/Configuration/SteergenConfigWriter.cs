using System.Security.Cryptography;
using System.Text;
using Steergen.Core.Model;
using Steergen.Core.Packs;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Steergen.Core.Configuration;

public sealed class SteergenConfigWriter
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public async Task WriteAsync(
        string filePath,
        SteeringConfiguration configuration,
        string? expectedHash = null,
        CancellationToken cancellationToken = default)
    {
        if (expectedHash is null && File.Exists(filePath))
        {
            var existingContent = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            expectedHash = ComputeHash(existingContent);
        }

        var yaml = MapToYaml(configuration);
        var serialized = Serializer.Serialize(yaml);
        var serializedBytes = Encoding.UTF8.GetBytes(serialized);

        if (expectedHash is not null && File.Exists(filePath))
        {
            var currentContent = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            var currentHash = ComputeHash(currentContent);
            if (!string.Equals(currentHash, expectedHash, StringComparison.Ordinal))
                throw new ConfigWriteConflictException(
                    $"The configuration file '{filePath}' was modified between read and write.");
        }

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllBytesAsync(filePath, serializedBytes, cancellationToken).ConfigureAwait(false);
    }

    public static string ComputeFileHash(byte[] data) => ComputeHash(data);

    private static string ComputeHash(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash);
    }

    private static SteeringConfigurationYamlOut MapToYaml(SteeringConfiguration config)
    {
        return new SteeringConfigurationYamlOut
        {
            ProjectRoot = config.ProjectRoot,
            GenerationRoot = config.GenerationRoot,
            ActiveProfiles = config.ActiveProfiles,
            Targets = config.Targets.Select(t => new TargetConfigurationYamlOut
            {
                Id = t.Id,
                Enabled = t.Enabled,
                OutputPath = t.OutputPath,
                LayoutOverridePath = t.LayoutOverridePath,
                FormatOptions = t.FormatOptions,
                RequiredMetadata = t.RequiredMetadata,
            }).ToList(),
            RegisteredTargets = config.RegisteredTargets,
            TemplatePackVersion = config.TemplatePackVersion,
            TemplatePack = config.TemplatePack is not null
                ? new TemplatePackConfigYamlOut
                {
                    Source = config.TemplatePack.Source,
                    Ref = config.TemplatePack.Ref,
                    EntryKey = config.TemplatePack.EntryKey,
                    Pin = config.TemplatePack.Pin is null
                        ? null
                        : new PackPinYamlOut
                        {
                            Tag = config.TemplatePack.Pin.Tag,
                            CommitSha = config.TemplatePack.Pin.CommitSha,
                        },
                    LocalPath = config.TemplatePack.LocalPath,
                }
                : null,
            RulesPacks = config.RulesPacks.Count > 0
                ? config.RulesPacks.Select(r => new RulesPackEntryYamlOut
                {
                    Source = r.Source,
                    Ref = r.Ref,
                    Path = r.Path,
                    Pin = r.Pin is null
                        ? null
                        : new PackPinYamlOut
                        {
                            Tag = r.Pin.Tag,
                            CommitSha = r.Pin.CommitSha,
                        },
                    Scope = r.Scope,
                }).ToList()
                : null,
        };
    }

    private sealed class SteeringConfigurationYamlOut
    {
        public string? ProjectRoot { get; set; }
        public string? GenerationRoot { get; set; }
        public IReadOnlyList<string>? ActiveProfiles { get; set; }
        public List<TargetConfigurationYamlOut>? Targets { get; set; }
        public IReadOnlyList<string>? RegisteredTargets { get; set; }
        public string? TemplatePackVersion { get; set; }
        public TemplatePackConfigYamlOut? TemplatePack { get; set; }
        public List<RulesPackEntryYamlOut>? RulesPacks { get; set; }
    }

    private sealed class TargetConfigurationYamlOut
    {
        public string? Id { get; set; }
        public bool Enabled { get; set; }
        public string? OutputPath { get; set; }
        public string? LayoutOverridePath { get; set; }
        public Dictionary<string, string>? FormatOptions { get; set; }
        public List<string>? RequiredMetadata { get; set; }
    }

    private sealed class TemplatePackConfigYamlOut
    {
        public string? Source { get; set; }
        public string? Ref { get; set; }
        public string? EntryKey { get; set; }
        public PackPinYamlOut? Pin { get; set; }
        public string? LocalPath { get; set; }
    }

    private sealed class RulesPackEntryYamlOut
    {
        public string? Source { get; set; }
        public string? Ref { get; set; }
        public string? Path { get; set; }
        public PackPinYamlOut? Pin { get; set; }
        public PackScope? Scope { get; set; }
    }

    private sealed class PackPinYamlOut
    {
        public string? Tag { get; set; }
        public string? CommitSha { get; set; }
    }
}
