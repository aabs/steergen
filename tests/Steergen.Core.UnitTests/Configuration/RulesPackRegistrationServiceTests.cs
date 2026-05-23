using Steergen.Core.Configuration;
using Steergen.Core.Model;

namespace Steergen.Core.UnitTests.Configuration;

public sealed class RulesPackRegistrationServiceTests
{
    [Fact]
    public async Task UpdatePinBySelectorAsync_UpdatesOnlyTargetedEntry()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"rules-upgrade-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        var configPath = Path.Combine(testDir, "steergen.config.yaml");

        try
        {
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
                    new RulesPackEntry
                    {
                        Source = "github:acme/security",
                        Path = "packs/platform",
                        Ref = "v2.0.0",
                        Pin = new PackPin { Tag = "v2.0.0", CommitSha = "2222222222222222222222222222222222222222" },
                    },
                ],
            });

            var resolver = new PackSelectorResolver();
            resolver.TryParse("github:acme/security|packs/security", out var selector, out _);

            var service = new RulesPackRegistrationService();
            var result = await service.UpdatePinBySelectorAsync(
                configPath,
                selector,
                "v1.1.0",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            Assert.True(result.Success);

            var loader = new SteergenConfigLoader();
            var config = await loader.LoadAsync(configPath);
            Assert.Equal("v1.1.0", config.RulesPacks[0].Ref);
            Assert.Equal("v1.1.0", config.RulesPacks[0].Pin!.Tag);
            Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", config.RulesPacks[0].Pin!.CommitSha);

            Assert.Equal("v2.0.0", config.RulesPacks[1].Ref);
            Assert.Equal("v2.0.0", config.RulesPacks[1].Pin!.Tag);
            Assert.Equal("2222222222222222222222222222222222222222", config.RulesPacks[1].Pin!.CommitSha);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task UpdatePinBySelectorAsync_MissingSelector_ReturnsFailure()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"rules-upgrade-miss-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        var configPath = Path.Combine(testDir, "steergen.config.yaml");

        try
        {
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
                    },
                ],
            });

            var resolver = new PackSelectorResolver();
            resolver.TryParse("github:acme/security|packs/unknown", out var selector, out _);

            var service = new RulesPackRegistrationService();
            var result = await service.UpdatePinBySelectorAsync(
                configPath,
                selector,
                "v1.1.0",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            Assert.False(result.Success);
            Assert.Contains("does not match", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, recursive: true);
        }
    }
}
