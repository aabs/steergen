using System.Reflection;
using Steergen.Core.Model;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Steergen.Core.Configuration;

/// <summary>
/// Loads the built-in default layout YAML for a target and optionally deep-merges
/// a user-provided override YAML on top of it.
///
/// Deep-merge semantics:
/// - Scalar fields in the override replace the default value.
/// - Maps merge recursively (each key is merged independently).
/// - Lists in the override replace the default list entirely.
/// </summary>
public sealed class LayoutOverrideLoader
{
    private static readonly Assembly CoreAssembly = typeof(LayoutOverrideLoader).Assembly;

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithTypeConverter(new StringOrListConverter())
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Returns the embedded resource name for a target's default layout YAML.
    /// Supports built-in targets: speckit, kiro, copilot-agent, kiro-agent.
    /// </summary>
    public static string GetEmbeddedResourceName(string targetId) => targetId switch
    {
        "speckit" => "Steergen.Core.Targets.Speckit.default-layout.yaml",
        "kiro" => "Steergen.Core.Targets.Kiro.default-layout.yaml",
        "copilot-agent" => "Steergen.Core.Targets.Agents.Copilot.default-layout.yaml",
        "kiro-agent" => "Steergen.Core.Targets.Agents.Kiro.default-layout.yaml",
        _ => throw new ArgumentException($"Unknown built-in target '{targetId}'.", nameof(targetId)),
    };

    /// <summary>
    /// Loads the default layout for <paramref name="targetId"/> from the embedded resource.
    /// If <paramref name="overrideFilePath"/> is provided, deep-merges the override on top.
    /// </summary>
    public async Task<TargetLayoutDefinition> LoadAsync(
        string targetId,
        string? overrideFilePath = null,
        CancellationToken cancellationToken = default)
    {
        var defaultYaml = LoadEmbeddedYaml(targetId);
        var defaultDto = Deserializer.Deserialize<LayoutYamlDto>(defaultYaml);

        if (overrideFilePath is not null)
        {
            var overrideYaml = await File.ReadAllTextAsync(overrideFilePath, cancellationToken)
                .ConfigureAwait(false);
            var overrideDto = Deserializer.Deserialize<LayoutYamlDto>(overrideYaml);
            defaultDto = DeepMerge(defaultDto, overrideDto);
        }

        return MapToModel(targetId, defaultDto);
    }

    /// <summary>
    /// Loads the default layout for <paramref name="targetId"/> from the embedded resource only.
    /// Synchronous convenience overload.
    /// </summary>
    public TargetLayoutDefinition LoadDefault(string targetId)
    {
        var yaml = LoadEmbeddedYaml(targetId);
        var dto = Deserializer.Deserialize<LayoutYamlDto>(yaml);
        return MapToModel(targetId, dto);
    }

    private static string LoadEmbeddedYaml(string targetId)
    {
        var resourceName = GetEmbeddedResourceName(targetId);
        using var stream = CoreAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded default layout '{resourceName}' not found. " +
                $"Available: {string.Join(", ", CoreAssembly.GetManifestResourceNames())}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static TargetLayoutDefinition MapToModel(string targetId, LayoutYamlDto dto)
    {
        var roots = dto.Roots is not null
            ? new LayoutRootsDefinition
            {
                GlobalRoot = dto.Roots.GlobalRoot ?? "${globalRoot}",
                ProjectRoot = dto.Roots.ProjectRoot ?? "${projectRoot}",
                TargetRoot = dto.Roots.TargetRoot ?? "${projectRoot}",
            }
            : new LayoutRootsDefinition();

        var routes = (dto.Routes ?? [])
            .Select((r, idx) => new RouteRuleDefinition
            {
                Id = r.Id ?? $"route-{idx}",
                Scope = ParseScope(r.Scope),
                Explicit = r.Explicit,
                Anchor = ParseAnchor(r.Anchor),
                Order = r.Order,
                Match = MapMatch(r.Match),
                Destination = MapDestination(r.Destination),
            })
            .ToList();

        var fallback = dto.Fallback is not null
            ? new FallbackRuleDefinition
            {
                Mode = ParseFallbackMode(dto.Fallback.Mode),
                FileBaseName = dto.Fallback.FileBaseName ?? "other",
            }
            : new FallbackRuleDefinition();

        var purge = dto.Purge is not null
            ? new PurgePolicyDefinition
            {
                Enabled = dto.Purge.Enabled,
                Roots = dto.Purge.Roots ?? [],
                Globs = dto.Purge.Globs ?? [],
            }
            : null;

        return new TargetLayoutDefinition
        {
            TargetId = targetId,
            Version = dto.Version,
            Roots = roots,
            Routes = routes,
            Fallback = fallback,
            Purge = purge,
        };
    }

    private static RouteMatchExpression MapMatch(RouteMatchYamlDto? m)
    {
        if (m is null) return new RouteMatchExpression();
        return new RouteMatchExpression
        {
            Domain = m.Domain ?? [],
            TagsAny = m.TagsAny ?? [],
            Category = m.Category ?? [],
            Severity = m.Severity ?? [],
            Profile = m.Profile ?? [],
        };
    }

    private static DestinationTemplate MapDestination(RouteDestinationYamlDto? d)
    {
        if (d is null) return new DestinationTemplate();
        return new DestinationTemplate
        {
            Directory = d.Directory ?? "",
            FileName = d.FileName ?? "",
            Extension = d.Extension,
        };
    }

    private static RouteScope ParseScope(string? s) => s?.ToLowerInvariant() switch
    {
        "global" => RouteScope.Global,
        "project" => RouteScope.Project,
        "both" => RouteScope.Both,
        null => RouteScope.Both,
        _ => throw new InvalidOperationException($"Unknown route scope '{s}'."),
    };

    private static RouteAnchor ParseAnchor(string? a) => a?.ToLowerInvariant() switch
    {
        "core" => RouteAnchor.Core,
        "none" or null => RouteAnchor.None,
        _ => throw new InvalidOperationException($"Unknown route anchor '{a}'."),
    };

    private static FallbackMode ParseFallbackMode(string? m) =>
        m?.ToLowerInvariant() switch
        {
            "other-at-core-anchor" => FallbackMode.OtherAtCoreAnchor,
            null => FallbackMode.OtherAtCoreAnchor,
            _ => throw new InvalidOperationException($"Unknown fallback mode '{m}'."),
        };

    /// <summary>
    /// Deep-merges <paramref name="overrideDto"/> onto <paramref name="baseDto"/>.
    /// Maps merge recursively; scalars and lists in override replace base values.
    /// </summary>
    internal static LayoutYamlDto DeepMerge(LayoutYamlDto baseDto, LayoutYamlDto overrideDto)
    {
        return new LayoutYamlDto
        {
            Version = overrideDto.Version ?? baseDto.Version,
            Roots = MergeRoots(baseDto.Roots, overrideDto.Roots),
            Routes = overrideDto.Routes ?? baseDto.Routes,
            Fallback = MergeFallback(baseDto.Fallback, overrideDto.Fallback),
            Purge = MergePurge(baseDto.Purge, overrideDto.Purge),
        };
    }

    private static LayoutRootsYamlDto? MergeRoots(LayoutRootsYamlDto? b, LayoutRootsYamlDto? o)
    {
        if (o is null) return b;
        if (b is null) return o;
        return new LayoutRootsYamlDto
        {
            GlobalRoot = o.GlobalRoot ?? b.GlobalRoot,
            ProjectRoot = o.ProjectRoot ?? b.ProjectRoot,
            TargetRoot = o.TargetRoot ?? b.TargetRoot,
        };
    }

    private static FallbackYamlDto? MergeFallback(FallbackYamlDto? b, FallbackYamlDto? o)
    {
        if (o is null) return b;
        if (b is null) return o;
        return new FallbackYamlDto
        {
            Mode = o.Mode ?? b.Mode,
            FileBaseName = o.FileBaseName ?? b.FileBaseName,
        };
    }

    private static PurgeYamlDto? MergePurge(PurgeYamlDto? b, PurgeYamlDto? o)
    {
        if (o is null) return b;
        if (b is null) return o;
        return new PurgeYamlDto
        {
            Enabled = o.Enabled,
            Roots = o.Roots ?? b.Roots,
            Globs = o.Globs ?? b.Globs,
        };
    }

    // ── YAML DTOs ────────────────────────────────────────────────────────────

    internal sealed class LayoutYamlDto
    {
        public string? Version { get; set; }
        public LayoutRootsYamlDto? Roots { get; set; }
        public List<RouteRuleYamlDto>? Routes { get; set; }
        public FallbackYamlDto? Fallback { get; set; }
        public PurgeYamlDto? Purge { get; set; }
    }

    internal sealed class LayoutRootsYamlDto
    {
        public string? GlobalRoot { get; set; }
        public string? ProjectRoot { get; set; }
        public string? TargetRoot { get; set; }
    }

    internal sealed class RouteRuleYamlDto
    {
        public string? Id { get; set; }
        public string? Scope { get; set; }
        public bool Explicit { get; set; }
        public string? Anchor { get; set; }
        public int Order { get; set; }
        public RouteMatchYamlDto? Match { get; set; }
        public RouteDestinationYamlDto? Destination { get; set; }
    }

    internal sealed class RouteMatchYamlDto
    {
        // These are List<string> but the YAML may supply a bare string; handled by StringOrListConverter.
        public List<string>? Domain { get; set; }
        public List<string>? TagsAny { get; set; }
        public List<string>? Category { get; set; }
        public List<string>? Severity { get; set; }
        public List<string>? Profile { get; set; }
        public Dictionary<string, string>? SourceContext { get; set; }
    }

    internal sealed class RouteDestinationYamlDto
    {
        public string? Directory { get; set; }
        public string? FileName { get; set; }
        public string? Extension { get; set; }
    }

    internal sealed class FallbackYamlDto
    {
        public string? Mode { get; set; }
        public string? FileBaseName { get; set; }
    }

    internal sealed class PurgeYamlDto
    {
        public bool Enabled { get; set; } = true;
        public List<string>? Roots { get; set; }
        public List<string>? Globs { get; set; }
    }

    // ── Converter: bare string → single-element list ─────────────────────────

    private sealed class StringOrListConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type) => type == typeof(List<string>);

        public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            if (parser.TryConsume<Scalar>(out var scalar))
                return new List<string> { scalar.Value };

            if (parser.TryConsume<SequenceStart>(out _))
            {
                var result = new List<string>();
                while (!parser.TryConsume<SequenceEnd>(out _))
                {
                    if (parser.TryConsume<Scalar>(out var item))
                        result.Add(item.Value);
                }
                return result;
            }

            return new List<string>();
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
            => serializer(value, type);
    }
}
