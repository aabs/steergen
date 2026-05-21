using Steergen.Core.Configuration;
using Steergen.Core.Validation;

namespace Steergen.Core.UnitTests.Configuration;

/// <summary>
/// Unit tests for globalRoot deprecation detection (CFG001).
/// Validates: Requirement 8.2
/// </summary>
public sealed class GlobalRootDeprecationTests : IDisposable
{
    private readonly string _testDir;

    public GlobalRootDeprecationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "GlobalRootDeprecation_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public async Task CheckForDeprecatedFields_WithGlobalRoot_ReturnsCFG001Error()
    {
        // Arrange
        var configPath = Path.Combine(_testDir, "steergen.config.yaml");
        await File.WriteAllTextAsync(configPath, """
            globalRoot: /some/path/to/global/rules
            projectRoot: ./steering
            """);

        var loader = new SteergenConfigLoader();

        // Act
        var diagnostic = await loader.CheckForDeprecatedFieldsAsync(configPath);

        // Assert
        Assert.NotNull(diagnostic);
        Assert.Equal("CFG001", diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("globalRoot", diagnostic.Message);
        Assert.Contains("removed", diagnostic.Message);
        Assert.Contains("rules packs", diagnostic.Message);
    }

    [Fact]
    public async Task CheckForDeprecatedFields_WithoutGlobalRoot_ReturnsNull()
    {
        // Arrange
        var configPath = Path.Combine(_testDir, "steergen.config.yaml");
        await File.WriteAllTextAsync(configPath, """
            projectRoot: ./steering
            generationRoot: .
            registeredTargets:
              - kiro
            """);

        var loader = new SteergenConfigLoader();

        // Act
        var diagnostic = await loader.CheckForDeprecatedFieldsAsync(configPath);

        // Assert
        Assert.Null(diagnostic);
    }

    [Fact]
    public async Task CheckForDeprecatedFields_WithGlobalRootAndOtherFields_StillReturnsCFG001()
    {
        // Arrange — globalRoot alongside valid pack configuration
        var configPath = Path.Combine(_testDir, "steergen.config.yaml");
        await File.WriteAllTextAsync(configPath, """
            globalRoot: /old/global/path
            projectRoot: ./steering
            templatePack:
              source: "github:acme/templates"
              ref: "v1.0.0"
            rulesPacks:
              - source: "github:acme/rules"
                ref: "v2.0.0"
                scope: global
            """);

        var loader = new SteergenConfigLoader();

        // Act
        var diagnostic = await loader.CheckForDeprecatedFieldsAsync(configPath);

        // Assert
        Assert.NotNull(diagnostic);
        Assert.Equal("CFG001", diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }
}
