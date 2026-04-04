using Steergen.Core.Model;

namespace Steergen.Core.Configuration;

/// <summary>
/// Adds and removes targets from the <c>registeredTargets</c> list in a steergen config file.
/// Uses optimistic locking to detect concurrent modifications.
/// </summary>
public sealed class TargetRegistrationService
{
    private readonly SteergenConfigLoader _loader = new();
    private readonly SteergenConfigWriter _writer = new();

    public async Task<TargetRegistrationResult> AddAsync(
        string configPath,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configPath))
            return TargetRegistrationResult.Fail($"Config file not found: {configPath}");

        var (config, hash) = await ReadWithHash(configPath, cancellationToken);

        if (config.RegisteredTargets.Contains(targetId, StringComparer.Ordinal))
            return TargetRegistrationResult.AlreadyPresent(targetId);

        var updated = config with
        {
            RegisteredTargets = [.. config.RegisteredTargets, targetId],
        };

        await _writer.WriteAsync(configPath, updated, hash, cancellationToken);
        return TargetRegistrationResult.Added(targetId);
    }

    public async Task<TargetRegistrationResult> RemoveAsync(
        string configPath,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configPath))
            return TargetRegistrationResult.Fail($"Config file not found: {configPath}");

        var (config, hash) = await ReadWithHash(configPath, cancellationToken);

        if (!config.RegisteredTargets.Contains(targetId, StringComparer.Ordinal))
            return TargetRegistrationResult.NotPresent(targetId);

        var updated = config with
        {
            RegisteredTargets = config.RegisteredTargets
                .Where(id => !string.Equals(id, targetId, StringComparison.Ordinal))
                .ToList(),
        };

        await _writer.WriteAsync(configPath, updated, hash, cancellationToken);
        return TargetRegistrationResult.Removed(targetId);
    }

    private async Task<(SteeringConfiguration Config, string Hash)> ReadWithHash(
        string configPath,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(configPath, cancellationToken);
        var hash = SteergenConfigWriter.ComputeFileHash(bytes);
        var config = await _loader.LoadAsync(configPath, cancellationToken);
        return (config, hash);
    }
}

public sealed record TargetRegistrationResult
{
    public bool Success { get; init; }
    public bool WasAlreadyPresent { get; init; }
    public bool WasNotPresent { get; init; }
    public string? TargetId { get; init; }
    public string? ErrorMessage { get; init; }

    public static TargetRegistrationResult Added(string targetId) =>
        new() { Success = true, TargetId = targetId };

    public static TargetRegistrationResult Removed(string targetId) =>
        new() { Success = true, TargetId = targetId };

    public static TargetRegistrationResult AlreadyPresent(string targetId) =>
        new() { Success = true, WasAlreadyPresent = true, TargetId = targetId };

    public static TargetRegistrationResult NotPresent(string targetId) =>
        new() { Success = true, WasNotPresent = true, TargetId = targetId };

    public static TargetRegistrationResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}
