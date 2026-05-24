using System.Diagnostics;
using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Updates;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]
public sealed class PackUpgradePerformanceTests
{
    [Fact]
    public async Task RulesPackUpgrade_P95_ForSimulatedLargePayload_IsUnderSixtySeconds()
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
                        Ref = "v1.0.0",
                    },
                ],
            });

            var cachePath = Path.Combine(testDir, "cache", "rules");
            var service = CreatePerfService(cachePath, simulatedBytes: 10 * 1024 * 1024);

            var samples = new List<double>(12);
            for (var i = 0; i < 12; i++)
            {
                var sw = Stopwatch.StartNew();
                var exitCode = await RulesPackUpgradeCommand.ExecuteAsync(
                    configPath,
                    "github:acme/security|packs/security",
                    $"v2.0.{i}",
                    service);
                sw.Stop();

                Assert.Equal(0, exitCode);
                samples.Add(sw.Elapsed.TotalSeconds);
            }

            var p95 = Percentile(samples, 0.95);
            Assert.True(p95 <= 60.0, $"Expected p95 <= 60s, actual {p95:F3}s");
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task TemplatePackUpgrade_P95_ForSimulatedLargePayload_IsUnderSixtySeconds()
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
                    Ref = "v1.0.0",
                },
            });

            var cachePath = Path.Combine(testDir, "cache", "templates");
            var service = CreatePerfService(cachePath, simulatedBytes: 10 * 1024 * 1024);

            var samples = new List<double>(12);
            for (var i = 0; i < 12; i++)
            {
                var sw = Stopwatch.StartNew();
                var exitCode = await TemplatePackUpgradeCommand.ExecuteAsync(
                    configPath,
                    "github:acme/templates|templates/default",
                    $"v3.1.{i}",
                    service);
                sw.Stop();

                Assert.Equal(0, exitCode);
                samples.Add(sw.Elapsed.TotalSeconds);
            }

            var p95 = Percentile(samples, 0.95);
            Assert.True(p95 <= 60.0, $"Expected p95 <= 60s, actual {p95:F3}s");
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    private static ExternalPackUpgradeService CreatePerfService(string cachePath, int simulatedBytes)
    {
        return new ExternalPackUpgradeService(
            downloadAsync: (_, _, _, _) =>
            {
                Directory.CreateDirectory(cachePath);
                var payloadPath = Path.Combine(cachePath, "payload.bin");
                if (!File.Exists(payloadPath) || new FileInfo(payloadPath).Length != simulatedBytes)
                {
                    File.WriteAllBytes(payloadPath, new byte[simulatedBytes]);
                }

                return Task.FromResult(new PackDownloadResult
                {
                    Success = true,
                    CachePath = cachePath,
                });
            },
            getCachePath: (_, _) => cachePath);
    }

    private static double Percentile(IReadOnlyList<double> values, double p)
    {
        if (values.Count == 0)
            return 0;

        var sorted = values.OrderBy(v => v).ToArray();
        var index = (int)Math.Ceiling(sorted.Length * p) - 1;
        index = Math.Clamp(index, 0, sorted.Length - 1);
        return sorted[index];
    }

    private static string CreateTestDir()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"pack-upgrade-perf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        return testDir;
    }
}
