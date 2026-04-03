using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Updates;
using Xunit;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]

/// <summary>
/// Integration tests verifying that constitution amendment provenance is captured correctly
/// when <see cref="TemplatePackUpdater"/> performs a version update.
/// Covers: version rationale storage, amendment date recording, impacted-artifact sync
/// records, and idempotent multi-amendment accumulation.
/// </summary>
public sealed class ConstitutionProvenanceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "steergen-provenance-test-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task<string> WriteConfigAsync(string dir, string? templatePackVersion = "1.0.0")
    {
        var path = Path.Combine(dir, "steergen.config.yaml");
        var config = new SteeringConfiguration
        {
            GlobalRoot  = Path.Combine(dir, "steering", "global"),
            ProjectRoot = Path.Combine(dir, "steering", "project"),
            TemplatePackVersion = templatePackVersion,
        };
        var writer = new SteergenConfigWriter();
        await writer.WriteAsync(path, config);
        return path;
    }

    private static ConstitutionProvenanceRecorder BuildRecorder(string dir) =>
        new(dir);

    private static TemplatePackUpdater BuildUpdater(string dir) =>
        new(provenance: BuildRecorder(dir));

    // ── Provenance file creation ──────────────────────────────────────────────

    [Fact]
    public async Task Update_WithProvenance_CreatesProvenanceFile()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);
            var updater    = BuildUpdater(dir);

            await updater.UpdateAsync(configPath, version: "1.2.0", preview: false);

            var provenancePath = Path.Combine(dir, ConstitutionProvenanceRecorder.DefaultFileName);
            Assert.True(File.Exists(provenancePath), "provenance file should be created");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Update_WithoutProvenance_DoesNotCreateProvenanceFile()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);
            var updater    = new TemplatePackUpdater(); // no provenance recorder

            await updater.UpdateAsync(configPath, version: "1.2.0", preview: false);

            var provenancePath = Path.Combine(dir, ConstitutionProvenanceRecorder.DefaultFileName);
            Assert.False(File.Exists(provenancePath), "provenance file must not be created when recorder is absent");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Version fields ────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WithProvenance_RecordsPreviousAndNewVersion()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            var updater    = BuildUpdater(dir);

            await updater.UpdateAsync(configPath, version: "1.2.0", preview: false);

            var recorder = BuildRecorder(dir);
            var entries  = await recorder.LoadAsync();

            Assert.Single(entries);
            Assert.Equal("1.0.0", entries[0].PreviousVersion);
            Assert.Equal("1.2.0", entries[0].NewVersion);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Amendment date ────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WithProvenance_RecordsAmendmentDateAsUtcNow()
    {
        var dir = CreateTempDir();
        try
        {
            var before     = DateTimeOffset.UtcNow.AddSeconds(-1);
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            var updater    = BuildUpdater(dir);

            await updater.UpdateAsync(configPath, version: "1.2.0", preview: false);

            var after    = DateTimeOffset.UtcNow.AddSeconds(1);
            var recorder = BuildRecorder(dir);
            var entries  = await recorder.LoadAsync();

            Assert.Single(entries);
            Assert.InRange(entries[0].AmendmentDate, before, after);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Version rationale ─────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WithRationale_PersistsRationaleInProvenanceEntry()
    {
        const string rationale = "Security patch: rule CORE-002 revised";
        var dir        = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            var updater    = BuildUpdater(dir);

            await updater.UpdateAsync(configPath, version: "1.2.0", preview: false, versionRationale: rationale);

            var recorder = BuildRecorder(dir);
            var entries  = await recorder.LoadAsync();

            Assert.Single(entries);
            Assert.Equal(rationale, entries[0].VersionRationale);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Update_WithoutRationale_ProvenanceEntryHasNullRationale()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            var updater    = BuildUpdater(dir);

            await updater.UpdateAsync(configPath, version: "1.2.0", preview: false);

            var recorder = BuildRecorder(dir);
            var entries  = await recorder.LoadAsync();

            Assert.Single(entries);
            Assert.Null(entries[0].VersionRationale);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Impacted-artifact sync record ─────────────────────────────────────────

    [Fact]
    public async Task Update_WithImpactedArtifacts_PersistsArtifactList()
    {
        var artifacts = new[] { ".speckit/constitution.md", ".kiro/steering/global.md" };
        var dir       = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            var updater    = BuildUpdater(dir);

            await updater.UpdateAsync(
                configPath,
                version: "1.2.0",
                preview: false,
                impactedArtifacts: artifacts);

            var recorder = BuildRecorder(dir);
            var entries  = await recorder.LoadAsync();

            Assert.Single(entries);
            Assert.Equal(artifacts, entries[0].ImpactedArtifacts);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Update_WithoutImpactedArtifacts_ProvenanceEntryHasEmptyList()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            var updater    = BuildUpdater(dir);

            await updater.UpdateAsync(configPath, version: "1.2.0", preview: false);

            var recorder = BuildRecorder(dir);
            var entries  = await recorder.LoadAsync();

            Assert.Single(entries);
            Assert.Empty(entries[0].ImpactedArtifacts);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Multi-amendment accumulation ─────────────────────────────────────────

    [Fact]
    public async Task Update_CalledTwice_ProvenanceFileContainsBothEntries()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            var updater    = BuildUpdater(dir);

            await updater.UpdateAsync(configPath, version: "1.1.0", preview: false, versionRationale: "First amendment");
            await updater.UpdateAsync(configPath, version: "1.2.0", preview: false, versionRationale: "Second amendment");

            var recorder = BuildRecorder(dir);
            var entries  = await recorder.LoadAsync();

            Assert.Equal(2, entries.Count);
            Assert.Equal("1.0.0", entries[0].PreviousVersion);
            Assert.Equal("1.1.0", entries[0].NewVersion);
            Assert.Equal("1.1.0", entries[1].PreviousVersion);
            Assert.Equal("1.2.0", entries[1].NewVersion);
            Assert.Equal("First amendment",  entries[0].VersionRationale);
            Assert.Equal("Second amendment", entries[1].VersionRationale);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Error flows ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_FailedUpdate_DoesNotWriteProvenanceEntry()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir, "1.0.0");
            var updater    = BuildUpdater(dir);

            // Request a version that doesn't exist in the catalog
            var result = await updater.UpdateAsync(configPath, version: "99.99.99", preview: false);

            Assert.False(result.Success);

            var provenancePath = Path.Combine(dir, ConstitutionProvenanceRecorder.DefaultFileName);
            // Either the file doesn't exist, or it exists but is empty
            if (File.Exists(provenancePath))
            {
                var recorder = BuildRecorder(dir);
                var entries  = await recorder.LoadAsync();
                Assert.Empty(entries);
            }
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
