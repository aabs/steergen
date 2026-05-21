using Steergen.Core.Validation;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Steergen.Core.Packs;

/// <summary>
/// Parses and validates <c>pack.yaml</c> manifest files for template packs and rules packs.
/// </summary>
public sealed class PackManifestParser
{
    private const string ManifestFileName = "pack.yaml";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Parses pack.yaml from the given directory.
    /// Returns null if pack.yaml does not exist.
    /// </summary>
    public PackManifest? Parse(string packDirectory)
    {
        var manifestPath = Path.Combine(packDirectory, ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var content = File.ReadAllText(manifestPath);
        var yaml = Deserializer.Deserialize<PackManifestYaml>(content);

        return MapToModel(yaml);
    }

    /// <summary>
    /// Validates manifest fields. Returns diagnostics for missing/invalid fields.
    /// For rules packs, additionally validates that <c>scope</c> is present and valid.
    /// Version compatibility is checked against the running Steergen version.
    /// </summary>
    public IReadOnlyList<Diagnostic> Validate(
        PackManifest manifest,
        PackType packType,
        string runningSteergenVersion)
    {
        var diagnostics = new List<Diagnostic>();

        // Validate name (non-empty)
        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            diagnostics.Add(new Diagnostic(
                "PM001",
                "Pack manifest 'name' field is required and must be non-empty.",
                DiagnosticSeverity.Error));
        }

        // Validate version (valid semver)
        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            diagnostics.Add(new Diagnostic(
                "PM002",
                "Pack manifest 'version' field is required.",
                DiagnosticSeverity.Error));
        }
        else if (!IsValidSemver(manifest.Version))
        {
            diagnostics.Add(new Diagnostic(
                "PM003",
                $"Pack manifest 'version' field '{manifest.Version}' is not a valid semantic version (expected major.minor.patch).",
                DiagnosticSeverity.Error));
        }

        // Validate minSteergenVersion (valid semver)
        if (string.IsNullOrWhiteSpace(manifest.MinSteergenVersion))
        {
            diagnostics.Add(new Diagnostic(
                "PM004",
                "Pack manifest 'minSteergenVersion' field is required.",
                DiagnosticSeverity.Error));
        }
        else if (!IsValidSemver(manifest.MinSteergenVersion))
        {
            diagnostics.Add(new Diagnostic(
                "PM005",
                $"Pack manifest 'minSteergenVersion' field '{manifest.MinSteergenVersion}' is not a valid semantic version (expected major.minor.patch).",
                DiagnosticSeverity.Error));
        }
        else if (IsValidSemver(runningSteergenVersion) &&
                 !IsVersionCompatible(runningSteergenVersion, manifest.MinSteergenVersion))
        {
            diagnostics.Add(new Diagnostic(
                "PM006",
                $"Running Steergen version '{runningSteergenVersion}' is lower than the required minimum '{manifest.MinSteergenVersion}'.",
                DiagnosticSeverity.Error));
        }

        // For rules packs, validate scope is present and valid
        if (packType == PackType.Rules)
        {
            if (manifest.Scope is null)
            {
                diagnostics.Add(new Diagnostic(
                    "PM007",
                    "Rules pack manifest 'scope' field is required (must be one of: global, supplemental, project).",
                    DiagnosticSeverity.Error));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates that a version string matches the semver pattern: major.minor.patch
    /// where all components are non-negative integers.
    /// </summary>
    internal static bool IsValidSemver(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        var parts = version.Split('.');
        if (parts.Length != 3)
            return false;

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
                return false;

            // Reject leading zeros (except for "0" itself)
            if (part.Length > 1 && part[0] == '0')
                return false;

            if (!int.TryParse(part, out var value) || value < 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true if runningVersion >= minVersion using standard semver comparison.
    /// Both versions must be valid semver strings.
    /// </summary>
    internal static bool IsVersionCompatible(string runningVersion, string minVersion)
    {
        var running = ParseSemverParts(runningVersion);
        var min = ParseSemverParts(minVersion);

        if (running is null || min is null)
            return false;

        if (running.Value.Major != min.Value.Major)
            return running.Value.Major > min.Value.Major;

        if (running.Value.Minor != min.Value.Minor)
            return running.Value.Minor > min.Value.Minor;

        return running.Value.Patch >= min.Value.Patch;
    }

    private static (int Major, int Minor, int Patch)? ParseSemverParts(string version)
    {
        var parts = version.Split('.');
        if (parts.Length != 3)
            return null;

        if (!int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
            return null;

        if (major < 0 || minor < 0 || patch < 0)
            return null;

        return (major, minor, patch);
    }

    private static PackManifest MapToModel(PackManifestYaml yaml)
    {
        PackScope? scope = ParseScope(yaml.Scope);

        return new PackManifest
        {
            Name = yaml.Name ?? string.Empty,
            Version = yaml.Version ?? string.Empty,
            MinSteergenVersion = yaml.MinSteergenVersion ?? string.Empty,
            Scope = scope,
            Targets = yaml.Targets,
            ProvidedTargets = yaml.ProvidedTargets?
                .Select(pt => new ProvidedTargetDefinition
                {
                    TargetId = pt.TargetId ?? string.Empty,
                    DefaultLayout = pt.DefaultLayout ?? string.Empty,
                    Description = pt.Description,
                })
                .ToList(),
            RulesRoot = yaml.RulesRoot,
        };
    }

    private static PackScope? ParseScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return null;

        return scope.ToLowerInvariant() switch
        {
            "global" => PackScope.Global,
            "supplemental" => PackScope.Supplemental,
            "project" => PackScope.Project,
            _ => null,
        };
    }

    // ── YAML deserialization model ──────────────────────────────────────────

    internal sealed class PackManifestYaml
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? MinSteergenVersion { get; set; }
        public string? Scope { get; set; }
        public List<string>? Targets { get; set; }
        public List<ProvidedTargetDefinitionYaml>? ProvidedTargets { get; set; }
        public string? RulesRoot { get; set; }
    }

    internal sealed class ProvidedTargetDefinitionYaml
    {
        public string? TargetId { get; set; }
        public string? DefaultLayout { get; set; }
        public string? Description { get; set; }
    }
}
