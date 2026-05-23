using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Updates;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]
public sealed class TemplatePackUpgradeCommandTests
{
    [Fact]
    public async Task Upgrade_LatestRefresh_UpdatesTemplatePackPin()
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
                    Ref = null,
                },
            });

            var cachePath = Path.Combine(testDir, "cache", "templates");
            var service = CreateSuccessfulUpgradeService(cachePath);

            var exitCode = await TemplatePackUpgradeCommand.ExecuteAsync(
                configPath,
                "github:acme/templates|templates/default",
                tag: null,
                service: service);

            Assert.Equal(0, exitCode);

            var loader = new SteergenConfigLoader();
            var loaded = await loader.LoadAsync(configPath);
            Assert.NotNull(loaded.TemplatePack);
            Assert.Equal("HEAD", loaded.TemplatePack!.Ref);
            Assert.Equal("HEAD", loaded.TemplatePack!.Pin!.Tag);
            Assert.False(string.IsNullOrWhiteSpace(loaded.TemplatePack!.Pin!.CommitSha));
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task Upgrade_ExplicitTag_KeepsUnrelatedReferencesUnchanged()
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
                ],
            });

            var cachePath = Path.Combine(testDir, "cache", "templates");
            var service = CreateSuccessfulUpgradeService(cachePath);

            var exitCode = await TemplatePackUpgradeCommand.ExecuteAsync(
                configPath,
                "github:acme/templates|templates/default",
                tag: "v5.0.1",
                service: service);

            Assert.Equal(0, exitCode);

            var loader = new SteergenConfigLoader();
            var loaded = await loader.LoadAsync(configPath);

            Assert.NotNull(loaded.TemplatePack);
            Assert.Equal("v5.0.1", loaded.TemplatePack!.Ref);
            Assert.Equal("v5.0.1", loaded.TemplatePack!.Pin!.Tag);

            Assert.Single(loaded.RulesPacks);
            Assert.Equal("v1.0.0", loaded.RulesPacks[0].Ref);
            Assert.Equal("v1.0.0", loaded.RulesPacks[0].Pin!.Tag);
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
            var configPath = await WriteTemplateConfigAsync(testDir, source: "github:acme/templates", entryKey: "templates/default");
            var service = CreateSuccessfulUpgradeService(Path.Combine(testDir, "cache"));

            var exitCode = await TemplatePackUpgradeCommand.ExecuteAsync(
                configPath,
                "github:acme/templates",
                tag: "v5.0.1",
                service: service);

            Assert.Equal(6, exitCode);
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task Upgrade_MissingTemplateSelector_ReturnsValidationExitCode()
    {
        var testDir = CreateTestDir();
        try
        {
            var configPath = await WriteTemplateConfigAsync(testDir, source: "github:acme/templates", entryKey: "templates/default");
            var service = CreateSuccessfulUpgradeService(Path.Combine(testDir, "cache"));

            var exitCode = await TemplatePackUpgradeCommand.ExecuteAsync(
                configPath,
                "github:acme/templates|templates/missing",
                tag: "v5.0.1",
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

    private static async Task<string> WriteTemplateConfigAsync(string testDir, string source, string entryKey)
    {
        var configPath = Path.Combine(testDir, "steergen.config.yaml");
        var writer = new SteergenConfigWriter();
        await writer.WriteAsync(configPath, new SteeringConfiguration
        {
            TemplatePack = new TemplatePackConfig
            {
                Source = source,
                EntryKey = entryKey,
            },
        });
        return configPath;
    }

    private static string CreateTestDir()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"template-upgrade-cmd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        return testDir;
    }
}
