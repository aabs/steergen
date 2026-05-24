using CsCheck;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Updates;

namespace Steergen.Core.PropertyTests.Updates;

public sealed class UpgradeDeterminismProperties
{
    private static readonly Gen<string> GenTag =
        Gen.String[Gen.Char['a', 'z'], 3, 8].Select(s => $"v2.{s.Length}.0-{s}");

    [Fact]
    public void ExplicitTagUpgrade_ConvergesToStablePinTuple()
    {
        GenTag.Sample(
            tag =>
            {
                var testDir = Path.Combine(Path.GetTempPath(), $"upgrade-determinism-{Guid.NewGuid():N}");
                Directory.CreateDirectory(testDir);

                var configPath = Path.Combine(testDir, "steergen.config.yaml");
                var writer = new SteergenConfigWriter();
                writer.WriteAsync(configPath, new SteeringConfiguration
                {
                    RulesPacks =
                    [
                        new RulesPackEntry
                        {
                            Source = "github:acme/security",
                            Path = "packs/security",
                            Ref = "v1.0.0",
                        },
                    ],
                }).GetAwaiter().GetResult();

                var cachePath = Path.Combine(testDir, "cache");
                var service = new ExternalPackUpgradeService(
                    downloadAsync: (_, _, _, _) =>
                    {
                        Directory.CreateDirectory(cachePath);
                        File.WriteAllText(Path.Combine(cachePath, "artifact.txt"), "ok");
                        return Task.FromResult(new PackDownloadResult { Success = true, CachePath = cachePath });
                    },
                    getCachePath: (_, _) => cachePath);

                var req = new ExternalPackUpgradeRequest(
                    UpgradePackKind.Rules,
                    "github:acme/security|packs/security",
                    tag);

                var r1 = service.UpgradeAsync(configPath, req).GetAwaiter().GetResult();
                var r2 = service.UpgradeAsync(configPath, req).GetAwaiter().GetResult();

                Assert.True(r1.Success);
                Assert.True(r2.Success);
                Assert.Equal(r1.FinalTag, r2.FinalTag);
                Assert.Equal(r1.FinalCommitSha, r2.FinalCommitSha);

                var loader = new SteergenConfigLoader();
                var loaded = loader.LoadAsync(configPath).GetAwaiter().GetResult();
                Assert.Equal(tag, loaded.RulesPacks[0].Pin!.Tag);
                Assert.Equal(r1.FinalCommitSha, loaded.RulesPacks[0].Pin!.CommitSha);

                Directory.Delete(testDir, recursive: true);
            },
            iter: 40,
            print: t => $"tag={t}");
    }
}
