using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Updates;
using Steergen.Core.Configuration;
using Steergen.Core.Validation;

namespace Steergen.Core.UnitTests.Updates;

public sealed class ExternalPackUpgradeServiceRollbackTests
{
    [Fact]
    public async Task UpgradeAsync_WhenFetchFails_RestoresCacheSnapshotAndLeavesConfigUnchanged()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"upgrade-rollback-{Guid.NewGuid():N}");
        var cachePath = Path.Combine(testDir, "cache", "rules");
        Directory.CreateDirectory(cachePath);
        await File.WriteAllTextAsync(Path.Combine(cachePath, "pre.txt"), "pre-upgrade");

        var configPath = Path.Combine(testDir, "steergen.config.yaml");
        Directory.CreateDirectory(testDir);
        var writer = new SteergenConfigWriter();
        await writer.WriteAsync(configPath, new SteeringConfiguration
        {
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

        try
        {
            var service = new ExternalPackUpgradeService(
                downloadAsync: (_, _, _, _) => Task.FromResult(new PackDownloadResult
                {
                    Success = false,
                    Diagnostics = [new Diagnostic("DL001", "simulated fetch failure", DiagnosticSeverity.Error)],
                }),
                getCachePath: (_, _) => cachePath);

            var result = await service.UpgradeAsync(configPath, new ExternalPackUpgradeRequest(
                UpgradePackKind.Rules,
                "github:acme/security|packs/security",
                "v2.0.0"));

            Assert.False(result.Success);
            Assert.True(result.RollbackPerformed);
            Assert.True(File.Exists(Path.Combine(cachePath, "pre.txt")));

            var loader = new SteergenConfigLoader();
            var config = await loader.LoadAsync(configPath);
            Assert.Equal("v1.0.0", config.RulesPacks[0].Ref);
            Assert.Equal("v1.0.0", config.RulesPacks[0].Pin!.Tag);
            Assert.Equal("1111111111111111111111111111111111111111", config.RulesPacks[0].Pin!.CommitSha);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, recursive: true);
        }
    }
}
