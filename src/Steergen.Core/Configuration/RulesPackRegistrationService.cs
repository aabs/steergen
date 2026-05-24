using Steergen.Core.Model;
using Steergen.Core.Packs;

namespace Steergen.Core.Configuration;

/// <summary>
/// Adds and removes rules pack entries from the <c>rulesPacks</c> list in a steergen config file.
/// Uses optimistic locking to detect concurrent modifications.
/// </summary>
public sealed class RulesPackRegistrationService
{
    private readonly SteergenConfigLoader _loader = new();
    private readonly SteergenConfigWriter _writer = new();
    private readonly PackSelectorResolver _selectorResolver = new();

    public async Task<RulesPackRegistrationResult> AddAsync(
        string configPath,
        RulesPackEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configPath))
            return RulesPackRegistrationResult.Fail($"Config file not found: {configPath}");

        var (config, hash) = await ReadWithHash(configPath, cancellationToken);

        // Check if already present (same source)
        var alreadyPresent = config.RulesPacks
            .Any(r => string.Equals(r.Source, entry.Source, StringComparison.OrdinalIgnoreCase));

        if (alreadyPresent)
            return RulesPackRegistrationResult.AlreadyPresent(entry.Source);

        var updated = config with
        {
            RulesPacks = [.. config.RulesPacks, entry],
        };

        await _writer.WriteAsync(configPath, updated, hash, cancellationToken);
        return RulesPackRegistrationResult.Added(entry.Source);
    }

    public async Task<RulesPackRegistrationResult> RemoveAsync(
        string configPath,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configPath))
            return RulesPackRegistrationResult.Fail($"Config file not found: {configPath}");

        var (config, hash) = await ReadWithHash(configPath, cancellationToken);

        var matching = config.RulesPacks
            .Where(r => string.Equals(r.Source, source, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matching.Count == 0)
            return RulesPackRegistrationResult.NotPresent(source);

        var updated = config with
        {
            RulesPacks = config.RulesPacks
                .Where(r => !string.Equals(r.Source, source, StringComparison.OrdinalIgnoreCase))
                .ToList(),
        };

        await _writer.WriteAsync(configPath, updated, hash, cancellationToken);
        return RulesPackRegistrationResult.Removed(source);
    }

    public async Task<RulesPackUpgradeMutationResult> UpdatePinBySelectorAsync(
        string configPath,
        CanonicalPackSelector selector,
        string tag,
        string commitSha,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configPath))
            return RulesPackUpgradeMutationResult.Fail($"Config file not found: {configPath}");

        var (config, hash) = await ReadWithHash(configPath, cancellationToken);
        if (!_selectorResolver.TryResolveRules(config, selector, out var index, out var error))
            return RulesPackUpgradeMutationResult.Fail(error);

        var existing = config.RulesPacks[index];
        var updatedEntry = existing with
        {
            Ref = tag,
            Pin = new PackPin
            {
                Tag = tag,
                CommitSha = commitSha,
            },
        };

        var updatedRulesPacks = config.RulesPacks.ToList();
        updatedRulesPacks[index] = updatedEntry;

        var updated = config with
        {
            RulesPacks = updatedRulesPacks,
        };

        await _writer.WriteAsync(configPath, updated, hash, cancellationToken);
        return RulesPackUpgradeMutationResult.Updated(updatedEntry.Source);
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

public sealed record RulesPackUpgradeMutationResult
{
    public bool Success { get; init; }
    public string? Source { get; init; }
    public string? ErrorMessage { get; init; }

    public static RulesPackUpgradeMutationResult Updated(string source) =>
        new() { Success = true, Source = source };

    public static RulesPackUpgradeMutationResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}

public sealed record RulesPackRegistrationResult
{
    public bool Success { get; init; }
    public bool WasAlreadyPresent { get; init; }
    public bool WasNotPresent { get; init; }
    public string? Source { get; init; }
    public string? ErrorMessage { get; init; }

    public static RulesPackRegistrationResult Added(string source) =>
        new() { Success = true, Source = source };

    public static RulesPackRegistrationResult Removed(string source) =>
        new() { Success = true, Source = source };

    public static RulesPackRegistrationResult AlreadyPresent(string source) =>
        new() { Success = true, WasAlreadyPresent = true, Source = source };

    public static RulesPackRegistrationResult NotPresent(string source) =>
        new() { Success = true, WasNotPresent = true, Source = source };

    public static RulesPackRegistrationResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}
