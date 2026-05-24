using Steergen.Core.Configuration;
using Steergen.Core.Model;

namespace Steergen.Core.UnitTests.Configuration;

public sealed class SteergenConfigUpgradePinRoundTripTests
{
    [Fact]
    public async Task WriteRead_RoundTrip_PreservesRulesAndTemplatePinTuple()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"steergen-pin-rt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        var configPath = Path.Combine(testDir, "steergen.config.yaml");

        try
        {
            var config = new SteeringConfiguration
            {
                TemplatePack = new TemplatePackConfig
                {
                    Source = "github:acme/templates",
                    Ref = "v2.0.0",
                    EntryKey = "templates/default",
                    Pin = new PackPin
                    {
                        Tag = "v2.0.0",
                        CommitSha = "deafbeefdeafbeefdeafbeefdeafbeefdeafbeef",
                    },
                },
                RulesPacks =
                [
                    new RulesPackEntry
                    {
                        Source = "github:acme/security",
                        Path = "packs/security",
                        Ref = "v1.4.2",
                        Pin = new PackPin
                        {
                            Tag = "v1.4.2",
                            CommitSha = "cafe0000cafe0000cafe0000cafe0000cafe0000",
                        },
                    },
                ],
            };

            var writer = new SteergenConfigWriter();
            var loader = new SteergenConfigLoader();

            await writer.WriteAsync(configPath, config);
            var loaded = await loader.LoadAsync(configPath);

            var templatePack = loaded.TemplatePack;
            Assert.NotNull(templatePack);
            Assert.Equal("templates/default", templatePack!.EntryKey);

            var templatePin = templatePack.Pin;
            Assert.NotNull(templatePin);
            Assert.Equal("v2.0.0", templatePin!.Tag);
            Assert.Equal("deafbeefdeafbeefdeafbeefdeafbeefdeafbeef", templatePin.CommitSha);

            Assert.Single(loaded.RulesPacks);
            var rulesPin = loaded.RulesPacks[0].Pin;
            Assert.NotNull(rulesPin);
            Assert.Equal("v1.4.2", rulesPin!.Tag);
            Assert.Equal("cafe0000cafe0000cafe0000cafe0000cafe0000", rulesPin.CommitSha);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, recursive: true);
        }
    }
}
