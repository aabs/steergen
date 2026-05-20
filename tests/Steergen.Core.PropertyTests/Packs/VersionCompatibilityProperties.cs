using CsCheck;
using Steergen.Core.Packs;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for version compatibility checking in <see cref="PackVersionChecker"/>.
/// Feature: custom-template-packs, Property 3: Version Compatibility Check
/// Validates: Requirements 2.4, 2.6, 13.1, 13.2
/// </summary>
public sealed class VersionCompatibilityProperties
{
    // ── Generators ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a non-negative integer suitable for a semver component.
    /// Constrained to [0, 999] to keep values realistic while covering edge cases.
    /// </summary>
    private static readonly Gen<int> GenVersionComponent = Gen.Int[0, 999];

    /// <summary>
    /// Generates a valid semver triple (major, minor, patch).
    /// </summary>
    private static readonly Gen<(int Major, int Minor, int Patch)> GenSemverTuple =
        Gen.Select(GenVersionComponent, GenVersionComponent, GenVersionComponent)
           .Select((major, minor, patch) => (major, minor, patch));

    /// <summary>
    /// Formats a semver tuple as a version string "major.minor.patch".
    /// </summary>
    private static string FormatVersion((int Major, int Minor, int Patch) v) =>
        $"{v.Major}.{v.Minor}.{v.Patch}";

    // ── Property 3: Version Compatibility Check ──────────────────────────────────

    [Fact]
    public void IsCompatible_ReturnsTrue_WhenRunningVersionIsGreaterOrEqual()
    {
        // **Validates: Requirements 2.4, 2.6, 13.1, 13.2**
        // For any pair of semver versions, IsCompatible returns true
        // if and only if runningVersion >= minSteergenVersion.
        Gen.Select(GenSemverTuple, GenSemverTuple)
            .Sample(
                (running, min) =>
                {
                    var runningStr = FormatVersion(running);
                    var minStr = FormatVersion(min);

                    var expected = CompareTuples(running, min) >= 0;
                    var actual = PackVersionChecker.IsCompatible(runningStr, minStr);

                    Assert.Equal(expected, actual);
                },
                iter: 200,
                print: t => $"running={FormatVersion(t.Item1)}, min={FormatVersion(t.Item2)}");
    }

    [Fact]
    public void IsCompatible_ReturnsTrue_WhenVersionsAreEqual()
    {
        // **Validates: Requirements 2.4, 13.1**
        // For any valid semver version, a version is always compatible with itself.
        GenSemverTuple
            .Sample(
                version =>
                {
                    var versionStr = FormatVersion(version);

                    Assert.True(PackVersionChecker.IsCompatible(versionStr, versionStr),
                        $"Expected compatible when running == min: '{versionStr}'");
                },
                iter: 200,
                print: v => $"version={FormatVersion(v)}");
    }

    [Fact]
    public void IsCompatible_MajorVersionDominates()
    {
        // **Validates: Requirements 2.4, 2.6, 13.1, 13.2**
        // When major versions differ, minor and patch are irrelevant.
        // running.Major > min.Major => always compatible regardless of minor/patch.
        Gen.Select(
            GenVersionComponent,
            GenVersionComponent,
            GenVersionComponent,
            GenVersionComponent,
            GenVersionComponent)
           .Where(t => t.Item1 < 999) // Ensure we can add 1 to major
           .Sample(
                (minMajor, runMinor, runPatch, minMinor, minPatch) =>
                {
                    var runMajor = minMajor + 1; // Guarantee running.Major > min.Major
                    var runningStr = $"{runMajor}.{runMinor}.{runPatch}";
                    var minStr = $"{minMajor}.{minMinor}.{minPatch}";

                    Assert.True(PackVersionChecker.IsCompatible(runningStr, minStr),
                        $"Expected compatible when running major ({runMajor}) > min major ({minMajor}): running={runningStr}, min={minStr}");
                },
                iter: 200,
                print: t => $"minMajor={t.Item1}, runMinor={t.Item2}, runPatch={t.Item3}, minMinor={t.Item4}, minPatch={t.Item5}");
    }

    [Fact]
    public void IsCompatible_MinorVersionDominates_WhenMajorEqual()
    {
        // **Validates: Requirements 2.4, 2.6, 13.1, 13.2**
        // When major versions are equal and minor versions differ,
        // patch is irrelevant.
        Gen.Select(
            GenVersionComponent,
            GenVersionComponent,
            GenVersionComponent,
            GenVersionComponent)
           .Where(t => t.Item2 < 999) // Ensure we can add 1 to minor
           .Sample(
                (major, minMinor, runPatch, minPatch) =>
                {
                    var runMinor = minMinor + 1; // Guarantee running.Minor > min.Minor
                    var runningStr = $"{major}.{runMinor}.{runPatch}";
                    var minStr = $"{major}.{minMinor}.{minPatch}";

                    Assert.True(PackVersionChecker.IsCompatible(runningStr, minStr),
                        $"Expected compatible when same major and running minor ({runMinor}) > min minor ({minMinor}): running={runningStr}, min={minStr}");
                },
                iter: 200,
                print: t => $"major={t.Item1}, minMinor={t.Item2}, runPatch={t.Item3}, minPatch={t.Item4}");
    }

    [Fact]
    public void IsCompatible_ReturnsFalse_WhenRunningVersionIsLower()
    {
        // **Validates: Requirements 2.6, 13.2**
        // For any pair where runningVersion < minSteergenVersion,
        // IsCompatible must return false.
        Gen.Select(GenSemverTuple, GenSemverTuple)
            .Where(t => CompareTuples(t.Item1, t.Item2) < 0)
            .Sample(
                (running, min) =>
                {
                    var runningStr = FormatVersion(running);
                    var minStr = FormatVersion(min);

                    Assert.False(PackVersionChecker.IsCompatible(runningStr, minStr),
                        $"Expected incompatible when running < min: running={runningStr}, min={minStr}");
                },
                iter: 200,
                print: t => $"running={FormatVersion(t.Item1)}, min={FormatVersion(t.Item2)}");
    }

    [Fact]
    public void IsCompatible_ReturnsFalse_ForInvalidVersionStrings()
    {
        // **Validates: Requirements 2.4, 13.1**
        // Invalid version strings should never be considered compatible.
        var invalidVersions = Gen.OneOf(
            Gen.Const(""),
            Gen.Const("not-a-version"),
            Gen.Const("1.0"),
            Gen.Const("1.0.0.0"),
            Gen.Const("v1.0.0"),
            Gen.Const("1.0.0-beta"),
            Gen.Const("abc.def.ghi"),
            Gen.Const("-1.0.0"),
            Gen.String[Gen.Char.AlphaNumeric, 1, 20]);

        Gen.Select(invalidVersions, GenSemverTuple)
            .Sample(
                (invalidVersion, validVersion) =>
                {
                    var validStr = FormatVersion(validVersion);

                    // Invalid running version => not compatible
                    Assert.False(PackVersionChecker.IsCompatible(invalidVersion, validStr),
                        $"Expected false for invalid running version: '{invalidVersion}' vs min='{validStr}'");

                    // Invalid min version => not compatible
                    Assert.False(PackVersionChecker.IsCompatible(validStr, invalidVersion),
                        $"Expected false for invalid min version: running='{validStr}' vs '{invalidVersion}'");
                },
                iter: 100,
                print: t => $"invalid='{t.Item1}', valid={FormatVersion(t.Item2)}");
    }

    // ── Helper ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reference implementation of semver comparison for test oracle.
    /// </summary>
    private static int CompareTuples(
        (int Major, int Minor, int Patch) a,
        (int Major, int Minor, int Patch) b)
    {
        int cmp = a.Major.CompareTo(b.Major);
        if (cmp != 0) return cmp;
        cmp = a.Minor.CompareTo(b.Minor);
        if (cmp != 0) return cmp;
        return a.Patch.CompareTo(b.Patch);
    }
}
