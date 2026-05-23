using Steergen.Core.Configuration;
using Steergen.Core.Model;

namespace Steergen.Core.UnitTests.Configuration;

public sealed class PackSelectorResolverTests
{
    [Fact]
    public void TryParse_ValidSelectorWithEscapedDelimiter_ParsesSuccessfully()
    {
        var resolver = new PackSelectorResolver();

        var ok = resolver.TryParse("github:acme/repo\\|mirror|packs/security", out var selector, out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal("github:acme/repo|mirror", selector.Source);
        Assert.Equal("packs/security", selector.EntryKey);
    }

    [Fact]
    public void TryParse_MissingDelimiter_ReturnsFalse()
    {
        var resolver = new PackSelectorResolver();

        var ok = resolver.TryParse("github:acme/repo", out _, out var error);

        Assert.False(ok);
        Assert.Contains("format", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_DanglingEscape_ReturnsFalse()
    {
        var resolver = new PackSelectorResolver();

        var ok = resolver.TryParse("github:acme/repo|packs/security\\", out _, out var error);

        Assert.False(ok);
        Assert.Contains("escape", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryResolveRules_AmbiguousSelector_ReturnsFalse()
    {
        var resolver = new PackSelectorResolver();
        resolver.TryParse("github:acme/repo|packs/security", out var selector, out _);

        var config = new SteeringConfiguration
        {
            RulesPacks =
            [
                new RulesPackEntry { Source = "github:acme/repo", Path = "packs/security" },
                new RulesPackEntry { Source = "github:acme/repo", Path = "packs/security" },
            ],
        };

        var ok = resolver.TryResolveRules(config, selector, out _, out var error);

        Assert.False(ok);
        Assert.Contains("ambiguous", error, StringComparison.OrdinalIgnoreCase);
    }
}
