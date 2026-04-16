using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models;
using Xunit;

namespace HtmlToPdf.Tests.Css;

public class StyleResolverTests
{
    private static Dictionary<HtmlNode, ComputedStyle> Resolve(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return new StyleResolver().Resolve(doc.DocumentNode);
    }

    private static ComputedStyle StyleOf(Dictionary<HtmlNode, ComputedStyle> styles, string tag)
    {
        var node = styles.Keys.First(n => n.Name == tag);
        return styles[node];
    }

    // ── Tag defaults ──────────────────────────────────────────────────────────

    [Fact]
    public void H1_HasBoldAndLargerFont()
    {
        var styles = Resolve("<h1>Title</h1>");
        var h1 = StyleOf(styles, "h1");

        Assert.Equal(FontWeight.Bold, h1.FontWeight);
        Assert.Equal(24f, h1.FontSize, precision: 0);
    }

    [Fact]
    public void Em_IsItalic()
    {
        var styles = Resolve("<em>text</em>");
        var em = StyleOf(styles, "em");

        Assert.Equal(FontStyle.Italic, em.FontStyle);
    }

    [Fact]
    public void Script_IsDisplayNone()
    {
        var styles = Resolve("<script>var x=1;</script>");
        var script = StyleOf(styles, "script");

        Assert.Equal(DisplayType.None, script.Display);
    }

    // ── Herencia ──────────────────────────────────────────────────────────────

    [Fact]
    public void Color_IsInheritedByChild()
    {
        var styles = Resolve("<div style=\"color:#ff0000\"><span>hello</span></div>");
        var span = StyleOf(styles, "span");

        Assert.Equal(1f, span.Color.R, precision: 2);
        Assert.Equal(0f, span.Color.G, precision: 2);
    }

    [Fact]
    public void FontSize_IsInheritedByChild()
    {
        var styles = Resolve("<div style=\"font-size:20pt\"><p>hello</p></div>");
        var p = StyleOf(styles, "p");

        Assert.Equal(20f, p.FontSize, precision: 0);
    }

    [Fact]
    public void BackgroundColor_IsNotInherited()
    {
        var styles = Resolve("<div style=\"background-color:#ff0000\"><span>hello</span></div>");
        var span = StyleOf(styles, "span");

        // Background no se hereda — debe ser transparent
        Assert.Equal(0f, span.BackgroundColor.A, precision: 2);
    }

    // ── Inline style ──────────────────────────────────────────────────────────

    [Fact]
    public void InlineStyle_OverridesTagDefault()
    {
        var styles = Resolve("<h1 style=\"font-size:10pt\">Title</h1>");
        var h1 = StyleOf(styles, "h1");

        Assert.Equal(10f, h1.FontSize, precision: 0);
    }

    [Fact]
    public void InlineStyle_Margin_ParsesCorrectly()
    {
        var styles = Resolve("<div style=\"margin: 5pt 10pt\">content</div>");
        var div = StyleOf(styles, "div");

        Assert.Equal(5f, div.Margin.Top.Points, precision: 1);
        Assert.Equal(10f, div.Margin.Right.Points, precision: 1);
        Assert.Equal(5f, div.Margin.Bottom.Points, precision: 1);
        Assert.Equal(10f, div.Margin.Left.Points, precision: 1);
    }

    [Fact]
    public void InlineStyle_Border_ParsesCorrectly()
    {
        var styles = Resolve("<div style=\"border: 1pt solid black\">box</div>");
        var div = StyleOf(styles, "div");

        Assert.True(div.BorderTop.IsVisible);
        Assert.Equal(BorderStyle.Solid, div.BorderTop.Style);
        Assert.Equal(1f, div.BorderTop.Width.Points, precision: 1);
    }

    [Fact]
    public void InlineStyle_PageBreakBefore_SetsFlag()
    {
        var styles = Resolve("<div style=\"page-break-before: always\">page</div>");
        var div = StyleOf(styles, "div");

        Assert.True(div.PageBreakBefore);
    }

    // ── BoxModel ──────────────────────────────────────────────────────────────

    [Fact]
    public void BoxModel_ResolveWidth_UsesAvailableWidthWhenAuto()
    {
        var style = new ComputedStyle
        {
            Margin = new CssEdges(new CssLength(10f)),
            Padding = new CssEdges(new CssLength(5f)),
        };

        var node = HtmlNode.CreateNode("<div/>");
        var box = new BoxModel(node, style);
        box.ResolveWidth(availableWidth: 400f);

        // ContentWidth = 400 - margin(10+10) - padding(5+5) = 370
        Assert.Equal(370f, box.ContentWidth, precision: 0);
    }

    [Fact]
    public void BoxModel_ResolveWidth_UsesExplicitWidthWhenSet()
    {
        var style = new ComputedStyle
        {
            Width = new CssLength(200f),
            Padding = new CssEdges(new CssLength(10f)),
        };

        var node = HtmlNode.CreateNode("<div/>");
        var box = new BoxModel(node, style);
        box.ResolveWidth(availableWidth: 500f);

        Assert.Equal(200f, box.ContentWidth, precision: 0);
    }

    [Fact]
    public void BoxModel_BorderBoxWidth_IncludesPaddingAndBorder()
    {
        var side = new CssBorderSide(new CssLength(2f), BorderStyle.Solid, CssColor.Black);
        var style = new ComputedStyle
        {
            Padding = new CssEdges(new CssLength(8f)),
            BorderLeft = side,
            BorderRight = side,
        };

        var node = HtmlNode.CreateNode("<div/>");
        var box = new BoxModel(node, style) { ContentWidth = 100f };

        // 100 content + 8+8 padding + 2+2 border = 120
        Assert.Equal(120f, box.BorderBoxWidth, precision: 0);
    }
}