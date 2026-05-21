using CsCheck;
using Steergen.Core.Packs;
using Steergen.Core.Validation;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for pack manifest validation correctness.
/// Feature: custom-template-packs, Property 2: Pack Manifest Validation
///
/// For any YAML document presented as a pack manifest, the manifest SHALL be valid
/// if and only if all required fields are present and well-formed:
/// - name (non-empty string)
/// - version (valid semver)
/// - minSteergenVersion (valid semver)
/// - For rules packs additionally: scope (one of global, supplemental, project)
///
/// **Validates: Requirements 2.2, 2.3, 9.3, 9.4**
/// </summary>
public sealed class PackManifestProperties
{
    // ── Generators ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates valid semver strings like "1.0.0", "2.3.4", "10.20.30".
    /// </summary>
    private static readonly Gen<string> GenValidSemver =
        Gen.Select(
            Gen.Int[0, 99],
            Gen.Int[0, 99],
            Gen.Int[0, 99])
        .Select((major, minor, patch) => $"{major}.{minor}.{patch}");

    /// <summary>
    /// Generates invalid version strings that are NOT valid semver.
    /// </summary>
    private static readonly Gen<string> GenInvalidSemver =
        Gen.OneOf(
            Gen.Const(""),
            Gen.Const("1"),
            Gen.Const("1.0"),
            Gen.Const("abc"),
            Gen.Const("1.0.0.0"),
            Gen.Const("v1.0.0"),
            Gen.Const(".1.0"),
            Gen.Const("1..0"),
            Gen.Const("1.0."),
            Gen.Const("-1.0.0"),
            Gen.Const("a.b.c"),
            Gen.String[Gen.Char.AlphaNumeric, 1, 8]
               .Where(s => !IsValidSemver(s)));

    /// <summary>
    /// Generates valid non-empty pack names.
    /// </summary>
    private static readonly Gen<string> GenValidName =
        Gen.String[Gen.Char.AlphaNumeric, 1, 30]
           .Select(s => s.Length == 0 ? "a" : s);

    /// <summary>
    /// Generates invalid names (empty or whitespace-only).
    /// </summary>
    private static readonly Gen<string> GenInvalidName =
        Gen.OneOf(
            Gen.Const(""),
            Gen.Const(" "),
            Gen.Const("  "),
            Gen.Const("\t"));

    /// <summary>
    /// Generates a valid PackScope value.
    /// </summary>
    private static readonly Gen<PackScope> GenValidScope =
        Gen.OneOf(
            Gen.Const(PackScope.Global),
            Gen.Const(PackScope.Supplemental),
            Gen.Const(PackScope.Project));

    /// <summary>
    /// Generates a PackType value.
    /// </summary>
    private static readonly Gen<PackType> GenPackType =
        Gen.OneOf(
            Gen.Const(PackType.Template),
            Gen.Const(PackType.Rules));

    /// <summary>
    /// Generates a running Steergen version that is always >= any generated minSteergenVersion.
    /// We use a high version to ensure compatibility checks pass when testing field validation.
    /// </summary>
    private static readonly Gen<string> GenRunningVersion =
        Gen.Const("99.99.99");

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks if a string is a valid semver (major.minor.patch, all non-negative integers).
    /// </summary>
    private static bool IsValidSemver(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split('.');
        if (parts.Length != 3)
            return false;

        return parts.All(p => int.TryParse(p, out var n) && n >= 0 && p == n.ToString());
    }

    /// <summary>
    /// Determines whether a manifest should be considered valid for the given pack type.
    /// </summary>
    private static bool ShouldBeValid(string? name, string? version, string? minSteergenVersion, PackScope? scope, PackType packType)
    {
        // Name must be non-empty and non-whitespace
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Version must be valid semver
        if (!IsValidSemver(version))
            return false;

        // MinSteergenVersion must be valid semver
        if (!IsValidSemver(minSteergenVersion))
            return false;

        // For rules packs, scope is additionally required
        if (packType == PackType.Rules && scope is null)
            return false;

        return true;
    }

    // ── Property 2: Pack Manifest Validation (Biconditional) ─────────────────────

    [Fact]
    public void ValidManifest_ProducesNoDiagnostics_ForTemplatePacks()
    {
        // Validates: Requirements 2.2, 2.3
        // A template pack manifest with all required fields present and well-formed
        // SHALL produce zero validation diagnostics.
        Gen.Select(GenValidName, GenValidSemver, GenValidSemver)
            .Sample(
                (name, version, minVersion) =>
                {
                    var manifest = new PackManifest
                    {
                        Name = name,
                        Version = version,
                        MinSteergenVersion = minVersion
                    };

                    var parser = new PackManifestParser();
                    var diagnostics = parser.Validate(manifest, PackType.Template, "99.99.99");

                    var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                    Assert.Empty(errors);
                },
                iter: 100,
                print: t => $"name=\"{t.Item1}\", version=\"{t.Item2}\", minVersion=\"{t.Item3}\"");
    }

    [Fact]
    public void ValidManifest_ProducesNoDiagnostics_ForRulesPacks()
    {
        // Validates: Requirements 9.3, 9.4
        // A rules pack manifest with all required fields (including scope) present and well-formed
        // SHALL produce zero validation diagnostics.
        Gen.Select(GenValidName, GenValidSemver, GenValidSemver, GenValidScope)
            .Sample(
                (name, version, minVersion, scope) =>
                {
                    var manifest = new PackManifest
                    {
                        Name = name,
                        Version = version,
                        MinSteergenVersion = minVersion,
                        Scope = scope
                    };

                    var parser = new PackManifestParser();
                    var diagnostics = parser.Validate(manifest, PackType.Rules, "99.99.99");

                    var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                    Assert.Empty(errors);
                },
                iter: 100,
                print: t => $"name=\"{t.Item1}\", version=\"{t.Item2}\", minVersion=\"{t.Item3}\", scope={t.Item4}");
    }

    [Fact]
    public void InvalidName_ProducesDiagnostic()
    {
        // Validates: Requirements 2.2
        // A manifest with an empty or whitespace-only name SHALL produce a validation error.
        Gen.Select(GenInvalidName, GenValidSemver, GenValidSemver, GenPackType)
            .Sample(
                (name, version, minVersion, packType) =>
                {
                    var manifest = new PackManifest
                    {
                        Name = name,
                        Version = version,
                        MinSteergenVersion = minVersion,
                        Scope = packType == PackType.Rules ? PackScope.Global : null
                    };

                    var parser = new PackManifestParser();
                    var diagnostics = parser.Validate(manifest, packType, "99.99.99");

                    var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                    Assert.NotEmpty(errors);
                },
                iter: 100,
                print: t => $"name=\"{t.Item1}\", version=\"{t.Item2}\", packType={t.Item4}");
    }

    [Fact]
    public void InvalidVersion_ProducesDiagnostic()
    {
        // Validates: Requirements 2.2
        // A manifest with an invalid version (not valid semver) SHALL produce a validation error.
        Gen.Select(GenValidName, GenInvalidSemver, GenValidSemver, GenPackType)
            .Sample(
                (name, version, minVersion, packType) =>
                {
                    var manifest = new PackManifest
                    {
                        Name = name,
                        Version = version,
                        MinSteergenVersion = minVersion,
                        Scope = packType == PackType.Rules ? PackScope.Global : null
                    };

                    var parser = new PackManifestParser();
                    var diagnostics = parser.Validate(manifest, packType, "99.99.99");

                    var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                    Assert.NotEmpty(errors);
                },
                iter: 100,
                print: t => $"name=\"{t.Item1}\", version=\"{t.Item2}\", packType={t.Item4}");
    }

    [Fact]
    public void InvalidMinSteergenVersion_ProducesDiagnostic()
    {
        // Validates: Requirements 2.2
        // A manifest with an invalid minSteergenVersion (not valid semver) SHALL produce a validation error.
        Gen.Select(GenValidName, GenValidSemver, GenInvalidSemver, GenPackType)
            .Sample(
                (name, version, minVersion, packType) =>
                {
                    var manifest = new PackManifest
                    {
                        Name = name,
                        Version = version,
                        MinSteergenVersion = minVersion,
                        Scope = packType == PackType.Rules ? PackScope.Global : null
                    };

                    var parser = new PackManifestParser();
                    var diagnostics = parser.Validate(manifest, packType, "99.99.99");

                    var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                    Assert.NotEmpty(errors);
                },
                iter: 100,
                print: t => $"name=\"{t.Item1}\", version=\"{t.Item2}\", minVersion=\"{t.Item3}\", packType={t.Item4}");
    }

    [Fact]
    public void RulesPack_MissingScope_ProducesDiagnostic()
    {
        // Validates: Requirements 9.3, 9.4
        // A rules pack manifest without a scope SHALL produce a validation error.
        Gen.Select(GenValidName, GenValidSemver, GenValidSemver)
            .Sample(
                (name, version, minVersion) =>
                {
                    var manifest = new PackManifest
                    {
                        Name = name,
                        Version = version,
                        MinSteergenVersion = minVersion,
                        Scope = null // Missing scope for rules pack
                    };

                    var parser = new PackManifestParser();
                    var diagnostics = parser.Validate(manifest, PackType.Rules, "99.99.99");

                    var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                    Assert.NotEmpty(errors);
                },
                iter: 100,
                print: t => $"name=\"{t.Item1}\", version=\"{t.Item2}\", minVersion=\"{t.Item3}\"");
    }

    [Fact]
    public void TemplatePack_MissingScope_IsValid()
    {
        // Validates: Requirements 2.2, 2.3
        // A template pack manifest without a scope SHALL still be valid (scope is only required for rules packs).
        Gen.Select(GenValidName, GenValidSemver, GenValidSemver)
            .Sample(
                (name, version, minVersion) =>
                {
                    var manifest = new PackManifest
                    {
                        Name = name,
                        Version = version,
                        MinSteergenVersion = minVersion,
                        Scope = null // No scope for template pack — this is fine
                    };

                    var parser = new PackManifestParser();
                    var diagnostics = parser.Validate(manifest, PackType.Template, "99.99.99");

                    var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                    Assert.Empty(errors);
                },
                iter: 100,
                print: t => $"name=\"{t.Item1}\", version=\"{t.Item2}\", minVersion=\"{t.Item3}\"");
    }

    [Fact]
    public void Validation_Biconditional_ValidIffAllRequiredFieldsPresent()
    {
        // Validates: Requirements 2.2, 2.3, 9.3, 9.4
        // Combined biconditional property: validation produces zero errors IFF
        // all required fields are present and well-formed for the given pack type.
        var genName = Gen.OneOf(GenValidName, GenInvalidName);
        var genVersion = Gen.OneOf(GenValidSemver, GenInvalidSemver);
        var genMinVersion = Gen.OneOf(GenValidSemver, GenInvalidSemver);
        var genScope = Gen.OneOf(
            GenValidScope.Select(s => (PackScope?)s),
            Gen.Const((PackScope?)null));

        Gen.Select(genName, genVersion, genMinVersion, genScope, GenPackType)
            .Sample(
                (name, version, minVersion, scope, packType) =>
                {
                    var manifest = new PackManifest
                    {
                        Name = name,
                        Version = version,
                        MinSteergenVersion = minVersion,
                        Scope = scope
                    };

                    var parser = new PackManifestParser();
                    var diagnostics = parser.Validate(manifest, packType, "99.99.99");

                    var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                    var expectedValid = ShouldBeValid(name, version, minVersion, scope, packType);

                    if (expectedValid)
                    {
                        Assert.Empty(errors);
                    }
                    else
                    {
                        Assert.NotEmpty(errors);
                    }
                },
                iter: 200,
                print: t => $"name=\"{t.Item1}\", version=\"{t.Item2}\", minVersion=\"{t.Item3}\", scope={t.Item4?.ToString() ?? "(null)"}, packType={t.Item5}");
    }
}
