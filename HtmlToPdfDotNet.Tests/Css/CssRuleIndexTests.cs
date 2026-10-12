using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Tests.Css;

/// <summary>
/// Tests for <see cref="CssRuleIndex"/>: bucketing by the subject (last) selector part and
/// equivalence with the brute-force <see cref="CssSelectorMatcher.GetMatchingRules"/>.
/// </summary>
public class CssRuleIndexTests
{
    private static List<CssRule> Rules(string css) => CssStyleSheetParser.Parse(css).ToList();

    private static List<HtmlNode> Elements(string html)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        return doc.DocumentNode.Descendants().Where(n => n.NodeType == HtmlNodeType.Element).ToList();
    }

    private static List<CssRule> IndexMatches(CssRuleIndex index, HtmlNode node, CssRuleMatchBuffer buffer)
    {
        index.CollectMatchingRules(node, buffer);
        return buffer.Matches.Select(m => m.Rule).ToList();
    }

    [Fact]
    public void Constructor_IdSubject_LandsInIdBucketOnly()
    {
        CssRuleIndex index = new(Rules("div #main { color: red; }"));

        Assert.Equal([0], index.GetIdBucket("MAIN"));
        Assert.Empty(index.GetTagBucket("div"));
        Assert.Empty(index.Universal);
    }

    [Fact]
    public void Constructor_CompoundClassSubject_UsesFirstClassAsKey()
    {
        CssRuleIndex index = new(Rules("p.a.b { color: red; }"));

        Assert.Equal([0], index.GetClassBucket("A"));
        Assert.Empty(index.GetClassBucket("b"));
        Assert.Empty(index.GetTagBucket("p"));
    }

    [Fact]
    public void Constructor_TagSubject_LandsInTagBucket()
    {
        CssRuleIndex index = new(Rules(".x span { color: red; }"));

        Assert.Equal([0], index.GetTagBucket("SPAN"));
        Assert.Empty(index.GetClassBucket("x"));
    }

    [Fact]
    public void Constructor_UniversalSubject_LandsInUniversal()
    {
        CssRuleIndex index = new(Rules("div * { color: red; }"));

        Assert.Equal([0], index.Universal);
    }

    [Fact]
    public void Constructor_CommaList_RuleLandsInEachSelectorBucketOnce()
    {
        CssRuleIndex index = new(Rules("p, .a, #b, p.c { color: red; }"));

        Assert.Equal([0], index.GetTagBucket("p"));
        Assert.Equal([0], index.GetClassBucket("a"));
        Assert.Equal([0], index.GetClassBucket("c"));
        Assert.Equal([0], index.GetIdBucket("b"));
    }

    [Fact]
    public void CollectMatchingRules_RuleReachableFromSeveralBuckets_IsReturnedOnce()
    {
        List<CssRule> rules = Rules(".a, .b, p { color: red; }");
        CssRuleIndex index = new(rules);
        HtmlNode node = Elements("<p class=\"a b\">x</p>")[0];

        List<CssRule> matched = IndexMatches(index, node, new CssRuleMatchBuffer());

        Assert.Single(matched);
    }

    [Fact]
    public void CollectMatchingRules_EqualsBruteForceForEveryNode()
    {
        List<CssRule> rules = Rules("""
            p { width: 1pt; }
            .a { width: 2pt; }
            #main { width: 3pt; }
            div.a { width: 4pt; }
            div .x { width: 5pt; }
            .a.b { width: 6pt; }
            P.B { width: 7pt; }
            #MAIN p { width: 8pt; }
            span, #main .x, div p.x { width: 9pt; }
            * { width: 10pt; }
            section * { width: 11pt; }
            .X { width: 12pt; }
            ul li span.a { width: 13pt; }
            .missing, li { width: 14pt; }
            p { width: 15pt; }
            .b, .a { width: 16pt; }
            """);
        List<HtmlNode> nodes = Elements("""
            <div id="main" class="a  b">
              <p class="x B">one</p>
              <section class="X">
                <p>two</p>
                <span class="a b x">three</span>
                <ul><li class="b"><span class="A">four</span></li></ul>
              </section>
              <DIV class="a"><p class="x" id="inner">five</p></DIV>
            </div>
            <p id="Main">six</p>
            """);
        CssRuleIndex index = new(rules);
        CssRuleMatchBuffer buffer = new();

        foreach (HtmlNode node in nodes)
        {
            List<CssRule> expected = CssSelectorMatcher.GetMatchingRules(node, rules).ToList();

            List<CssRule> actual = IndexMatches(index, node, buffer);

            Assert.Equal(expected, actual);
        }
    }
}
