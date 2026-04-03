using Steergen.Core.Generation;
using Steergen.Core.Model;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Fixtures;

namespace Steergen.Core.UnitTests.Targets;

/// <summary>
/// Proves that registering an additive (fixture) target has no effect on existing built-in
/// target outputs, and that the fixture target itself produces correct output.
/// </summary>
public sealed class TargetRegistryCompatibilityTests
{
    private static ResolvedSteeringModel BuildSampleModel(int ruleCount = 3) =>
        new()
        {
            Rules = Enumerable.Range(1, ruleCount)
                .Select(i => new SteeringRule
                {
                    Id = $"RULE-{i:D3}",
                    Severity = "error",
                    Domain = "core",
                    PrimaryText = $"Rule {i} prose text.",
                })
                .ToList(),
        };

    [Fact]
    public void TargetRegistrationMetadata_FromDescriptor_RoundTrips()
    {
        var descriptor = new TargetDescriptor("my-target", "My Target", "A test target.");
        var metadata = TargetRegistrationMetadata.FromDescriptor(descriptor, isBuiltIn: false);

        Assert.Equal(descriptor.Id, metadata.TargetId);
        Assert.Equal(descriptor.DisplayName, metadata.DisplayName);
        Assert.Equal(descriptor.Description, metadata.Description);
        Assert.False(metadata.IsBuiltIn);
        Assert.Null(metadata.AuthorName);
        Assert.Null(metadata.Version);
    }

    [Fact]
    public void TargetRegistrationMetadata_BuiltInFlag_PreservedOnBuiltins()
    {
        var descriptor = new TargetDescriptor("speckit", "Speckit", "Built-in target.");
        var metadata = TargetRegistrationMetadata.FromDescriptor(descriptor, isBuiltIn: true);

        Assert.True(metadata.IsBuiltIn);
    }

    [Fact]
    public async Task FixtureTarget_WritesManifestFile()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"fixture-compat-{Guid.NewGuid():N}");
        try
        {
            var model = BuildSampleModel(3);
            var target = new FixtureTargetComponent();
            var config = new TargetConfiguration
            {
                Id = "fixture",
                Enabled = true,
                OutputPath = outputDir,
            };

            await target.GenerateAsync(model, config, CancellationToken.None);

            var manifestPath = Path.Combine(outputDir, "fixture-manifest.txt");
            Assert.True(File.Exists(manifestPath), "fixture-manifest.txt should be created");

            var lines = await File.ReadAllLinesAsync(manifestPath);
            Assert.Equal(3, lines.Length);
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task FixtureTarget_ManifestIsSortedAlphabetically()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"fixture-sort-{Guid.NewGuid():N}");
        try
        {
            var model = new ResolvedSteeringModel
            {
                Rules =
                [
                    new SteeringRule { Id = "ZZRULE", Severity = "error", Domain = "core", PrimaryText = "Z rule." },
                    new SteeringRule { Id = "AARULE", Severity = "error", Domain = "core", PrimaryText = "A rule." },
                    new SteeringRule { Id = "MMRULE", Severity = "error", Domain = "core", PrimaryText = "M rule." },
                ],
            };

            var target = new FixtureTargetComponent();
            var config = new TargetConfiguration
            {
                Id = "fixture",
                Enabled = true,
                OutputPath = outputDir,
            };

            await target.GenerateAsync(model, config, CancellationToken.None);

            var lines = await File.ReadAllLinesAsync(Path.Combine(outputDir, "fixture-manifest.txt"));
            Assert.Equal(["AARULE", "MMRULE", "ZZRULE"], lines);
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task FixtureTarget_DeprecatedRulesAreExcluded()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"fixture-deprecated-{Guid.NewGuid():N}");
        try
        {
            var model = new ResolvedSteeringModel
            {
                Rules =
                [
                    new SteeringRule { Id = "ACTIVE-001", Severity = "error", Domain = "core", PrimaryText = "Active." },
                    new SteeringRule { Id = "OLD-001", Severity = "error", Domain = "core", PrimaryText = "Old.", Deprecated = true },
                ],
            };

            var target = new FixtureTargetComponent();
            var config = new TargetConfiguration
            {
                Id = "fixture",
                Enabled = true,
                OutputPath = outputDir,
            };

            await target.GenerateAsync(model, config, CancellationToken.None);

            var lines = await File.ReadAllLinesAsync(Path.Combine(outputDir, "fixture-manifest.txt"));
            Assert.Single(lines);
            Assert.Equal("ACTIVE-001", lines[0]);
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task AddingFixtureTarget_DoesNotAffectSpeckitOutput()
    {
        // Verifies that Speckit and Fixture generate side-by-side without interfering.
        var speckitDir = Path.Combine(Path.GetTempPath(), $"speckit-compat-{Guid.NewGuid():N}");
        var fixtureDir = Path.Combine(Path.GetTempPath(), $"fixture-compat2-{Guid.NewGuid():N}");
        try
        {
            var model = BuildSampleModel(2);

            var speckitTarget = new Steergen.Core.Targets.Speckit.SpeckitTargetComponent(
                new StubSpeckitTemplateProvider());
            var fixtureTarget = new FixtureTargetComponent();

            await speckitTarget.GenerateAsync(model, new TargetConfiguration
            {
                Id = "speckit",
                Enabled = true,
                OutputPath = speckitDir,
            }, CancellationToken.None);

            await fixtureTarget.GenerateAsync(model, new TargetConfiguration
            {
                Id = "fixture",
                Enabled = true,
                OutputPath = fixtureDir,
            }, CancellationToken.None);

            // Speckit still produces constitution.md
            Assert.True(File.Exists(Path.Combine(speckitDir, "constitution.md")));
            // Fixture independently produces fixture-manifest.txt
            Assert.True(File.Exists(Path.Combine(fixtureDir, "fixture-manifest.txt")));
            // Speckit output is unaffected by fixture (no extra files)
            Assert.DoesNotContain(
                Directory.GetFiles(speckitDir).Select(Path.GetFileName),
                f => f == "fixture-manifest.txt");
        }
        finally
        {
            if (Directory.Exists(speckitDir)) Directory.Delete(speckitDir, recursive: true);
            if (Directory.Exists(fixtureDir)) Directory.Delete(fixtureDir, recursive: true);
        }
    }

    private sealed class StubSpeckitTemplateProvider : ITemplateProvider
    {
        public string GetTemplate(string targetId, string templateName) =>
            templateName switch
            {
                "constitution" => "# Constitution\n{{- for rule in rules }}\n- {{ rule.primary_text }}\n{{- end }}\n",
                "module" => "# {{ domain }}\n{{- for rule in rules }}\n- {{ rule.primary_text }}\n{{- end }}\n",
                _ => string.Empty,
            };
    }

    [Fact]
    public void FixtureTarget_HasCorrectTargetId()
    {
        var target = new FixtureTargetComponent();
        Assert.Equal("fixture", target.TargetId);
        Assert.Equal("fixture", target.Descriptor.Id);
        Assert.Equal("Fixture", target.Descriptor.DisplayName);
    }

    [Fact]
    public async Task FixtureTarget_MissingOutputPath_Throws()
    {
        var target = new FixtureTargetComponent();
        var config = new TargetConfiguration { Id = "fixture", Enabled = true, OutputPath = null };
        var model = BuildSampleModel(1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => target.GenerateAsync(model, config, CancellationToken.None));
    }
}
