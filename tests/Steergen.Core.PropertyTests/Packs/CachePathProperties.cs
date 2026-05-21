using CsCheck;
using Steergen.Core.Packs;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for cache path construction.
/// Property 5: Cache Path Construction
/// Validates: Requirements 4.1, 12.1
/// </summary>
public sealed class CachePathProperties
{
    // ── Generators ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates alphanumeric owner/repo/ref segments of reasonable length.
    /// Avoids empty strings and path-separator characters.
    /// </summary>
    private static readonly Gen<string> GenSegment =
        Gen.String[Gen.Char.AlphaNumeric, 1, 20];

    private static readonly Gen<PackType> GenPackType =
        Gen.OneOf(Gen.Const(PackType.Template), Gen.Const(PackType.Rules));

    // ── Property 5: Cache Path Construction ──────────────────────────────────────
    //
    // For any valid (owner, repo, ref) tuple and pack type, the computed cache path
    // SHALL equal {userProfileDirectory}/.steergen/{packTypeDir}/{owner}/{repo}/{ref}/
    // where packTypeDir is "packs" for template packs and "rules" for rules packs.

    [Fact]
    public void GetCachedPath_MatchesExpectedFormat_ForAllPackTypes()
    {
        // **Validates: Requirements 4.1, 12.1**
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cacheBase = Path.Combine(userProfile, ".steergen");
        var downloader = new PackDownloader(new HttpClient(), cacheBase);

        Gen.Select(GenSegment, GenSegment, GenSegment, GenPackType)
            .Sample(
                (owner, repo, refValue, packType) =>
                {
                    var source = new GitHubPackSource
                    {
                        Owner = owner,
                        Repo = repo,
                        Ref = refValue
                    };

                    var result = downloader.GetCachedPath(source, packType);

                    var packTypeDir = packType == PackType.Template ? "packs" : "rules";
                    var expected = Path.Combine(
                        cacheBase,
                        packTypeDir,
                        owner,
                        repo,
                        refValue) + Path.DirectorySeparatorChar;

                    Assert.Equal(expected, result);
                },
                iter: 200,
                print: t => $"owner={t.Item1}, repo={t.Item2}, ref={t.Item3}, packType={t.Item4}");
    }

    [Fact]
    public void GetCachedPath_UsesPacksDir_ForTemplatePacks()
    {
        // **Validates: Requirements 4.1**
        var cacheBase = Path.Combine("C:", "users", "test", ".steergen");
        var downloader = new PackDownloader(new HttpClient(), cacheBase);

        Gen.Select(GenSegment, GenSegment, GenSegment)
            .Sample(
                (owner, repo, refValue) =>
                {
                    var source = new GitHubPackSource
                    {
                        Owner = owner,
                        Repo = repo,
                        Ref = refValue
                    };

                    var result = downloader.GetCachedPath(source, PackType.Template);

                    Assert.Contains(Path.Combine("packs", owner, repo, refValue), result);
                    Assert.DoesNotContain("rules", result);
                },
                iter: 100,
                print: t => $"owner={t.Item1}, repo={t.Item2}, ref={t.Item3}");
    }

    [Fact]
    public void GetCachedPath_UsesRulesDir_ForRulesPacks()
    {
        // **Validates: Requirements 12.1**
        var cacheBase = Path.Combine("C:", "users", "test", ".steergen");
        var downloader = new PackDownloader(new HttpClient(), cacheBase);

        Gen.Select(GenSegment, GenSegment, GenSegment)
            .Sample(
                (owner, repo, refValue) =>
                {
                    var source = new GitHubPackSource
                    {
                        Owner = owner,
                        Repo = repo,
                        Ref = refValue
                    };

                    var result = downloader.GetCachedPath(source, PackType.Rules);

                    Assert.Contains(Path.Combine("rules", owner, repo, refValue), result);
                    Assert.DoesNotContain("packs", result);
                },
                iter: 100,
                print: t => $"owner={t.Item1}, repo={t.Item2}, ref={t.Item3}");
    }

    [Fact]
    public void GetCachedPath_EndsWithDirectorySeparator()
    {
        // **Validates: Requirements 4.1, 12.1**
        var cacheBase = Path.Combine("home", "user", ".steergen");
        var downloader = new PackDownloader(new HttpClient(), cacheBase);

        Gen.Select(GenSegment, GenSegment, GenSegment, GenPackType)
            .Sample(
                (owner, repo, refValue, packType) =>
                {
                    var source = new GitHubPackSource
                    {
                        Owner = owner,
                        Repo = repo,
                        Ref = refValue
                    };

                    var result = downloader.GetCachedPath(source, packType);

                    Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), result);
                },
                iter: 100,
                print: t => $"owner={t.Item1}, repo={t.Item2}, ref={t.Item3}, packType={t.Item4}");
    }

    [Fact]
    public void GetCachedPath_UsesHEAD_WhenRefIsNull()
    {
        // **Validates: Requirements 4.1, 12.1**
        // When no ref is specified, the cache path uses "HEAD" as the ref directory.
        var cacheBase = Path.Combine("home", "user", ".steergen");
        var downloader = new PackDownloader(new HttpClient(), cacheBase);

        Gen.Select(GenSegment, GenSegment, GenPackType)
            .Sample(
                (owner, repo, packType) =>
                {
                    var source = new GitHubPackSource
                    {
                        Owner = owner,
                        Repo = repo,
                        Ref = null
                    };

                    var result = downloader.GetCachedPath(source, packType);

                    var packTypeDir = packType == PackType.Template ? "packs" : "rules";
                    var expected = Path.Combine(
                        cacheBase,
                        packTypeDir,
                        owner,
                        repo,
                        "HEAD") + Path.DirectorySeparatorChar;

                    Assert.Equal(expected, result);
                },
                iter: 100,
                print: t => $"owner={t.Item1}, repo={t.Item2}, packType={t.Item3}");
    }
}
