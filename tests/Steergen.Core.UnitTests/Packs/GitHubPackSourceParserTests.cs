using Steergen.Core.Packs;

namespace Steergen.Core.UnitTests.Packs;

/// <summary>
/// Unit tests for <see cref="GitHubPackSourceParser"/> covering Parse and Format
/// for the <c>github:{owner}/{repo}</c> notation.
/// </summary>
public sealed class GitHubPackSourceParserTests
{
    // ── Parse: valid inputs ──────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidOwnerRepo_ReturnsGitHubPackSource()
    {
        var result = GitHubPackSourceParser.Parse("github:acme-corp/steergen-templates");

        Assert.NotNull(result);
        Assert.Equal("acme-corp", result.Owner);
        Assert.Equal("steergen-templates", result.Repo);
        Assert.Null(result.Ref);
        Assert.Null(result.Path);
    }

    [Fact]
    public void Parse_WithRefAndPath_PassesThroughOptionalParameters()
    {
        var result = GitHubPackSourceParser.Parse(
            "github:org/repo",
            refValue: "v1.0.0",
            path: "subdir/pack");

        Assert.NotNull(result);
        Assert.Equal("org", result.Owner);
        Assert.Equal("repo", result.Repo);
        Assert.Equal("v1.0.0", result.Ref);
        Assert.Equal("subdir/pack", result.Path);
    }

    [Fact]
    public void Parse_WithCommitSha_PassesThroughRef()
    {
        var sha = "abc123def456789012345678901234567890abcd";
        var result = GitHubPackSourceParser.Parse("github:owner/repo", refValue: sha);

        Assert.NotNull(result);
        Assert.Equal(sha, result.Ref);
    }

    [Fact]
    public void Parse_RepoWithDots_ParsesCorrectly()
    {
        var result = GitHubPackSourceParser.Parse("github:owner/my.repo.name");

        Assert.NotNull(result);
        Assert.Equal("owner", result.Owner);
        Assert.Equal("my.repo.name", result.Repo);
    }

    [Fact]
    public void Parse_RepoWithMultipleSlashes_TreatsEverythingAfterFirstSlashAsRepo()
    {
        var result = GitHubPackSourceParser.Parse("github:owner/repo/extra");

        Assert.NotNull(result);
        Assert.Equal("owner", result.Owner);
        Assert.Equal("repo/extra", result.Repo);
    }

    // ── Parse: invalid inputs ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NullOrWhitespace_ReturnsNull(string? source)
    {
        var result = GitHubPackSourceParser.Parse(source!);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_MissingPrefix_ReturnsNull()
    {
        var result = GitHubPackSourceParser.Parse("acme-corp/steergen-templates");
        Assert.Null(result);
    }

    [Fact]
    public void Parse_WrongPrefix_ReturnsNull()
    {
        var result = GitHubPackSourceParser.Parse("gitlab:acme-corp/steergen-templates");
        Assert.Null(result);
    }

    [Fact]
    public void Parse_MissingSlash_ReturnsNull()
    {
        var result = GitHubPackSourceParser.Parse("github:acme-corp");
        Assert.Null(result);
    }

    [Fact]
    public void Parse_EmptyOwner_ReturnsNull()
    {
        var result = GitHubPackSourceParser.Parse("github:/repo");
        Assert.Null(result);
    }

    [Fact]
    public void Parse_EmptyRepo_ReturnsNull()
    {
        var result = GitHubPackSourceParser.Parse("github:owner/");
        Assert.Null(result);
    }

    [Fact]
    public void Parse_WhitespaceOnlyOwner_ReturnsNull()
    {
        var result = GitHubPackSourceParser.Parse("github:   /repo");
        Assert.Null(result);
    }

    [Fact]
    public void Parse_WhitespaceOnlyRepo_ReturnsNull()
    {
        var result = GitHubPackSourceParser.Parse("github:owner/   ");
        Assert.Null(result);
    }

    [Fact]
    public void Parse_PrefixOnly_ReturnsNull()
    {
        var result = GitHubPackSourceParser.Parse("github:");
        Assert.Null(result);
    }

    [Fact]
    public void Parse_CaseSensitivePrefix_ReturnsNull()
    {
        var result = GitHubPackSourceParser.Parse("GitHub:owner/repo");
        Assert.Null(result);
    }

    // ── Format ───────────────────────────────────────────────────────────────

    [Fact]
    public void Format_ProducesCanonicalString()
    {
        var source = new GitHubPackSource { Owner = "acme-corp", Repo = "templates" };
        var result = GitHubPackSourceParser.Format(source);
        Assert.Equal("github:acme-corp/templates", result);
    }

    [Fact]
    public void Format_IgnoresRefAndPath()
    {
        var source = new GitHubPackSource
        {
            Owner = "org",
            Repo = "repo",
            Ref = "v1.0.0",
            Path = "subdir"
        };
        var result = GitHubPackSourceParser.Format(source);
        Assert.Equal("github:org/repo", result);
    }

    // ── Round-trip ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("acme-corp", "templates")]
    [InlineData("my-org", "my-repo")]
    [InlineData("a", "b")]
    public void RoundTrip_FormatThenParse_PreservesOwnerAndRepo(string owner, string repo)
    {
        var original = new GitHubPackSource { Owner = owner, Repo = repo };
        var formatted = GitHubPackSourceParser.Format(original);
        var parsed = GitHubPackSourceParser.Parse(formatted);

        Assert.NotNull(parsed);
        Assert.Equal(owner, parsed.Owner);
        Assert.Equal(repo, parsed.Repo);
    }
}
