using System.Text.RegularExpressions;

namespace Steergen.Core.Updates;

/// <summary>
/// Parses and compares template-pack version strings (stable <c>x.y.z</c> and
/// preview <c>x.y.z-previewN</c>) and resolves the appropriate version from a
/// catalog.
/// </summary>
public static class TemplateVersionResolver
{
    private static readonly Regex StablePattern  = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);
    private static readonly Regex PreviewPattern = new(@"^\d+\.\d+\.\d+-preview(\d+)$", RegexOptions.Compiled);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="version"/> is a valid
    /// stable (<c>x.y.z</c>) or preview (<c>x.y.z-previewN</c>) version string.
    /// </summary>
    public static bool IsValidVersion(string version) =>
        StablePattern.IsMatch(version) || PreviewPattern.IsMatch(version);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="version"/> is a preview
    /// version (<c>x.y.z-previewN</c>).
    /// </summary>
    public static bool IsPreviewVersion(string version) =>
        PreviewPattern.IsMatch(version);

    /// <summary>
    /// Returns the highest stable version from <paramref name="catalog"/>, or
    /// <see langword="null"/> if the catalog contains no stable entries.
    /// </summary>
    public static string? ResolveLatestStable(IEnumerable<string> catalog)
    {
        return catalog
            .Where(v => StablePattern.IsMatch(v))
            .OrderByDescending(v => Parse(v), VersionComparer.Instance)
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns the highest version from <paramref name="catalog"/> including
    /// preview entries, or <see langword="null"/> if the catalog is empty.
    /// </summary>
    public static string? ResolveLatestIncludingPreview(IEnumerable<string> catalog)
    {
        return catalog
            .Where(IsValidVersion)
            .OrderByDescending(v => Parse(v), VersionComparer.Instance)
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns <paramref name="requested"/> if it appears in
    /// <paramref name="catalog"/> and has a valid format; otherwise
    /// <see langword="null"/>.
    /// </summary>
    public static string? ResolveExact(IEnumerable<string> catalog, string requested)
    {
        if (!IsValidVersion(requested))
            return null;
        return catalog.FirstOrDefault(v => string.Equals(v, requested, StringComparison.OrdinalIgnoreCase));
    }

    // ── Internal parsing ──────────────────────────────────────────────────────

    internal static ParsedVersion Parse(string version)
    {
        var previewMatch = PreviewPattern.Match(version);
        if (previewMatch.Success)
        {
            var parts = version[..version.IndexOf('-')].Split('.');
            return new ParsedVersion(
                int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]),
                int.Parse(previewMatch.Groups[1].Value));
        }

        var stableParts = version.Split('.');
        return new ParsedVersion(
            int.Parse(stableParts[0]), int.Parse(stableParts[1]), int.Parse(stableParts[2]),
            PreviewNumber: null);
    }

    internal readonly record struct ParsedVersion(int Major, int Minor, int Patch, int? PreviewNumber);

    private sealed class VersionComparer : IComparer<ParsedVersion>
    {
        public static readonly VersionComparer Instance = new();

        public int Compare(ParsedVersion x, ParsedVersion y)
        {
            int cmp = x.Major.CompareTo(y.Major);
            if (cmp != 0) return cmp;
            cmp = x.Minor.CompareTo(y.Minor);
            if (cmp != 0) return cmp;
            cmp = x.Patch.CompareTo(y.Patch);
            if (cmp != 0) return cmp;

            // Stable (null previewNumber) beats preview (has previewNumber) for the same base.
            return (x.PreviewNumber, y.PreviewNumber) switch
            {
                (null, null) => 0,
                (null, _)    => 1,   // stable > preview
                (_, null)    => -1,  // preview < stable
                var (xp, yp) => xp!.Value.CompareTo(yp!.Value),
            };
        }
    }
}
