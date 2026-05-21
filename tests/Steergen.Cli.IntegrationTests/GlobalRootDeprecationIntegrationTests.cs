using Steergen.Cli.Commands;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]

/// <summary>
/// Integration tests for globalRoot deprecation (CFG001).
/// Validates: Requirement 8.2
///
/// When <c>globalRoot</c> is present in <c>steergen.config.yaml</c>,
/// <c>steergen run</c> must emit diagnostic CFG001 and exit with code 2.
/// </summary>
public sealed class GlobalRootDeprecationIntegrationTests : IDisposable
{
    private readonly string _workDir;

    public GlobalRootDeprecationIntegrationTests()
    {
        _workDir = Directory.CreateTempSubdirectory("globalroot-deprecation-").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
            Directory.Delete(_workDir, recursive: true);
    }

    [Fact]
    public async Task Run_WithGlobalRootInConfig_ReturnsExitCode2()
    {
        // Arrange — config file containing the deprecated globalRoot field
        var configPath = Path.Combine(_workDir, "steergen.config.yaml");
        await File.WriteAllTextAsync(configPath, """
            globalRoot: /some/old/global/rules
            projectRoot: ./steering
            registeredTargets:
              - speckit
            """);

        var outputDir = Path.Combine(_workDir, "output");
        Directory.CreateDirectory(outputDir);

        // Act
        var exitCode = await RunCommand.RunAsync(
            configPath: configPath,
            globalRoot: null,
            projectRoot: null,
            outputBase: outputDir,
            explicitTargets: [],
            quiet: true,
            cancellationToken: default);

        // Assert — CFG001 causes exit code 2
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task Run_WithGlobalRootInConfig_EmitsCFG001ToStderr()
    {
        // Arrange
        var configPath = Path.Combine(_workDir, "steergen.config.yaml");
        await File.WriteAllTextAsync(configPath, """
            globalRoot: /legacy/path
            projectRoot: ./steering
            """);

        var outputDir = Path.Combine(_workDir, "output");
        Directory.CreateDirectory(outputDir);

        // Capture stderr
        var originalStderr = Console.Error;
        using var stderrWriter = new StringWriter();
        Console.SetError(stderrWriter);

        try
        {
            // Act
            await RunCommand.RunAsync(
                configPath: configPath,
                globalRoot: null,
                projectRoot: null,
                outputBase: outputDir,
                explicitTargets: [],
                quiet: true,
                cancellationToken: default);

            var stderrOutput = stderrWriter.ToString();

            // Assert — stderr contains CFG001 diagnostic
            Assert.Contains("CFG001", stderrOutput);
            Assert.Contains("globalRoot", stderrOutput);
        }
        finally
        {
            Console.SetError(originalStderr);
        }
    }

    [Fact]
    public async Task Run_WithGlobalRootInConfig_DoesNotProduceOutputFiles()
    {
        // Arrange
        var configPath = Path.Combine(_workDir, "steergen.config.yaml");
        await File.WriteAllTextAsync(configPath, """
            globalRoot: /old/path
            projectRoot: ./steering
            registeredTargets:
              - kiro
            """);

        var outputDir = Path.Combine(_workDir, "output");
        Directory.CreateDirectory(outputDir);

        // Act
        await RunCommand.RunAsync(
            configPath: configPath,
            globalRoot: null,
            projectRoot: null,
            outputBase: outputDir,
            explicitTargets: [],
            quiet: true,
            cancellationToken: default);

        // Assert — no files generated because the command aborted early
        Assert.Empty(Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories));
    }
}
