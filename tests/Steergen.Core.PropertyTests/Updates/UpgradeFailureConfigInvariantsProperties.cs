using CsCheck;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Updates;
using Steergen.Core.Validation;

namespace Steergen.Core.PropertyTests.Updates;

public sealed class UpgradeFailureConfigInvariantsProperties
{
    private static readonly Gen<string> GenTag =
        Gen.String[Gen.Char['a', 'z'], 3, 8].Select(v => $"v1.{v.Length}.0-{v}");

    [Fact]
    public void FailedUpgrade_NeverMutatesTargetedConfigReference()
    {
        GenTag.Sample(
            tag =>
            {
                var testDir = Path.Combine(Path.GetTempPath(), $"upgrade-prop-{Guid.NewGuid():N}");
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
                            Pin = new PackPin
                            {
                                Tag = "v1.0.0",
                                CommitSha = "1111111111111111111111111111111111111111",
                            },
                        },
                    ],
                }).GetAwaiter().GetResult();

                var cachePath = Path.Combine(testDir, "cache");
                Directory.CreateDirectory(cachePath);
                File.WriteAllText(Path.Combine(cachePath, "marker.txt"), "before");

                var service = new ExternalPackUpgradeService(
                    downloadAsync: (_, _, _, _) => Task.FromResult(new PackDownloadResult
                    {
                        Success = false,
                        Diagnostics = [new Diagnostic("DL001", "simulated", DiagnosticSeverity.Error)],
                    }),
                    getCachePath: (_, _) => cachePath);

                var result = service.UpgradeAsync(configPath, new ExternalPackUpgradeRequest(
                    UpgradePackKind.Rules,
                    "github:acme/security|packs/security",
                    tag)).GetAwaiter().GetResult();

                Assert.False(result.Success);

                var loader = new SteergenConfigLoader();
                var loaded = loader.LoadAsync(configPath).GetAwaiter().GetResult();
                Assert.Equal("v1.0.0", loaded.RulesPacks[0].Ref);
                Assert.Equal("v1.0.0", loaded.RulesPacks[0].Pin!.Tag);
                Assert.Equal("1111111111111111111111111111111111111111", loaded.RulesPacks[0].Pin!.CommitSha);

                Directory.Delete(testDir, recursive: true);
            },
            iter: 40,
            print: tag => $"tag={tag}");
    }
}
