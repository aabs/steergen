using System.Text.RegularExpressions;

namespace Steergen.Core.Packs;

/// <summary>
/// Provides version compatibility checking for pack manifests.
/// Compatible means runningVersion >= minSteergenVersion using standard
/// semver comparison (major.minor.patch).
/// </summary>
public static partial class PackVersionChecker
{
    private static readonly Regex SemverPattern = SemverRegex();

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="runningVersion"/> is
    /// greater than or equal to <paramref name="minSteergenVersion"/> using
    /// standard semver comparison (major.minor.patch).
    /// Returns <see langword="false"/> if either version string is not a valid semver.
    /// </summary>
    public static bool IsCompatible(string runningVersion, string minSteergenVersion)
    {
        if (!TryParse(runningVersion, out var running) || !TryParse(minSteergenVersion, out var min))
            return false;

        return Compare(running, min) >= 0;
    }

    /// <summary>
    /// Attempts to parse a version string in the format "major.minor.patch"
    /// where each component is a non-negative integer.
    /// </summary>
    public static bool IsValidSemver(string version) =>
        SemverPattern.IsMatch(version);

    internal static bool TryParse(string version, out (int Major, int Minor, int Patch) result)
    {
        result = default;
        var match = SemverPattern.Match(version);
        if (!match.Success)
            return false;

        result = (
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value));
        return true;
    }

    internal static int Compare((int Major, int Minor, int Patch) a, (int Major, int Minor, int Patch) b)
    {
        int cmp = a.Major.CompareTo(b.Major);
        if (cmp != 0) return cmp;
        cmp = a.Minor.CompareTo(b.Minor);
        if (cmp != 0) return cmp;
        return a.Patch.CompareTo(b.Patch);
    }

    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)$", RegexOptions.Compiled)]
    private static partial Regex SemverRegex();
}
