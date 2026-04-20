using Steergen.Core.Model;

namespace Steergen.Core.Generation;

/// <summary>
/// Deletes generated files matching configured globs under configured roots.
/// Root-bounded safety: candidates that escape configured roots are rejected.
/// No manifest is required; purge eligibility is determined solely by configuration.
/// </summary>
public sealed class GeneratedFilePurger
{
    /// <summary>
    /// Executes a purge for <paramref name="targetId"/> using the given <paramref name="policy"/>.
    /// Roots in the policy may contain resolved absolute paths (template variables should be
    /// resolved by the caller before invoking this method).
    /// </summary>
    /// <param name="targetId">Target identifier for reporting.</param>
    /// <param name="policy">Purge policy from the target's layout definition.</param>
    /// <param name="dryRun">When true, report candidates but do not delete.</param>
    public PurgeResult Purge(
        string targetId,
        PurgePolicyDefinition policy,
        bool dryRun = false)
    {
        if (!policy.Enabled)
            return new PurgeResult
            {
                TargetId = targetId,
                Success = true,
                NoOpReason = "Purge is disabled for this target.",
            };

        if (policy.Globs.Count == 0)
            return new PurgeResult
            {
                TargetId = targetId,
                Success = true,
                NoOpReason = "No purge globs configured for this target.",
            };

        if (policy.Roots.Count == 0)
            return new PurgeResult
            {
                TargetId = targetId,
                Success = true,
                NoOpReason = "No purge roots configured for this target.",
            };

        var removed = new List<string>();
        var skipped = new List<SkippedPurgeFile>();

        foreach (var rootPath in policy.Roots)
        {
            if (!Directory.Exists(rootPath))
                continue;

            var normalizedRoot = NormalizePath(rootPath);

            foreach (var glob in policy.Globs)
            {
                var candidates = DiscoverCandidates(normalizedRoot, glob);
                foreach (var candidate in candidates.OrderBy(c => c, StringComparer.Ordinal))
                {
                    var normalizedCandidate = NormalizePath(candidate);

                    if (!IsWithinRoot(normalizedCandidate, normalizedRoot))
                    {
                        skipped.Add(new SkippedPurgeFile
                        {
                            Path = normalizedCandidate,
                            Reason = SkippedPurgeReason.OutsideRoot,
                        });
                        continue;
                    }

                    if (dryRun)
                    {
                        skipped.Add(new SkippedPurgeFile
                        {
                            Path = normalizedCandidate,
                            Reason = SkippedPurgeReason.DryRun,
                        });
                        continue;
                    }

                    try
                    {
                        File.Delete(normalizedCandidate);
                        removed.Add(normalizedCandidate);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        skipped.Add(new SkippedPurgeFile
                        {
                            Path = normalizedCandidate,
                            Reason = SkippedPurgeReason.PermissionDenied,
                        });
                    }
                    catch (IOException)
                    {
                        skipped.Add(new SkippedPurgeFile
                        {
                            Path = normalizedCandidate,
                            Reason = SkippedPurgeReason.PermissionDenied,
                        });
                    }
                }
            }
        }

        return new PurgeResult
        {
            TargetId = targetId,
            RemovedFiles = removed.OrderBy(f => f, StringComparer.Ordinal).ToList(),
            SkippedFiles = skipped.OrderBy(f => f.Path, StringComparer.Ordinal).ToList(),
            Success = true,
        };
    }

    private static IEnumerable<string> DiscoverCandidates(string normalizedRoot, string glob)
    {
        // Handle ** recursive glob: split on **/ and use AllDirectories search for the remainder.
        var searchOption = SearchOption.TopDirectoryOnly;
        var searchPattern = glob;

        if (glob.Contains("**/") || glob.StartsWith("**/"))
        {
            searchOption = SearchOption.AllDirectories;
            // Strip all leading **/ segments; use the remaining pattern as the filename glob.
            var idx = glob.LastIndexOf("**/", StringComparison.Ordinal);
            searchPattern = glob[(idx + 3)..];
        }
        else if (glob.Contains(Path.DirectorySeparatorChar) || glob.Contains('/'))
        {
            // Sub-directory pattern: locate directory portion and filename portion.
            var lastSep = Math.Max(glob.LastIndexOf('/'), glob.LastIndexOf(Path.DirectorySeparatorChar));
            var dirPart = glob[..lastSep];
            var filePart = glob[(lastSep + 1)..];
            var subDir = Path.Combine(normalizedRoot, dirPart);
            if (!Directory.Exists(subDir))
                return [];
            try
            {
                return Directory.EnumerateFiles(subDir, filePart, SearchOption.TopDirectoryOnly);
            }
            catch (ArgumentException)
            {
                return [];
            }
        }

        try
        {
            return Directory.EnumerateFiles(normalizedRoot, searchPattern, searchOption);
        }
        catch (ArgumentException)
        {
            return [];
        }
    }

    private static bool IsWithinRoot(string normalizedCandidate, string normalizedRoot)
    {
        var rootWithSep = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(rootWithSep, StringComparison.Ordinal)
            || string.Equals(normalizedCandidate, normalizedRoot, StringComparison.Ordinal);
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

    /// <summary>
    /// Resolves template variables
    /// (<c>${globalRoot}</c>, <c>${projectRoot}</c>, <c>${targetRoot}</c>,
    /// <c>${profileRoot}</c>, <c>${tempRoot}</c>)
    /// in a path template string using the provided context.
    /// </summary>
    public static string ResolvePathTemplate(string template, IReadOnlyDictionary<string, string>? context)
    {
        if (context is null || !template.Contains("${", StringComparison.Ordinal))
            return template;

        foreach (var (key, value) in context)
            template = template.Replace($"${{{key}}}", value, StringComparison.OrdinalIgnoreCase);

        return template;
    }

    /// <summary>
    /// Resolves all path templates in a <see cref="PurgePolicyDefinition"/> using the provided context.
    /// </summary>
    public static PurgePolicyDefinition ResolvePolicy(
        PurgePolicyDefinition policy,
        IReadOnlyDictionary<string, string>? context)
    {
        if (context is null)
            return policy;

        var resolvedRoots = policy.Roots
            .Select(r => ResolvePathTemplate(r, context))
            .ToList();

        return policy with { Roots = resolvedRoots };
    }
}
