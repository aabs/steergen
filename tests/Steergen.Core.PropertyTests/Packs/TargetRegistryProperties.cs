using CsCheck;
using Steergen.Core.Packs;
using Steergen.Core.Targets;
using Steergen.Core.Validation;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for external target registration consistency.
///
/// Property 14: External Target Registration Consistency
/// For any template pack manifest declaring providedTargets, the target registry
/// SHALL make those targets available for generation if and only if the referenced
/// defaultLayout file exists within the pack directory. Additionally, for any
/// registered target (built-in or pack-provided), the IsAvailable check SHALL
/// return true, and for any unregistered target ID, it SHALL return false.
///
/// **Validates: Requirements 16.1, 16.3, 16.4, 16.6**
/// </summary>
public sealed class TargetRegistryProperties : IDisposable
{
    private readonly string _testRoot;

    /// <summary>
    /// Lock to serialize access to the static TargetRegistry across concurrent CsCheck samples.
    /// </summary>
    private static readonly object RegistryLock = new();

    public TargetRegistryProperties()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "TargetRegProps_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        lock (RegistryLock)
        {
            TargetRegistry.Clear();
        }

        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    // ── Generators ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates valid target IDs (alphanumeric lowercase, 3-12 chars).
    /// </summary>
    private static readonly Gen<string> GenTargetId =
        Gen.String[Gen.Char['a', 'z'], 3, 12];

    /// <summary>
    /// Generates valid pack names.
    /// </summary>
    private static readonly Gen<string> GenPackName =
        Gen.Select(
            Gen.String[Gen.Char['a', 'z'], 3, 8],
            Gen.String[Gen.Char['a', 'z'], 3, 8])
        .Select((a, b) => $"{a}-{b}-pack");

    /// <summary>
    /// Generates valid semver strings.
    /// </summary>
    private static readonly Gen<string> GenSemver =
        Gen.Select(Gen.Int[0, 20], Gen.Int[0, 20], Gen.Int[0, 20])
           .Select((major, minor, patch) => $"{major}.{minor}.{patch}");

    /// <summary>
    /// Generates a relative layout file path within a pack.
    /// </summary>
    private static readonly Gen<string> GenLayoutPath =
        Gen.Select(GenTargetId, Gen.String[Gen.Char['a', 'z'], 3, 8])
           .Select((targetDir, name) => Path.Combine(targetDir, $"{name}-layout.yaml"));

    /// <summary>
    /// Generates a boolean indicating whether the layout file should exist on disk.
    /// </summary>
    private static readonly Gen<bool> GenLayoutExists = Gen.Bool;

    /// <summary>
    /// Generates a provided target definition with random layout existence.
    /// </summary>
    private static readonly Gen<(ProvidedTargetDefinition Definition, bool LayoutExists)> GenProvidedTarget =
        Gen.Select(GenTargetId, GenLayoutPath, GenLayoutExists)
           .Select((targetId, layout, exists) => (
               new ProvidedTargetDefinition
               {
                   TargetId = targetId,
                   DefaultLayout = layout,
                   Description = $"Test target: {targetId}"
               },
               exists));

    /// <summary>
    /// Generates a list of 1-5 provided target definitions with unique target IDs.
    /// </summary>
    private static readonly Gen<IReadOnlyList<(ProvidedTargetDefinition Definition, bool LayoutExists)>> GenProvidedTargets =
        GenProvidedTarget.Array[1, 5]
            .Select(arr =>
            {
                // Ensure unique target IDs
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var unique = new List<(ProvidedTargetDefinition, bool)>();
                foreach (var item in arr)
                {
                    if (seen.Add(item.Definition.TargetId))
                        unique.Add(item);
                }
                return (IReadOnlyList<(ProvidedTargetDefinition, bool)>)unique;
            })
            .Where(list => list.Count > 0);

    /// <summary>
    /// Generates a random target ID that is unlikely to collide with generated ones.
    /// Used for testing IsAvailable returns false for unregistered targets.
    /// </summary>
    private static readonly Gen<string> GenUnregisteredTargetId =
        Gen.String[Gen.Char['a', 'z'], 13, 20]
           .Select(s => $"unreg-{s}");

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private string CreatePackDir()
    {
        var dir = Path.Combine(_testRoot, "pack_" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void EnsureLayoutFile(string packDir, string relativePath)
    {
        var fullPath = Path.Combine(packDir, relativePath);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, "# layout placeholder\nfiles: []");
    }

    private static PackManifest CreateManifest(
        string name,
        string version,
        IReadOnlyList<ProvidedTargetDefinition> providedTargets) =>
        new()
        {
            Name = name,
            Version = version,
            MinSteergenVersion = "1.0.0",
            ProvidedTargets = providedTargets
        };

    // ── Property 14a: Targets available iff defaultLayout exists ─────────────────

    [Fact]
    public void RegisterPackTargets_MakesTargetAvailable_IffDefaultLayoutExists()
    {
        // **Validates: Requirements 16.1, 16.3, 16.4**
        //
        // For any manifest with providedTargets, the target registry SHALL make
        // those targets available for generation if and only if the referenced
        // defaultLayout file exists within the pack directory.
        Gen.Select(GenPackName, GenSemver, GenProvidedTargets)
            .Sample(
                (packName, version, targets) =>
                {
                    lock (RegistryLock)
                    {
                        TargetRegistry.Clear();

                        var packDir = CreatePackDir();
                        var templateProvider = new FakeTemplateProvider();

                        // Set up filesystem: create layout files only where LayoutExists is true
                        foreach (var (def, exists) in targets)
                        {
                            if (exists)
                                EnsureLayoutFile(packDir, def.DefaultLayout);
                        }

                        var manifest = CreateManifest(
                            packName,
                            version,
                            targets.Select(t => t.Definition).ToList());

                        TargetRegistry.RegisterPackTargets(manifest, packDir, templateProvider);

                        // Assert: each target is available iff its layout file existed
                        foreach (var (def, exists) in targets)
                        {
                            var isAvailable = TargetRegistry.IsAvailable(def.TargetId);

                            if (exists)
                            {
                                Assert.True(isAvailable,
                                    $"Target '{def.TargetId}' should be available because defaultLayout exists.");
                            }
                            else
                            {
                                Assert.False(isAvailable,
                                    $"Target '{def.TargetId}' should NOT be available because defaultLayout is missing.");
                            }
                        }
                    }
                },
                iter: 150,
                print: t => $"pack={t.Item1}, targets=[{string.Join(", ", t.Item3.Select(x => $"{x.Definition.TargetId}(exists={x.LayoutExists})"))}]");
    }

    // ── Property 14b: TP009 diagnostic emitted for missing layout ─────────────────

    [Fact]
    public void RegisterPackTargets_EmitsTP009_WhenDefaultLayoutMissing()
    {
        // **Validates: Requirements 16.4**
        //
        // For any provided target whose defaultLayout file does not exist,
        // the registry SHALL emit a TP009 diagnostic error.
        Gen.Select(GenPackName, GenSemver, GenProvidedTargets)
            .Sample(
                (packName, version, targets) =>
                {
                    lock (RegistryLock)
                    {
                        TargetRegistry.Clear();

                        var packDir = CreatePackDir();
                        var templateProvider = new FakeTemplateProvider();

                        // Set up filesystem: create layout files only where LayoutExists is true
                        foreach (var (def, exists) in targets)
                        {
                            if (exists)
                                EnsureLayoutFile(packDir, def.DefaultLayout);
                        }

                        var manifest = CreateManifest(
                            packName,
                            version,
                            targets.Select(t => t.Definition).ToList());

                        var diagnostics = TargetRegistry.RegisterPackTargets(manifest, packDir, templateProvider);

                        var missingCount = targets.Count(t => !t.LayoutExists);
                        var tp009Diagnostics = diagnostics
                            .Where(d => d.Code == "TP009" && d.Severity == DiagnosticSeverity.Error)
                            .ToList();

                        Assert.Equal(missingCount, tp009Diagnostics.Count);
                    }
                },
                iter: 150,
                print: t => $"pack={t.Item1}, targets=[{string.Join(", ", t.Item3.Select(x => $"{x.Definition.TargetId}(exists={x.LayoutExists})"))}]");
    }

    // ── Property 14c: IsAvailable returns true for registered targets ────────────

    [Fact]
    public void IsAvailable_ReturnsTrue_ForAllRegisteredTargets()
    {
        // **Validates: Requirements 16.6**
        //
        // For any registered target (built-in or pack-provided), the IsAvailable
        // check SHALL return true.
        Gen.Select(GenPackName, GenSemver, GenProvidedTargets)
            .Sample(
                (packName, version, targets) =>
                {
                    lock (RegistryLock)
                    {
                        TargetRegistry.Clear();

                        var packDir = CreatePackDir();
                        var templateProvider = new FakeTemplateProvider();

                        // Create ALL layout files so all targets get registered
                        foreach (var (def, _) in targets)
                        {
                            EnsureLayoutFile(packDir, def.DefaultLayout);
                        }

                        var manifest = CreateManifest(
                            packName,
                            version,
                            targets.Select(t => t.Definition).ToList());

                        TargetRegistry.RegisterPackTargets(manifest, packDir, templateProvider);

                        // Assert: every registered target reports as available
                        foreach (var (def, _) in targets)
                        {
                            Assert.True(TargetRegistry.IsAvailable(def.TargetId),
                                $"Registered target '{def.TargetId}' should be available.");
                        }
                    }
                },
                iter: 150,
                print: t => $"pack={t.Item1}, targets=[{string.Join(", ", t.Item3.Select(x => x.Definition.TargetId))}]");
    }

    // ── Property 14d: IsAvailable returns false for unregistered targets ─────────

    [Fact]
    public void IsAvailable_ReturnsFalse_ForUnregisteredTargets()
    {
        // **Validates: Requirements 16.6**
        //
        // For any unregistered target ID, the IsAvailable check SHALL return false.
        Gen.Select(GenPackName, GenSemver, GenProvidedTargets, GenUnregisteredTargetId)
            .Sample(
                (packName, version, targets, unregisteredId) =>
                {
                    lock (RegistryLock)
                    {
                        TargetRegistry.Clear();

                        var packDir = CreatePackDir();
                        var templateProvider = new FakeTemplateProvider();

                        // Create all layout files so all targets get registered
                        foreach (var (def, _) in targets)
                        {
                            EnsureLayoutFile(packDir, def.DefaultLayout);
                        }

                        var manifest = CreateManifest(
                            packName,
                            version,
                            targets.Select(t => t.Definition).ToList());

                        TargetRegistry.RegisterPackTargets(manifest, packDir, templateProvider);

                        // The unregistered ID should not be available
                        Assert.False(TargetRegistry.IsAvailable(unregisteredId),
                            $"Unregistered target '{unregisteredId}' should NOT be available.");
                    }
                },
                iter: 150,
                print: t => $"pack={t.Item1}, unregistered={t.Item4}");
    }

    // ── Property 14e: Empty registry returns false for any target ID ─────────────

    [Fact]
    public void IsAvailable_ReturnsFalse_WhenRegistryIsEmpty()
    {
        // **Validates: Requirements 16.6**
        //
        // When no targets are registered, IsAvailable SHALL return false for any ID.
        GenTargetId.Sample(
            targetId =>
            {
                lock (RegistryLock)
                {
                    TargetRegistry.Clear();
                    Assert.False(TargetRegistry.IsAvailable(targetId),
                        $"Target '{targetId}' should not be available in empty registry.");
                }
            },
            iter: 100,
            print: id => $"targetId={id}");
    }

    // ── Property 14f: Manifest with no providedTargets registers nothing ─────────

    [Fact]
    public void RegisterPackTargets_WithNullProvidedTargets_RegistersNothing()
    {
        // **Validates: Requirements 16.1**
        //
        // A manifest with null or empty providedTargets SHALL not register any targets.
        Gen.Select(GenPackName, GenSemver, GenTargetId)
            .Sample(
                (packName, version, queryTarget) =>
                {
                    lock (RegistryLock)
                    {
                        TargetRegistry.Clear();

                        var packDir = CreatePackDir();
                        var templateProvider = new FakeTemplateProvider();

                        var manifest = new PackManifest
                        {
                            Name = packName,
                            Version = version,
                            MinSteergenVersion = "1.0.0",
                            ProvidedTargets = null
                        };

                        var diagnostics = TargetRegistry.RegisterPackTargets(manifest, packDir, templateProvider);

                        Assert.Empty(diagnostics);
                        Assert.False(TargetRegistry.IsAvailable(queryTarget));
                    }
                },
                iter: 100,
                print: t => $"pack={t.Item1}, query={t.Item3}");
    }

    // ── Fake template provider ───────────────────────────────────────────────────

    /// <summary>
    /// A fake ITemplateProvider for use in registration tests.
    /// The actual template content is irrelevant for registration consistency tests.
    /// </summary>
    private sealed class FakeTemplateProvider : ITemplateProvider
    {
        public string GetTemplate(string targetId, string templateName) =>
            $"{{{{ # template for {targetId}/{templateName} }}}}";
    }
}
