using System.Security.Cryptography;
using System.Text;
using Steergen.Core.Model;
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
            GlobalRoot = config.GlobalRoot,
            ProjectRoot = config.ProjectRoot,
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
        };
    }

    private sealed class SteeringConfigurationYamlOut
    {
        public string? GlobalRoot { get; set; }
        public string? ProjectRoot { get; set; }
        public IReadOnlyList<string>? ActiveProfiles { get; set; }
        public List<TargetConfigurationYamlOut>? Targets { get; set; }
        public IReadOnlyList<string>? RegisteredTargets { get; set; }
        public string? TemplatePackVersion { get; set; }
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
}
