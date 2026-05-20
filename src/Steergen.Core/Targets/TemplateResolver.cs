using Steergen.Core.Validation;

namespace Steergen.Core.Targets;

/// <summary>
/// Resolves Scriban templates using a three-level override precedence:
/// 1. Local override path (templatePackPath in config)
/// 2. Cached GitHub pack (downloaded to local pack cache)
/// 3. Built-in embedded templates (EmbeddedTemplateProvider)
///
/// Template packs that declare a <c>targets</c> list are only consulted for
/// those declared targets. Packs without a <c>targets</c> list apply to all targets.
/// </summary>
public sealed class TemplateResolver : ITemplateProvider
{
    private readonly string? _localOverridePath;
    private readonly string? _cachedPackPath;
    private readonly ITemplateProvider _embeddedProvider;
    private readonly IReadOnlySet<string>? _declaredTargets;
    private readonly long _maxFileSizeBytes;

    /// <summary>
    /// Creates a new <see cref="TemplateResolver"/> with the specified override layers.
    /// </summary>
    /// <param name="localOverridePath">
    /// Path to the local template override directory. If non-null but the directory
    /// does not exist on the filesystem, a <see cref="TemplatePackException"/> is thrown
    /// with diagnostic TP001 and exit code 2.
    /// </param>
    /// <param name="cachedPackPath">
    /// Path to the cached GitHub pack directory. May be null if no GitHub pack is configured.
    /// </param>
    /// <param name="embeddedProvider">
    /// The built-in embedded template provider used as the final fallback.
    /// </param>
    /// <param name="declaredTargets">
    /// If non-null, restricts the local and cached layers to only serve templates
    /// for the declared target IDs. If null, all targets are served (backward-compatible).
    /// </param>
    /// <param name="maxFileSizeBytes">
    /// Maximum allowed file size in bytes. Files exceeding this limit are rejected
    /// with diagnostic TP002. Defaults to 1 MB (1,048,576 bytes).
    /// </param>
    public TemplateResolver(
        string? localOverridePath,
        string? cachedPackPath,
        ITemplateProvider embeddedProvider,
        IReadOnlySet<string>? declaredTargets = null,
        long maxFileSizeBytes = 1_048_576)
    {
        ArgumentNullException.ThrowIfNull(embeddedProvider);

        // If localOverridePath is configured but does not exist, throw with TP001
        if (localOverridePath is not null && !Directory.Exists(localOverridePath))
        {
            throw new TemplatePackException(
                new Diagnostic(
                    "TP001",
                    $"Configured templatePackPath does not exist: '{localOverridePath}'",
                    DiagnosticSeverity.Error),
                ExitCode: 2);
        }

        _localOverridePath = localOverridePath;
        _cachedPackPath = cachedPackPath;
        _embeddedProvider = embeddedProvider;
        _declaredTargets = declaredTargets;
        _maxFileSizeBytes = maxFileSizeBytes;
    }

    /// <summary>
    /// Returns the template content for the given target and template name,
    /// resolved using the three-level override precedence.
    /// </summary>
    public string GetTemplate(string targetId, string templateName)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetId);
        ArgumentException.ThrowIfNullOrEmpty(templateName);

        // Check if the target is in the declared targets set (if filtering is active)
        var targetInScope = IsTargetInScope(targetId);

        // Layer 1: Local override path
        if (targetInScope && _localOverridePath is not null)
        {
            var localContent = TryReadTemplateFile(_localOverridePath, targetId, templateName);
            if (localContent is not null)
                return localContent;
        }

        // Layer 2: Cached GitHub pack
        if (targetInScope && _cachedPackPath is not null)
        {
            var cachedContent = TryReadTemplateFile(_cachedPackPath, targetId, templateName);
            if (cachedContent is not null)
                return cachedContent;
        }

        // Layer 3: Built-in embedded templates (always available, no target scoping)
        return _embeddedProvider.GetTemplate(targetId, templateName);
    }

    /// <summary>
    /// Returns the source layer that would provide the template.
    /// Used by <c>steergen inspect --templates</c>.
    /// </summary>
    public TemplateSource GetTemplateSource(string targetId, string templateName)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetId);
        ArgumentException.ThrowIfNullOrEmpty(templateName);

        var targetInScope = IsTargetInScope(targetId);

        // Layer 1: Local override path
        if (targetInScope && _localOverridePath is not null)
        {
            if (TemplateFileExists(_localOverridePath, targetId, templateName))
                return TemplateSource.LocalOverride;
        }

        // Layer 2: Cached GitHub pack
        if (targetInScope && _cachedPackPath is not null)
        {
            if (TemplateFileExists(_cachedPackPath, targetId, templateName))
                return TemplateSource.CachedGitHubPack;
        }

        // Layer 3: Built-in embedded
        return TemplateSource.BuiltInEmbedded;
    }

    /// <summary>
    /// Returns true if this resolver can provide templates for the given target.
    /// A resolver with no declared targets can provide for any target.
    /// </summary>
    public bool ProvidesForTarget(string targetId)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetId);

        // If no declared targets, we provide for all targets
        if (_declaredTargets is null)
            return true;

        return _declaredTargets.Contains(targetId);
    }

    /// <summary>
    /// Determines whether the target is in scope for the local/cached layers.
    /// </summary>
    private bool IsTargetInScope(string targetId)
    {
        // If no declared targets, all targets are in scope
        if (_declaredTargets is null)
            return true;

        return _declaredTargets.Contains(targetId);
    }

    /// <summary>
    /// Attempts to read a template file from the given base path.
    /// Returns null if the file does not exist or is a symbolic link.
    /// Throws if the file exceeds the maximum size limit.
    /// </summary>
    private string? TryReadTemplateFile(string basePath, string targetId, string templateName)
    {
        var filePath = ComputeTemplatePath(basePath, targetId, templateName);

        if (!File.Exists(filePath))
            return null;

        // Do not follow symbolic links (check FileAttributes before reading)
        var attributes = File.GetAttributes(filePath);
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
            return null;

        // Reject files > max size
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > _maxFileSizeBytes)
        {
            throw new TemplatePackException(
                new Diagnostic(
                    "TP002",
                    $"Template file exceeds {_maxFileSizeBytes} byte size limit: '{filePath}' ({fileInfo.Length} bytes)",
                    DiagnosticSeverity.Error),
                ExitCode: 2);
        }

        return File.ReadAllText(filePath);
    }

    /// <summary>
    /// Checks whether a template file exists at the given base path without reading it.
    /// Returns false for symbolic links.
    /// </summary>
    private static bool TemplateFileExists(string basePath, string targetId, string templateName)
    {
        var filePath = ComputeTemplatePath(basePath, targetId, templateName);

        if (!File.Exists(filePath))
            return false;

        // Do not follow symbolic links
        var attributes = File.GetAttributes(filePath);
        return !attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    /// <summary>
    /// Computes the template file path using ordinal file path comparison.
    /// Path format: {basePath}/{targetId}/{templateName}.scriban
    /// </summary>
    private static string ComputeTemplatePath(string basePath, string targetId, string templateName)
    {
        // Use Path.Combine for deterministic path construction with ordinal comparison
        return Path.Combine(basePath, targetId, $"{templateName}.scriban");
    }
}

/// <summary>
/// Exception thrown when a template pack configuration error is detected.
/// Carries a diagnostic and an exit code for CLI reporting.
/// </summary>
public sealed class TemplatePackException : Exception
{
    public Diagnostic Diagnostic { get; }
    public int ExitCode { get; }

    public TemplatePackException(Diagnostic diagnostic, int ExitCode)
        : base(diagnostic.Message)
    {
        Diagnostic = diagnostic;
        this.ExitCode = ExitCode;
    }
}
