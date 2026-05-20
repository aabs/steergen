using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]
/// <summary>
/// Integration tests for rules pack CLI commands:
/// <c>steergen rules-pack add</c>, <c>steergen rules-pack remove</c>,
/// <c>steergen rules-pack list</c>, <c>steergen update --rules</c>,
/// and <c>steergen run</c> with rules packs configured.
/// </summary>
public sealed class RulesPackCommandsTests
{
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "steergen-rp-test-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task<string> WriteConfigAsync(
        string dir,
        string? projectRoot = null,
        IReadOnlyList<RulesPackEntry>? rulesPacks = null,
        IReadOnlyList<string>? registeredTargets = null)
    {
        var path = Path.Combine(dir, "steergen.config.yaml");
        var config = new SteeringConfiguration
        {
            ProjectRoot = projectRoot,
            RulesPacks = rulesPacks ?? [],
            RegisteredTargets = registeredTargets?.ToList() ?? [],
        };
        var writer = new SteergenConfigWriter();
        await writer.WriteAsync(path, config);
        return path;
    }

    private static string GetCacheBaseDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".steergen");
    }

    /// <summary>
    /// Creates a fake rules pack in the local cache directory for testing.
    /// Returns the cache path where the pack was created.
    /// </summary>
    private static string CreateFakeRulesPackInCache(
        string owner,
        string repo,
        string refValue,
        PackScope scope,
        string packName = "test-rules-pack",
        string? rulesContent = null)
    {
        var cacheBase = GetCacheBaseDirectory();
        var cachePath = Path.Combine(cacheBase, "rules", owner, repo, refValue);
        Directory.CreateDirectory(cachePath);

        // Write pack.yaml manifest
        var manifest = $"""
            name: "{packName}"
            version: "1.0.0"
            minSteergenVersion: "0.1.0"
            scope: {scope.ToString().ToLowerInvariant()}
            """;
        File.WriteAllText(Path.Combine(cachePath, "pack.yaml"), manifest);

        // Write a sample rules document
        var rules = rulesContent ?? """
            ---
            id: test-rules-doc
            version: "1.0.0"
            title: Test Rules
            scope: global
            status: active
            ---

            # Test Rules

            :::rule id="TEST-001" mandatory="true" category="testing"
            All code must have tests. This is a test rule from a rules pack.
            :::
            """;
        File.WriteAllText(Path.Combine(cachePath, "test-rules.md"), rules);

        return cachePath;
    }

    /// <summary>
    /// Removes a fake rules pack from the local cache directory.
    /// </summary>
    private static void RemoveFakeRulesPackFromCache(string owner, string repo, string refValue)
    {
        var cacheBase = GetCacheBaseDirectory();
        var cachePath = Path.Combine(cacheBase, "rules", owner, repo, refValue);
        if (Directory.Exists(cachePath))
            Directory.Delete(cachePath, recursive: true);

        // Clean up empty parent directories
        var repoDir = Path.Combine(cacheBase, "rules", owner, repo);
        if (Directory.Exists(repoDir) && !Directory.EnumerateFileSystemEntries(repoDir).Any())
            Directory.Delete(repoDir);

        var ownerDir = Path.Combine(cacheBase, "rules", owner);
        if (Directory.Exists(ownerDir) && !Directory.EnumerateFileSystemEntries(ownerDir).Any())
            Directory.Delete(ownerDir);
    }

    // ── rules-pack add ───────────────────────────────────────────────────────

    [Fact]
    public async Task RulesPackAdd_InvalidSourceFormat_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);
            var exitCode = await RulesPackAddCommand.ExecuteAsync(
                configPath, "invalid-format", refValue: null, path: null, scopeStr: null);

            Assert.Equal(2, exitCode);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task RulesPackAdd_InvalidScope_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);
            var exitCode = await RulesPackAddCommand.ExecuteAsync(
                configPath, "github:owner/repo", refValue: "v1.0.0", path: null, scopeStr: "invalid-scope");

            Assert.Equal(2, exitCode);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task RulesPackAdd_ValidSource_PersistsToConfig()
    {
        // This test uses a fake cached pack to avoid network calls during add.
        // The add command will attempt to download, which will fail for a non-existent repo.
        // We test the config persistence path by pre-caching the pack.
        var dir = CreateTempDir();
        var owner = "test-rp-add-owner";
        var repo = "test-rp-add-repo";
        var refValue = "v1.0.0";

        try
        {
            CreateFakeRulesPackInCache(owner, repo, refValue, PackScope.Global);
            var configPath = await WriteConfigAsync(dir);

            // The add command downloads first — since we pre-cached, it will still try to download
            // from GitHub and fail. Instead, test the registration service directly.
            var entry = new RulesPackEntry
            {
                Source = $"github:{owner}/{repo}",
                Ref = refValue,
                Scope = PackScope.Global,
            };
            var svc = new RulesPackRegistrationService();
            var result = await svc.AddAsync(configPath, entry);

            Assert.True(result.Success);

            var loader = new SteergenConfigLoader();
            var config = await loader.LoadAsync(configPath);
            Assert.Single(config.RulesPacks);
            Assert.Equal($"github:{owner}/{repo}", config.RulesPacks[0].Source);
            Assert.Equal(refValue, config.RulesPacks[0].Ref);
            Assert.Equal(PackScope.Global, config.RulesPacks[0].Scope);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            RemoveFakeRulesPackFromCache(owner, repo, refValue);
        }
    }

    [Fact]
    public async Task RulesPackAdd_WithPathAndScope_PersistsAllFieldsToConfig()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);

            var entry = new RulesPackEntry
            {
                Source = "github:acme/rules",
                Ref = "abc123def456789012345678901234567890abcd",
                Path = "backend-team",
                Scope = PackScope.Supplemental,
            };
            var svc = new RulesPackRegistrationService();
            var result = await svc.AddAsync(configPath, entry);

            Assert.True(result.Success);

            var loader = new SteergenConfigLoader();
            var config = await loader.LoadAsync(configPath);
            Assert.Single(config.RulesPacks);
            Assert.Equal("github:acme/rules", config.RulesPacks[0].Source);
            Assert.Equal("abc123def456789012345678901234567890abcd", config.RulesPacks[0].Ref);
            Assert.Equal("backend-team", config.RulesPacks[0].Path);
            Assert.Equal(PackScope.Supplemental, config.RulesPacks[0].Scope);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task RulesPackAdd_DuplicateSource_DoesNotDuplicate()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);

            var entry = new RulesPackEntry
            {
                Source = "github:acme/rules",
                Ref = "v1.0.0",
            };
            var svc = new RulesPackRegistrationService();
            await svc.AddAsync(configPath, entry);
            var secondResult = await svc.AddAsync(configPath, entry);

            Assert.True(secondResult.Success);
            Assert.True(secondResult.WasAlreadyPresent);

            var loader = new SteergenConfigLoader();
            var config = await loader.LoadAsync(configPath);
            Assert.Single(config.RulesPacks);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task RulesPackAdd_MissingConfigFile_ReturnsFailure()
    {
        var svc = new RulesPackRegistrationService();
        var entry = new RulesPackEntry { Source = "github:owner/repo" };
        var result = await svc.AddAsync("/nonexistent/steergen.config.yaml", entry);

        Assert.False(result.Success);
    }

    // ── rules-pack remove ────────────────────────────────────────────────────

    [Fact]
    public async Task RulesPackRemove_ExistingEntry_RemovesFromConfig()
    {
        var dir = CreateTempDir();
        try
        {
            var rulesPacks = new List<RulesPackEntry>
            {
                new() { Source = "github:acme/baseline-rules", Ref = "v1.0.0", Scope = PackScope.Global },
                new() { Source = "github:acme/team-rules", Ref = "v2.0.0", Scope = PackScope.Supplemental },
            };
            var configPath = await WriteConfigAsync(dir, rulesPacks: rulesPacks);

            var exitCode = await RulesPackRemoveCommand.ExecuteAsync(
                configPath, "github:acme/baseline-rules");

            Assert.Equal(0, exitCode);

            var loader = new SteergenConfigLoader();
            var config = await loader.LoadAsync(configPath);
            Assert.Single(config.RulesPacks);
            Assert.Equal("github:acme/team-rules", config.RulesPacks[0].Source);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task RulesPackRemove_NotPresent_ReturnsExitCode0Idempotently()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);
            var exitCode = await RulesPackRemoveCommand.ExecuteAsync(
                configPath, "github:nonexistent/repo");

            Assert.Equal(0, exitCode);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task RulesPackRemove_MissingConfigFile_ReturnsExitCode2()
    {
        var exitCode = await RulesPackRemoveCommand.ExecuteAsync(
            "/nonexistent/steergen.config.yaml", "github:owner/repo");

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RulesPackRemove_LeavesOtherEntriesIntact()
    {
        var dir = CreateTempDir();
        try
        {
            var rulesPacks = new List<RulesPackEntry>
            {
                new() { Source = "github:acme/security-rules", Ref = "v1.0.0", Scope = PackScope.Global },
                new() { Source = "github:acme/team-rules", Ref = "v2.0.0", Scope = PackScope.Supplemental },
                new() { Source = "github:acme/project-rules", Ref = "v3.0.0", Scope = PackScope.Project },
            };
            var configPath = await WriteConfigAsync(dir, rulesPacks: rulesPacks);

            await RulesPackRemoveCommand.ExecuteAsync(configPath, "github:acme/team-rules");

            var loader = new SteergenConfigLoader();
            var config = await loader.LoadAsync(configPath);
            Assert.Equal(2, config.RulesPacks.Count);
            Assert.Contains(config.RulesPacks, r => r.Source == "github:acme/security-rules");
            Assert.Contains(config.RulesPacks, r => r.Source == "github:acme/project-rules");
            Assert.DoesNotContain(config.RulesPacks, r => r.Source == "github:acme/team-rules");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── rules-pack list ──────────────────────────────────────────────────────

    [Fact]
    public async Task RulesPackList_NoPacksConfigured_ReturnsExitCode0()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);
            var exitCode = await RulesPackListCommand.RunAsync(configPath);

            Assert.Equal(0, exitCode);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task RulesPackList_WithConfiguredPacks_ReturnsExitCode0()
    {
        var dir = CreateTempDir();
        try
        {
            var rulesPacks = new List<RulesPackEntry>
            {
                new() { Source = "github:acme/baseline-rules", Ref = "v1.0.0", Scope = PackScope.Global },
                new() { Source = "github:acme/team-rules", Ref = "v2.0.0", Scope = PackScope.Supplemental },
            };
            var configPath = await WriteConfigAsync(dir, rulesPacks: rulesPacks);

            var exitCode = await RulesPackListCommand.RunAsync(configPath);

            Assert.Equal(0, exitCode);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task RulesPackList_MissingConfigFile_ReturnsExitCode2()
    {
        var exitCode = await RulesPackListCommand.RunAsync("/nonexistent/steergen.config.yaml");
        Assert.Equal(2, exitCode);
    }

    // ── update --rules ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateRules_NoRulesPacksConfigured_ReturnsExitCode0()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = await WriteConfigAsync(dir);
            var result = await UpdateCommand.RunRulesUpdateAsync(configPath, force: false);
            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task UpdateRules_MissingConfigFile_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var configPath = Path.Combine(dir, "does-not-exist.yaml");
            var result = await UpdateCommand.RunRulesUpdateAsync(configPath, force: false);
            Assert.Equal(2, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task UpdateRules_InvalidSourceFormat_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var rulesPacks = new List<RulesPackEntry>
            {
                new() { Source = "not-a-valid-source" },
            };
            var configPath = await WriteConfigAsync(dir, rulesPacks: rulesPacks);
            var result = await UpdateCommand.RunRulesUpdateAsync(configPath, force: false);
            Assert.Equal(2, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task UpdateRules_UnreachableGitHubSource_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var rulesPacks = new List<RulesPackEntry>
            {
                new() { Source = "github:nonexistent-xyz-owner/nonexistent-xyz-repo", Ref = "v1.0.0" },
            };
            var configPath = await WriteConfigAsync(dir, rulesPacks: rulesPacks);
            var result = await UpdateCommand.RunRulesUpdateAsync(configPath, force: false);
            Assert.Equal(2, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── steergen run with rules packs ────────────────────────────────────────

    [Fact]
    public async Task Run_WithCachedRulesPack_MergesRulesIntoOutput()
    {
        var dir = CreateTempDir();
        var owner = "test-rp-run-owner";
        var repo = "test-rp-run-repo";
        var refValue = "v1.0.0";

        try
        {
            // Set up a fake rules pack in the cache with a known rule
            var rulesContent = """
                ---
                id: pack-rules-doc
                version: "1.0.0"
                title: Pack Rules
                scope: global
                status: active
                ---

                # Pack Rules

                :::rule id="PACK-001" mandatory="true" category="governance"
                All services must implement health check endpoints. This rule comes from a rules pack.
                :::
                """;
            CreateFakeRulesPackInCache(owner, repo, refValue, PackScope.Global, "integration-test-pack", rulesContent);

            // Set up project with a local steering document
            var projectDir = Path.Combine(dir, "steering", "project");
            Directory.CreateDirectory(projectDir);
            var projectDoc = """
                ---
                id: project-rules-doc
                version: "1.0.0"
                title: Project Rules
                scope: project
                status: active
                ---

                # Project Rules

                :::rule id="PROJ-001" mandatory="true" category="testing"
                All code must have unit tests covering critical paths.
                :::
                """;
            File.WriteAllText(Path.Combine(projectDir, "project-rules.md"), projectDoc);

            // Write config referencing the rules pack
            var rulesPacks = new List<RulesPackEntry>
            {
                new() { Source = $"github:{owner}/{repo}", Ref = refValue, Scope = PackScope.Global },
            };
            var configPath = await WriteConfigAsync(dir, projectRoot: projectDir, rulesPacks: rulesPacks, registeredTargets: ["speckit"]);

            var outputDir = Path.Combine(dir, "output");
            Directory.CreateDirectory(outputDir);

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);

            // Verify output was generated (speckit target produces files)
            var generatedFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
            Assert.True(generatedFiles.Length > 0, "Expected generated output files from speckit target");

            // Verify that the rules pack rule (PACK-001) appears in the generated output
            var allContent = string.Join("\n", generatedFiles.Select(File.ReadAllText));
            Assert.Contains("PACK-001", allContent);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            RemoveFakeRulesPackFromCache(owner, repo, refValue);
        }
    }

    [Fact]
    public async Task Run_WithRulesPackNotInCache_ReturnsExitCode2()
    {
        var dir = CreateTempDir();
        try
        {
            var projectDir = Path.Combine(dir, "steering", "project");
            Directory.CreateDirectory(projectDir);
            var projectDoc = """
                ---
                id: project-doc
                version: "1.0.0"
                title: Project Rules
                scope: project
                status: active
                ---

                # Project Rules

                :::rule id="PROJ-001" mandatory="true" category="testing"
                All code must have tests.
                :::
                """;
            File.WriteAllText(Path.Combine(projectDir, "project-rules.md"), projectDoc);

            // Reference a rules pack that is NOT in the cache
            var rulesPacks = new List<RulesPackEntry>
            {
                new() { Source = "github:nonexistent-cache-owner/nonexistent-cache-repo", Ref = "v9.9.9", Scope = PackScope.Global },
            };
            var configPath = await WriteConfigAsync(dir, projectRoot: projectDir, rulesPacks: rulesPacks, registeredTargets: ["speckit"]);

            var outputDir = Path.Combine(dir, "output");
            Directory.CreateDirectory(outputDir);

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            // RP005 error: pack not in cache → exit code 2
            Assert.Equal(2, exitCode);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Run_WithMultipleRulesPacks_MergesWithScopePrecedence()
    {
        var dir = CreateTempDir();
        var globalOwner = "test-rp-global-owner";
        var globalRepo = "test-rp-global-repo";
        var globalRef = "v1.0.0";
        var suppOwner = "test-rp-supp-owner";
        var suppRepo = "test-rp-supp-repo";
        var suppRef = "v2.0.0";

        try
        {
            // Create a global-scoped rules pack
            var globalRules = """
                ---
                id: global-pack-doc
                version: "1.0.0"
                title: Global Baseline Rules
                scope: global
                status: active
                ---

                # Global Baseline Rules

                :::rule id="GLOBAL-001" mandatory="true" category="governance"
                All services must have documentation.
                :::
                """;
            CreateFakeRulesPackInCache(globalOwner, globalRepo, globalRef, PackScope.Global, "global-baseline", globalRules);

            // Create a supplemental-scoped rules pack
            var suppRules = """
                ---
                id: supp-pack-doc
                version: "1.0.0"
                title: Supplemental Team Rules
                scope: supplemental
                status: active
                ---

                # Supplemental Team Rules

                :::rule id="SUPP-001" mandatory="true" category="quality"
                Code coverage must exceed 80 percent for all services.
                :::
                """;
            CreateFakeRulesPackInCache(suppOwner, suppRepo, suppRef, PackScope.Supplemental, "team-supplemental", suppRules);

            // Set up project with a local steering document
            var projectDir = Path.Combine(dir, "steering", "project");
            Directory.CreateDirectory(projectDir);
            var projectDoc = """
                ---
                id: local-project-doc
                version: "1.0.0"
                title: Local Project Rules
                scope: project
                status: active
                ---

                # Local Project Rules

                :::rule id="LOCAL-001" mandatory="true" category="testing"
                Integration tests must cover all API endpoints.
                :::
                """;
            File.WriteAllText(Path.Combine(projectDir, "local-rules.md"), projectDoc);

            // Write config referencing both rules packs
            var rulesPacks = new List<RulesPackEntry>
            {
                new() { Source = $"github:{globalOwner}/{globalRepo}", Ref = globalRef, Scope = PackScope.Global },
                new() { Source = $"github:{suppOwner}/{suppRepo}", Ref = suppRef, Scope = PackScope.Supplemental },
            };
            var configPath = await WriteConfigAsync(dir, projectRoot: projectDir, rulesPacks: rulesPacks, registeredTargets: ["speckit"]);

            var outputDir = Path.Combine(dir, "output");
            Directory.CreateDirectory(outputDir);

            var exitCode = await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: ["speckit"],
                quiet: true,
                cancellationToken: default);

            Assert.Equal(0, exitCode);

            // Verify all rules from all sources appear in the output
            var generatedFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
            Assert.True(generatedFiles.Length > 0, "Expected generated output files");

            var allContent = string.Join("\n", generatedFiles.Select(File.ReadAllText));
            Assert.Contains("GLOBAL-001", allContent);
            Assert.Contains("SUPP-001", allContent);
            Assert.Contains("LOCAL-001", allContent);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            RemoveFakeRulesPackFromCache(globalOwner, globalRepo, globalRef);
            RemoveFakeRulesPackFromCache(suppOwner, suppRepo, suppRef);
        }
    }
}
