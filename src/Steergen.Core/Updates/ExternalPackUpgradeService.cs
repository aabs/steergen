using System.Security.Cryptography;
using System.Text;
using Steergen.Core.Configuration;
using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Validation;

namespace Steergen.Core.Updates;

public enum UpgradePackKind
{
    Rules,
    Template,
}

public sealed record ExternalPackUpgradeRequest(UpgradePackKind Kind, string Selector, string? RequestedTag);

public sealed record ExternalPackUpgradeResult
{
    public bool Success { get; init; }
    public bool RollbackPerformed { get; init; }
    public string? FinalTag { get; init; }
    public string? FinalCommitSha { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    public static ExternalPackUpgradeResult Failed(string message, IReadOnlyList<Diagnostic>? diagnostics = null) =>
        new()
        {
            Success = false,
            ErrorMessage = message,
            Diagnostics = diagnostics ?? [],
        };
}

public sealed class ExternalPackUpgradeService
{
    private readonly SteergenConfigLoader _loader;
    private readonly SteergenConfigWriter _writer;
    private readonly PackSelectorResolver _selectorResolver;
    private readonly PackCacheSnapshotStore _snapshotStore;
    private readonly Func<GitHubPackSource, PackType, bool, CancellationToken, Task<PackDownloadResult>> _downloadAsync;
    private readonly Func<GitHubPackSource, PackType, string> _getCachePath;

    public ExternalPackUpgradeService(
        SteergenConfigLoader? loader = null,
        SteergenConfigWriter? writer = null,
        PackSelectorResolver? selectorResolver = null,
        PackCacheSnapshotStore? snapshotStore = null,
        Func<GitHubPackSource, PackType, bool, CancellationToken, Task<PackDownloadResult>>? downloadAsync = null,
        Func<GitHubPackSource, PackType, string>? getCachePath = null)
    {
        _loader = loader ?? new SteergenConfigLoader();
        _writer = writer ?? new SteergenConfigWriter();
        _selectorResolver = selectorResolver ?? new PackSelectorResolver();
        _snapshotStore = snapshotStore ?? new PackCacheSnapshotStore();

        if (downloadAsync is not null && getCachePath is not null)
        {
            _downloadAsync = downloadAsync;
            _getCachePath = getCachePath;
            return;
        }

        var cacheBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".steergen");
        var downloader = new PackDownloader(new HttpClient(), cacheBase);
        _downloadAsync = downloadAsync ?? ((source, packType, force, ct) => downloader.DownloadAsync(source, packType, force, ct));
        _getCachePath = getCachePath ?? ((source, packType) => downloader.GetCachedPath(source, packType));
    }

    public async Task<ExternalPackUpgradeResult> UpgradeAsync(
        string configPath,
        ExternalPackUpgradeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configPath))
            return ExternalPackUpgradeResult.Failed($"Config file not found: {configPath}");

        if (!_selectorResolver.TryParse(request.Selector, out var selector, out var parseError))
            return ExternalPackUpgradeResult.Failed(parseError);

        var bytes = await File.ReadAllBytesAsync(configPath, cancellationToken).ConfigureAwait(false);
        var configHash = SteergenConfigWriter.ComputeFileHash(bytes);
        var config = await _loader.LoadAsync(configPath, cancellationToken).ConfigureAwait(false);

        switch (request.Kind)
        {
            case UpgradePackKind.Rules:
                return await UpgradeRulesAsync(configPath, configHash, config, selector, request.RequestedTag, cancellationToken).ConfigureAwait(false);
            case UpgradePackKind.Template:
                return await UpgradeTemplateAsync(configPath, configHash, config, selector, request.RequestedTag, cancellationToken).ConfigureAwait(false);
            default:
                return ExternalPackUpgradeResult.Failed("Unsupported upgrade kind.");
        }
    }

    private async Task<ExternalPackUpgradeResult> UpgradeRulesAsync(
        string configPath,
        string configHash,
        SteeringConfiguration config,
        CanonicalPackSelector selector,
        string? requestedTag,
        CancellationToken cancellationToken)
    {
        if (!_selectorResolver.TryResolveRules(config, selector, out var index, out var resolveError))
            return ExternalPackUpgradeResult.Failed(resolveError);

        var target = config.RulesPacks[index];
        var parsed = GitHubPackSourceParser.Parse(target.Source, requestedTag ?? target.Ref, target.Path);
        if (parsed is null)
            return ExternalPackUpgradeResult.Failed($"Invalid source format: {target.Source}");

        return await ExecuteUpgradeAsync(
            configPath,
            configHash,
            selector,
            parsed,
            PackType.Rules,
            requestedTag,
            applyUpdate: (resolvedTag, resolvedCommitSha) =>
            {
                var updatedRules = config.RulesPacks.ToList();
                updatedRules[index] = target with
                {
                    Ref = resolvedTag,
                    Pin = new PackPin
                    {
                        Tag = resolvedTag,
                        CommitSha = resolvedCommitSha,
                    },
                };

                return config with { RulesPacks = updatedRules };
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ExternalPackUpgradeResult> UpgradeTemplateAsync(
        string configPath,
        string configHash,
        SteeringConfiguration config,
        CanonicalPackSelector selector,
        string? requestedTag,
        CancellationToken cancellationToken)
    {
        if (!_selectorResolver.TryResolveTemplate(config, selector, out var resolveError))
            return ExternalPackUpgradeResult.Failed(resolveError);

        var target = config.TemplatePack!;
        var parsed = GitHubPackSourceParser.Parse(target.Source ?? string.Empty, requestedTag ?? target.Ref, target.EntryKey);
        if (parsed is null)
            return ExternalPackUpgradeResult.Failed($"Invalid source format: {target.Source}");

        return await ExecuteUpgradeAsync(
            configPath,
            configHash,
            selector,
            parsed,
            PackType.Template,
            requestedTag,
            applyUpdate: (resolvedTag, resolvedCommitSha) =>
            {
                var updatedTemplate = target with
                {
                    Ref = resolvedTag,
                    Pin = new PackPin
                    {
                        Tag = resolvedTag,
                        CommitSha = resolvedCommitSha,
                    },
                };

                return config with { TemplatePack = updatedTemplate };
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ExternalPackUpgradeResult> ExecuteUpgradeAsync(
        string configPath,
        string configHash,
        CanonicalPackSelector selector,
        GitHubPackSource source,
        PackType packType,
        string? requestedTag,
        Func<string, string, SteeringConfiguration> applyUpdate,
        CancellationToken cancellationToken)
    {
        var cachePath = _getCachePath(source, packType);
        var snapshotPath = await _snapshotStore.CaptureAsync(cachePath, cancellationToken).ConfigureAwait(false);

        try
        {
            if (Directory.Exists(cachePath))
                Directory.Delete(cachePath, recursive: true);

            var downloadResult = await _downloadAsync(source, packType, true, cancellationToken).ConfigureAwait(false);
            if (!downloadResult.Success)
            {
                var rollbackPerformed = false;
                try
                {
                    if (!string.IsNullOrWhiteSpace(snapshotPath))
                    {
                        await _snapshotStore.RestoreAsync(snapshotPath!, cachePath, cancellationToken).ConfigureAwait(false);
                        rollbackPerformed = true;
                    }
                }
                catch (Exception rollbackEx)
                {
                    var diagnostics = downloadResult.Diagnostics.ToList();
                    diagnostics.Add(new Diagnostic("UPG002", $"Rollback failed: {rollbackEx.Message}", DiagnosticSeverity.Error));
                    return new ExternalPackUpgradeResult
                    {
                        Success = false,
                        RollbackPerformed = false,
                        ErrorMessage = "Upgrade download failed and rollback failed.",
                        Diagnostics = diagnostics,
                    };
                }

                return new ExternalPackUpgradeResult
                {
                    Success = false,
                    RollbackPerformed = rollbackPerformed,
                    ErrorMessage = "Upgrade download failed.",
                    Diagnostics = downloadResult.Diagnostics,
                };
            }

            var resolvedTag = requestedTag ?? source.Ref ?? "HEAD";
            var resolvedCommitSha = ResolveCommitSha(source, requestedTag);
            var updatedConfig = applyUpdate(resolvedTag, resolvedCommitSha);
            await _writer.WriteAsync(configPath, updatedConfig, configHash, cancellationToken).ConfigureAwait(false);

            return new ExternalPackUpgradeResult
            {
                Success = true,
                RollbackPerformed = false,
                FinalTag = resolvedTag,
                FinalCommitSha = resolvedCommitSha,
                Diagnostics =
                [
                    new Diagnostic(
                        "UPG001",
                        $"Upgrade completed: mode={(requestedTag is null ? "latest-refresh" : "explicit-tag")}, selector={selector.Raw}, tag={resolvedTag}, commitSha={resolvedCommitSha}",
                        DiagnosticSeverity.Info),
                ],
            };
        }
        finally
        {
            _snapshotStore.DeleteSnapshot(snapshotPath);
        }
    }

    private static string ResolveCommitSha(GitHubPackSource source, string? requestedTag)
    {
        var refValue = requestedTag ?? source.Ref;
        if (PackDownloader.IsImmutablePin(refValue))
            return refValue!;

        var input = $"{source.Owner}/{source.Repo}:{refValue ?? "HEAD"}";
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
