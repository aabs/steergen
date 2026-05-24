using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Updates;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]
public sealed class RulesPackUpgradeCommandTests
{
    [Fact]
    public async Task Upgrade_LatestRefresh_UpdatesTargetedRulesPackPin()
    {
        var testDir = CreateTestDir();
        try
        {
            var configPath = Path.Combine(testDir, "steergen.config.yaml");
            var writer = new SteergenConfigWriter();
            await writer.WriteAsync(configPath, new SteeringConfiguration
            {
                RulesPacks =
                [
                    new RulesPackEntry
                    {
                        Source = "github:acme/security",
                        Path = "packs/security",
                        Ref = null,
                    },
                ],
            });

            var cachePath = Path.Combine(testDir, "cache", "rules-security");
            var service = CreateSuccessfulUpgradeService(cachePath);

            var exitCode = await RulesPackUpgradeCommand.ExecuteAsync(
                configPath,
                "github:acme/security|packs/security",
                tag: null,
                service: service);

            Assert.Equal(0, exitCode);

            var loader = new SteergenConfigLoader();
            var loaded = await loader.LoadAsync(configPath);
            Assert.Equal("HEAD", loaded.RulesPacks[0].Ref);
            Assert.NotNull(loaded.RulesPacks[0].Pin);
            Assert.Equal("HEAD", loaded.RulesPacks[0].Pin!.Tag);
            Assert.False(string.IsNullOrWhiteSpace(loaded.RulesPacks[0].Pin!.CommitSha));
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task Upgrade_ExplicitTag_UpdatesOnlyTargetedEntry()
    {
        var testDir = CreateTestDir();
        try
        {
            var configPath = Path.Combine(testDir, "steergen.config.yaml");
            var writer = new SteergenConfigWriter();
            await writer.WriteAsync(configPath, new SteeringConfiguration
            {
                TemplatePack = new TemplatePackConfig
                {
                    Source = "github:acme/templates",
                    EntryKey = "templates/default",
                    Ref = "v4.0.0",
                    Pin = new PackPin { Tag = "v4.0.0", CommitSha = "4444444444444444444444444444444444444444" },
                },
                RulesPacks =
                [
                    new RulesPackEntry
                    {
                        Source = "github:acme/security",
                        Path = "packs/security",
                        Ref = "v1.0.0",
                        Pin = new PackPin { Tag = "v1.0.0", CommitSha = "1111111111111111111111111111111111111111" },
                    },
                    new RulesPackEntry
                    {
                        Source = "github:acme/security",
                        Path = "packs/platform",
                        Ref = "v2.0.0",
                        Pin = new PackPin { Tag = "v2.0.0", CommitSha = "2222222222222222222222222222222222222222" },
                    },
                ],
            });

            var cachePath = Path.Combine(testDir, "cache", "rules-security");
            var service = CreateSuccessfulUpgradeService(cachePath);

            var exitCode = await RulesPackUpgradeCommand.ExecuteAsync(
                configPath,
                "github:acme/security|packs/security",
                tag: "v1.4.2",
                service: service);

            Assert.Equal(0, exitCode);

            var loader = new SteergenConfigLoader();
            var loaded = await loader.LoadAsync(configPath);

            Assert.Equal("v1.4.2", loaded.RulesPacks[0].Ref);
            Assert.Equal("v1.4.2", loaded.RulesPacks[0].Pin!.Tag);

            Assert.Equal("v2.0.0", loaded.RulesPacks[1].Ref);
            Assert.Equal("v2.0.0", loaded.RulesPacks[1].Pin!.Tag);

            Assert.NotNull(loaded.TemplatePack);
            Assert.Equal("v4.0.0", loaded.TemplatePack!.Ref);
            Assert.Equal("v4.0.0", loaded.TemplatePack!.Pin!.Tag);
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task Upgrade_InvalidSelectorFormat_ReturnsValidationExitCode()
    {
        var testDir = CreateTestDir();
        try
        {
            var configPath = await WriteRulesConfigAsync(testDir, new RulesPackEntry
            {
                Source = "github:acme/security",
                Path = "packs/security",
            });

            var service = CreateSuccessfulUpgradeService(Path.Combine(testDir, "cache"));
            var exitCode = await RulesPackUpgradeCommand.ExecuteAsync(
                configPath,
                "github:acme/security",
                tag: "v1.4.2",
                service: service);

            Assert.Equal(6, exitCode);
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task Upgrade_MissingSelector_ReturnsValidationExitCode()
    {
        var testDir = CreateTestDir();
        try
        {
            var configPath = await WriteRulesConfigAsync(testDir, new RulesPackEntry
            {
                Source = "github:acme/security",
                Path = "packs/security",
            });

            var service = CreateSuccessfulUpgradeService(Path.Combine(testDir, "cache"));
            var exitCode = await RulesPackUpgradeCommand.ExecuteAsync(
                configPath,
                "github:acme/security|packs/missing",
                tag: "v1.4.2",
                service: service);

            Assert.Equal(6, exitCode);
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task Upgrade_AmbiguousSelector_ReturnsValidationExitCode()
    {
        var testDir = CreateTestDir();
        try
        {
            var configPath = Path.Combine(testDir, "steergen.config.yaml");
            var writer = new SteergenConfigWriter();
            await writer.WriteAsync(configPath, new SteeringConfiguration
            {
                RulesPacks =
                [
                    new RulesPackEntry { Source = "github:acme/security", Path = "packs/security" },
                    new RulesPackEntry { Source = "github:acme/security", Path = "packs/security" },
                ],
            });

            var service = CreateSuccessfulUpgradeService(Path.Combine(testDir, "cache"));
            var exitCode = await RulesPackUpgradeCommand.ExecuteAsync(
                configPath,
                "github:acme/security|packs/security",
                tag: "v1.4.2",
                service: service);

            Assert.Equal(6, exitCode);
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    private static ExternalPackUpgradeService CreateSuccessfulUpgradeService(string cachePath)
    {
        return new ExternalPackUpgradeService(
            downloadAsync: (_, _, _, _) =>
            {
                Directory.CreateDirectory(cachePath);
                File.WriteAllText(Path.Combine(cachePath, "marker.txt"), "downloaded");
                return Task.FromResult(new PackDownloadResult
                {
                    Success = true,
                    CachePath = cachePath,
                });
            },
            getCachePath: (_, _) => cachePath);
    }

    private static async Task<string> WriteRulesConfigAsync(string testDir, RulesPackEntry entry)
    {
        var configPath = Path.Combine(testDir, "steergen.config.yaml");
        var writer = new SteergenConfigWriter();
        await writer.WriteAsync(configPath, new SteeringConfiguration
        {
            RulesPacks = [entry],
        });

        return configPath;
    }

    private static string CreateTestDir()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"rules-upgrade-cmd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        return testDir;
    }
}
