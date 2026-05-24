using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Updates;
using Steergen.Core.Validation;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]
public sealed class PackUpgradeRollbackTests
{
    private sealed class ThrowingSnapshotStore : PackCacheSnapshotStore
    {
        public override Task RestoreAsync(string snapshotPath, string cachePath, CancellationToken cancellationToken = default)
        {
            throw new IOException("simulated restore failure");
        }
    }

    [Fact]
    public async Task RulesPackUpgrade_FetchFailure_PerformsRollbackAndReturnsExecutionExit()
    {
        var testDir = CreateTestDir();
        try
        {
            var configPath = await WriteRulesConfigAsync(testDir);
            var cachePath = Path.Combine(testDir, "cache");
            Directory.CreateDirectory(cachePath);
            await File.WriteAllTextAsync(Path.Combine(cachePath, "before.txt"), "before");

            var service = new ExternalPackUpgradeService(
                downloadAsync: (_, _, _, _) => Task.FromResult(new PackDownloadResult
                {
                    Success = false,
                    Diagnostics = [new Diagnostic("DL001", "fetch failed", DiagnosticSeverity.Error)],
                }),
                getCachePath: (_, _) => cachePath);

            var exitCode = await RulesPackUpgradeCommand.ExecuteAsync(
                configPath,
                "github:acme/security|packs/security",
                "v2.0.0",
                service);

            Assert.Equal(7, exitCode);
            Assert.True(File.Exists(Path.Combine(cachePath, "before.txt")));
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task RulesPackUpgrade_RollbackFailure_ReturnsRollbackExitAndDualDiagnostics()
    {
        var testDir = CreateTestDir();
        var originalError = Console.Error;
        using var stderr = new StringWriter();
        Console.SetError(stderr);

        try
        {
            var configPath = await WriteRulesConfigAsync(testDir);
            var cachePath = Path.Combine(testDir, "cache");
            Directory.CreateDirectory(cachePath);
            await File.WriteAllTextAsync(Path.Combine(cachePath, "before.txt"), "before");

            var service = new ExternalPackUpgradeService(
                snapshotStore: new ThrowingSnapshotStore(),
                downloadAsync: (_, _, _, _) => Task.FromResult(new PackDownloadResult
                {
                    Success = false,
                    Diagnostics = [new Diagnostic("DL001", "fetch failed", DiagnosticSeverity.Error)],
                }),
                getCachePath: (_, _) => cachePath);

            var exitCode = await RulesPackUpgradeCommand.ExecuteAsync(
                configPath,
                "github:acme/security|packs/security",
                "v2.0.0",
                service);

            Assert.Equal(8, exitCode);

            var output = stderr.ToString();
            Assert.Contains("DL001", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("UPG002", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetError(originalError);
            Directory.Delete(testDir, recursive: true);
        }
    }

    private static async Task<string> WriteRulesConfigAsync(string testDir)
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
        return configPath;
    }

    private static string CreateTestDir()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"pack-upgrade-rollback-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        return testDir;
    }
}
