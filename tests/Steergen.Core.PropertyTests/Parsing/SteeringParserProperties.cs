using Steergen.Core.Parsing;
using Xunit;

namespace Steergen.Core.PropertyTests.Parsing;

public sealed class SteeringParserProperties
{
    [Fact]
    public void Parse_PreservesRuleId_WhenValidIdProvided()
    {
        var ids = new[] { "RULE-001", "MY-RULE", "X", "ABC-123-DEF" };
        foreach (var id in ids)
        {
            var content = $"""
                :::rule id="{id}" severity="info" domain="core"
                Some text.
                :::
                """;
            var doc = SteeringMarkdownParser.Parse(content, "test.md");
            Assert.Single(doc.Rules);
            Assert.Equal(id, doc.Rules[0].Id);
        }
    }

    [Fact]
    public void Parse_RuleCount_EqualsRuleBlockCount()
    {
        var ruleCounts = new[] { 0, 1, 3, 5, 10 };
        foreach (var count in ruleCounts)
        {
            var content = string.Join('\n', Enumerable.Range(1, count).Select(i =>
                $":::rule id=\"R{i:D3}\" severity=\"info\" domain=\"core\"\nText {i}.\n:::"));
            var doc = SteeringMarkdownParser.Parse(content, "test.md");
            Assert.Equal(count, doc.Rules.Count);
        }
    }

    [Fact]
    public void Parse_EmptyContent_YieldsEmptyDocument()
    {
        var doc = SteeringMarkdownParser.Parse(string.Empty, "test.md");
        Assert.Null(doc.Id);
        Assert.Empty(doc.Rules);
    }

    [Fact]
    public void Parse_MissingFrontmatter_YieldsDocumentWithNoIdOrRulesFromFrontmatter()
    {
        var content = "# Just a heading\n\nSome text, no frontmatter.";
        var doc = SteeringMarkdownParser.Parse(content, "test.md");
        Assert.Null(doc.Id);
        Assert.Empty(doc.Rules);
    }

    [Fact]
    public void Parse_RuleAttributes_ArePreservedThroughParse()
    {
        const string content = """
            :::rule id="R001" severity="error" category="security" domain="core" deprecated="true"
            Check something.
            :::
            """;
        var doc = SteeringMarkdownParser.Parse(content, "test.md");
        var rule = Assert.Single(doc.Rules);
        Assert.Equal("R001", rule.Id);
        Assert.Equal("error", rule.Severity);
        Assert.Equal("security", rule.Category);
        Assert.Equal("core", rule.Domain);
        Assert.True(rule.Deprecated);
        Assert.Equal("Check something.", rule.PrimaryText);
    }
}
