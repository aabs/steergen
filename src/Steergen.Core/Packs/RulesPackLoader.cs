using Steergen.Core.Model;
using Steergen.Core.Parsing;
using Steergen.Core.Validation;

namespace Steergen.Core.Packs;

/// <summary>
/// Discovers, parses, validates, and prepares rules pack documents for merge.
/// Loads all rules from configured packs, applying scope and ordering.
/// Returns documents tagged with source pack metadata.
/// </summary>
public sealed class RulesPackLoader
{
    private const long MaxFileSizeBytes = 1_048_576; // 1 MB

    private readonly PackManifestParser _manifestParser;
    private readonly SteeringValidator _validator;

    public RulesPackLoader(
        PackManifestParser manifestParser,
        SteeringValidator validator)
    {
        _manifestParser = manifestParser ?? throw new ArgumentNullException(nameof(manifestParser));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <summary>
    /// Loads all rules from configured packs, applying scope and ordering.
    /// Returns documents tagged with source pack metadata.
    /// </summary>
    public RulesPackLoadResult Load(
        IReadOnlyList<RulesPackConfiguration> packConfigs,
        string cacheBaseDirectory,
        string runningSteergenVersion)
    {
        var allDocuments = new List<SteeringDocument>();
        var allDiagnostics = new List<Diagnostic>();

        foreach (var packConfig in packConfigs)
        {
            LoadSinglePack(packConfig, cacheBaseDirectory, runningSteergenVersion, allDocuments, allDiagnostics);
        }

        return new RulesPackLoadResult
        {
            Documents = allDocuments,
            Diagnostics = allDiagnostics
        };
    }

    private void LoadSinglePack(
        RulesPackConfiguration packConfig,
        string cacheBaseDirectory,
        string runningSteergenVersion,
        List<SteeringDocument> allDocuments,
        List<Diagnostic> allDiagnostics)
    {
        var source = packConfig.Source;

        // Step a: Resolve pack root path (cache path + optional configured subdirectory)
        var cachePath = ResolvePackRootPath(source, cacheBaseDirectory);

        // Step b: If cache missing → emit error diagnostic, skip pack
        if (!Directory.Exists(cachePath))
        {
            allDiagnostics.Add(new Diagnostic(
                "RP005",
                $"Rules pack '{source.Owner}/{source.Repo}' is not in the local cache. Run 'steergen update --rules' to download it.",
                DiagnosticSeverity.Error));
            return;
        }

        // Step c: Parse pack.yaml → validate manifest
        var manifest = _manifestParser.Parse(cachePath);
        if (manifest is null)
        {
            allDiagnostics.Add(new Diagnostic(
                "RP001",
                $"Rules pack at '{cachePath}' is missing pack.yaml manifest.",
                DiagnosticSeverity.Error));
            return;
        }

        var manifestDiagnostics = _manifestParser.Validate(manifest, PackType.Rules, runningSteergenVersion);
        if (manifestDiagnostics.Count > 0)
        {
            allDiagnostics.AddRange(manifestDiagnostics);

            // If there are errors (not just warnings), skip this pack
            if (manifestDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                return;
        }

        // Step d: Determine effective scope: ScopeOverride ?? manifest.Scope
        var effectiveScope = packConfig.ScopeOverride ?? manifest.Scope;
        if (effectiveScope is null)
        {
            allDiagnostics.Add(new Diagnostic(
                "RP002",
                $"Rules pack '{manifest.Name}' has no effective scope (neither consumer override nor manifest scope).",
                DiagnosticSeverity.Error));
            return;
        }

        // Step e: Resolve rules root: manifest.RulesRoot ?? pack root
        var rulesRoot = cachePath;
        if (!string.IsNullOrWhiteSpace(manifest.RulesRoot))
        {
            rulesRoot = Path.Combine(cachePath, manifest.RulesRoot);
            if (!Directory.Exists(rulesRoot))
            {
                allDiagnostics.Add(new Diagnostic(
                    "RP003",
                    $"Rules pack '{manifest.Name}' declares rulesRoot '{manifest.RulesRoot}' but the directory does not exist.",
                    DiagnosticSeverity.Error));
                return;
            }
        }

        // Step f: Enumerate *.md files recursively (ordinal sort, no symlink follow)
        var mdFiles = EnumerateMarkdownFiles(rulesRoot);

        // Steps g-j: Process each file
        foreach (var filePath in mdFiles)
        {
            // Step g: Reject files > 1 MB
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxFileSizeBytes)
            {
                allDiagnostics.Add(new Diagnostic(
                    "RP004",
                    $"Rules pack '{manifest.Name}': file '{filePath}' exceeds 1 MB size limit ({fileInfo.Length} bytes).",
                    DiagnosticSeverity.Error,
                    new SourceLocation(filePath, 0)));
                continue;
            }

            // Step h: Parse each file with SteeringMarkdownParser
            var content = File.ReadAllText(filePath);
            var document = SteeringMarkdownParser.Parse(content, filePath);

            // Step i: Validate with SteeringValidator
            var validationDiagnostics = _validator.Validate(document);
            if (validationDiagnostics.Count > 0)
            {
                allDiagnostics.AddRange(validationDiagnostics.Select(d => d with
                {
                    Message = $"Rules pack '{manifest.Name}': {d.Message}"
                }));
            }

            // Step j: Tag each rule with SourcePackName and effective scope
            var taggedRules = document.Rules
                .Select(rule => rule with
                {
                    SourcePackName = manifest.Name,
                    SourcePackScope = effectiveScope.Value
                })
                .ToList();

            var taggedDocument = document with { Rules = taggedRules };
            allDocuments.Add(taggedDocument);
        }
    }

    /// <summary>
    /// Resolves the pack root path for a rules pack source.
    /// Format: {cacheBase}/rules/{owner}/{repo}/{ref}/(+ optional configured subdirectory)
    /// </summary>
    private static string ResolvePackRootPath(GitHubPackSource source, string cacheBaseDirectory)
    {
        var refValue = source.Ref ?? "HEAD";
        var cachePath = Path.Combine(
            cacheBaseDirectory,
            "rules",
            source.Owner,
            source.Repo,
            refValue) + Path.DirectorySeparatorChar;

        if (string.IsNullOrWhiteSpace(source.Path))
            return cachePath;

        var normalizedSubPath = source.Path
            .Replace('\\', '/')
            .Trim('/');

        if (string.IsNullOrEmpty(normalizedSubPath))
            return cachePath;

        return Path.Combine(cachePath, normalizedSubPath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Enumerates all .md files recursively under the given root directory,
    /// sorted in ordinal order, excluding symbolic links.
    /// </summary>
    private static IReadOnlyList<string> EnumerateMarkdownFiles(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
            return [];

        var files = new List<string>();
        EnumerateRecursive(rootDirectory, files);
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    /// <summary>
    /// Recursively enumerates .md files without following symbolic links.
    /// </summary>
    private static void EnumerateRecursive(string directory, List<string> results)
    {
        // Check if the directory itself is a symlink
        var dirInfo = new DirectoryInfo(directory);
        if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return;

        // Enumerate files in this directory
        foreach (var file in Directory.GetFiles(directory, "*.md"))
        {
            var fileInfo = new FileInfo(file);
            // Skip symbolic links
            if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            results.Add(file);
        }

        // Recurse into subdirectories
        foreach (var subDir in Directory.GetDirectories(directory))
        {
            var subDirInfo = new DirectoryInfo(subDir);
            // Skip symbolic links
            if (subDirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            EnumerateRecursive(subDir, results);
        }
    }
}
