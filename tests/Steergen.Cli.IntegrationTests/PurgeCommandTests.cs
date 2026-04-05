using Steergen.Cli.Commands;

namespace Steergen.Cli.IntegrationTests;

/// <summary>
/// CLI integration tests for the <c>purge</c> command.
///
/// Validates:
/// - Files matching configured globs under configured roots are removed.
/// - Dry-run reports candidates without deleting.
/// - Targets with no globs produce no-op output.
/// - Purge works without a prior generation manifest.
/// - Target scoping: only the requested target is purged.
/// </summary>
[Collection("CliOutput")]
public sealed class PurgeCommandTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private string MakeTempDir(string prefix = "purge-integ-")
    {
        var dir = Directory.CreateTempSubdirectory(prefix).FullName;
        _tempDirs.Add(dir);
        return dir;
    }

    private static string MakePurgeLayoutYaml(string purgeRoot) => $"""
        version: "1.0"
        roots:
          globalRoot: "{purgeRoot}"
          projectRoot: "{purgeRoot}"
          targetRoot: "{purgeRoot}"
        routes:
          - id: core-anchor
            scope: both
            explicit: true
            anchor: core
            order: 10
            match:
              domain: core
            destination:
              directory: "{purgeRoot}"
              fileName: "constitution"
              extension: ".md"
          - id: catch-all
            scope: both
            explicit: false
            order: 99
            match:
              domain: "*"
            destination:
              directory: "{purgeRoot}"
              fileName: "other"
              extension: ".md"
        fallback:
          mode: other-at-core-anchor
          fileBaseName: other
        purge:
          enabled: true
          roots:
            - "{purgeRoot}"
          globs:
            - "*.md"
        """;

    private static string MakeNoGlobLayoutYaml(string purgeRoot) => $"""
        version: "1.0"
        roots:
          globalRoot: "{purgeRoot}"
          projectRoot: "{purgeRoot}"
          targetRoot: "{purgeRoot}"
        routes:
          - id: core-anchor
            scope: both
            explicit: true
            anchor: core
            order: 10
            match:
              domain: core
            destination:
              directory: "{purgeRoot}"
              fileName: "constitution"
              extension: ".md"
          - id: catch-all
            scope: both
            explicit: false
            order: 99
            match:
              domain: "*"
            destination:
              directory: "{purgeRoot}"
              fileName: "other"
              extension: ".md"
        fallback:
          mode: other-at-core-anchor
          fileBaseName: other
        purge:
          enabled: true
          roots:
            - "{purgeRoot}"
          globs: []
        """;

    private static string MakeConfigYaml(
        string targetId,
        string globalRoot,
        string outputPath,
        string overridePath) => $"""
        globalRoot: "{globalRoot}"
        registeredTargets:
          - {targetId}
        targets:
          - id: {targetId}
            enabled: true
            outputPath: "{outputPath}"
            layoutOverridePath: "{overridePath}"
        """;

    // ── Purge removes matching files ─────────────────────────────────────────

    [Fact]
    public async Task Purge_WithGlobsAndRealFiles_RemovesMatchingFiles()
    {
        var purgeRoot = MakeTempDir("purge-root-");
        var configDir = MakeTempDir("purge-cfg-");
        var globalRoot = MakeTempDir("purge-global-");

        // Create generated files that should be purged
        var file1 = Path.Combine(purgeRoot, "constitution.md");
        var file2 = Path.Combine(purgeRoot, "api.md");
        var keepFile = Path.Combine(purgeRoot, "keep.txt");
        await File.WriteAllTextAsync(file1, "generated");
        await File.WriteAllTextAsync(file2, "generated");
        await File.WriteAllTextAsync(keepFile, "manual");

        var overridePath = Path.Combine(configDir, "speckit-override.yaml");
        await File.WriteAllTextAsync(overridePath, MakePurgeLayoutYaml(purgeRoot));

        var configPath = Path.Combine(configDir, "steergen.config.yaml");
        await File.WriteAllTextAsync(configPath, MakeConfigYaml("speckit", globalRoot, purgeRoot, overridePath));

        var exitCode = await PurgeCommand.RunAsync(
            configPath: configPath,
            explicitTargets: ["speckit"],
            dryRun: false,
            quiet: true);

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(file1), "constitution.md should have been purged.");
        Assert.False(File.Exists(file2), "api.md should have been purged.");
        Assert.True(File.Exists(keepFile), ".txt file should not be purged.");
    }

    // ── Dry-run does not delete ──────────────────────────────────────────────

    [Fact]
    public async Task Purge_DryRun_DoesNotRemoveFiles()
    {
        var purgeRoot = MakeTempDir("purge-dry-root-");
        var configDir = MakeTempDir("purge-dry-cfg-");
        var globalRoot = MakeTempDir("purge-dry-global-");

        var file = Path.Combine(purgeRoot, "constitution.md");
        await File.WriteAllTextAsync(file, "generated");

        var overridePath = Path.Combine(configDir, "speckit-override.yaml");
        await File.WriteAllTextAsync(overridePath, MakePurgeLayoutYaml(purgeRoot));

        var configPath = Path.Combine(configDir, "steergen.config.yaml");
        await File.WriteAllTextAsync(configPath, MakeConfigYaml("speckit", globalRoot, purgeRoot, overridePath));

        var exitCode = await PurgeCommand.RunAsync(
            configPath: configPath,
            explicitTargets: ["speckit"],
            dryRun: true,
            quiet: true);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(file), "Dry-run must not delete files.");
    }

    // ── No globs configured reports no-op ───────────────────────────────────

    [Fact]
    public async Task Purge_NoGlobsConfigured_ReportsNoOp_ExitCode0()
    {
        var purgeRoot = MakeTempDir("purge-noglob-root-");
        var configDir = MakeTempDir("purge-noglob-cfg-");
        var globalRoot = MakeTempDir("purge-noglob-global-");

        var file = Path.Combine(purgeRoot, "constitution.md");
        await File.WriteAllTextAsync(file, "content");

        var overridePath = Path.Combine(configDir, "speckit-override.yaml");
        await File.WriteAllTextAsync(overridePath, MakeNoGlobLayoutYaml(purgeRoot));

        var configPath = Path.Combine(configDir, "steergen.config.yaml");
        await File.WriteAllTextAsync(configPath, MakeConfigYaml("speckit", globalRoot, purgeRoot, overridePath));

        var exitCode = await PurgeCommand.RunAsync(
            configPath: configPath,
            explicitTargets: ["speckit"],
            dryRun: false,
            quiet: true);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(file), "File should NOT be removed when no globs are configured.");
    }

    // ── No manifest required ─────────────────────────────────────────────────

    [Fact]
    public async Task Purge_NoManifestRequired_WorksWithoutPriorRun()
    {
        var purgeRoot = MakeTempDir("purge-nomanifest-root-");
        var configDir = MakeTempDir("purge-nomanifest-cfg-");
        var globalRoot = MakeTempDir("purge-nomanifest-global-");

        // Create files directly without any prior `run` command
        var file = Path.Combine(purgeRoot, "stale.md");
        await File.WriteAllTextAsync(file, "stale generated content");

        var overridePath = Path.Combine(configDir, "speckit-override.yaml");
        await File.WriteAllTextAsync(overridePath, MakePurgeLayoutYaml(purgeRoot));

        var configPath = Path.Combine(configDir, "steergen.config.yaml");
        await File.WriteAllTextAsync(configPath, MakeConfigYaml("speckit", globalRoot, purgeRoot, overridePath));

        // Purge with no prior manifest (no manifest.json, no prior run)
        var exitCode = await PurgeCommand.RunAsync(
            configPath: configPath,
            explicitTargets: ["speckit"],
            dryRun: false,
            quiet: true);

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(file), "Stale file should be removed without requiring a prior manifest.");
    }

    // ── Target scoping: only requested target is purged ──────────────────────

    [Fact]
    public async Task Purge_TargetScoping_OnlyPurgesRequestedTarget()
    {
        var speckitRoot = MakeTempDir("purge-scoping-speckit-");
        var kiroRoot = MakeTempDir("purge-scoping-kiro-");
        var configDir = MakeTempDir("purge-scoping-cfg-");
        var globalRoot = MakeTempDir("purge-scoping-global-");

        var speckitFile = Path.Combine(speckitRoot, "constitution.md");
        var kiroFile = Path.Combine(kiroRoot, "spec.md");
        await File.WriteAllTextAsync(speckitFile, "speckit content");
        await File.WriteAllTextAsync(kiroFile, "kiro content");

        var speckitOverridePath = Path.Combine(configDir, "speckit-override.yaml");
        await File.WriteAllTextAsync(speckitOverridePath, MakePurgeLayoutYaml(speckitRoot));

        var kiroOverridePath = Path.Combine(configDir, "kiro-override.yaml");
        await File.WriteAllTextAsync(kiroOverridePath, MakePurgeLayoutYaml(kiroRoot));

        var configYaml = $"""
            globalRoot: "{globalRoot}"
            registeredTargets:
              - speckit
              - kiro
            targets:
              - id: speckit
                enabled: true
                outputPath: "{speckitRoot}"
                layoutOverridePath: "{speckitOverridePath}"
              - id: kiro
                enabled: true
                outputPath: "{kiroRoot}"
                layoutOverridePath: "{kiroOverridePath}"
            """;
        var configPath = Path.Combine(configDir, "steergen.config.yaml");
        await File.WriteAllTextAsync(configPath, configYaml);

        // Only purge speckit
        var exitCode = await PurgeCommand.RunAsync(
            configPath: configPath,
            explicitTargets: ["speckit"],
            dryRun: false,
            quiet: true);

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(speckitFile), "speckit file should be purged.");
        Assert.True(File.Exists(kiroFile), "kiro file should NOT be purged when scoped to speckit only.");
    }
}
