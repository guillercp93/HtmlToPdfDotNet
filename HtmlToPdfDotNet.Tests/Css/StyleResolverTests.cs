using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Styles;
using Xunit;

namespace HtmlToPdfDotNet.Tests.Css;

public class StyleResolverTests
{
    private static Dictionary<HtmlNode, ComputedStyle> Resolve(string html)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        return new StyleResolver().Resolve(doc.DocumentNode);
    }

    private static ComputedStyle StyleOf(Dictionary<HtmlNode, ComputedStyle> styles, string tag)
    {
        HtmlNode node = styles.Keys.First(n => n.Name == tag);
        return styles[node];
    }

    // ── Tag defaults ──────────────────────────────────────────────────────────

    [Fact]
    public void H1_HasBoldAndLargerFont()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<h1>Title</h1>");
        ComputedStyle h1 = StyleOf(styles, "h1");

        Assert.Equal(FontWeight.Bold, h1.FontWeight);
        Assert.Equal(24f, h1.FontSize, precision: 0);
    }

    [Fact]
    public void Em_IsItalic()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<em>text</em>");
        ComputedStyle em = StyleOf(styles, "em");

        Assert.Equal(FontStyle.Italic, em.FontStyle);
    }

    [Fact]
    public void Script_IsDisplayNone()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<script>var x=1;</script>");
        ComputedStyle script = StyleOf(styles, "script");

        Assert.Equal(DisplayType.None, script.Display);
    }

    // ── Herencia ──────────────────────────────────────────────────────────────

    [Fact]
    public void Color_IsInheritedByChild()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"color:#ff0000\"><span>hello</span></div>");
        ComputedStyle span = StyleOf(styles, "span");

        Assert.Equal(1f, span.Color.R, precision: 2);
        Assert.Equal(0f, span.Color.G, precision: 2);
    }

    [Fact]
    public void FontSize_IsInheritedByChild()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"font-size:20pt\"><p>hello</p></div>");
        ComputedStyle p = StyleOf(styles, "p");

        Assert.Equal(20f, p.FontSize, precision: 0);
    }

    [Fact]
    public void BackgroundColor_IsNotInherited()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"background-color:#ff0000\"><span>hello</span></div>");
        ComputedStyle span = StyleOf(styles, "span");

        // Background no se hereda — debe ser transparent
        Assert.Equal(0f, span.BackgroundColor.A, precision: 2);
    }

    // ── Inline style ──────────────────────────────────────────────────────────

    [Fact]
    public void InlineStyle_OverridesTagDefault()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<h1 style=\"font-size:10pt\">Title</h1>");
        ComputedStyle h1 = StyleOf(styles, "h1");

        Assert.Equal(10f, h1.FontSize, precision: 0);
    }

    [Fact]
    public void InlineStyle_Margin_ParsesCorrectly()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"margin: 5pt 10pt\">content</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(5f, div.Margin.Top.Points, precision: 1);
        Assert.Equal(10f, div.Margin.Right.Points, precision: 1);
        Assert.Equal(5f, div.Margin.Bottom.Points, precision: 1);
        Assert.Equal(10f, div.Margin.Left.Points, precision: 1);
    }

    [Fact]
    public void InlineStyle_Border_ParsesCorrectly()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"border: 1pt solid black\">box</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.True(div.BorderTop.IsVisible);
        Assert.Equal(BorderStyle.Solid, div.BorderTop.Style);
        Assert.Equal(1f, div.BorderTop.Width.Points, precision: 1);
    }

    [Fact]
    public void InlineStyle_PageBreakBeforeAlways_SetsPageBreakAction()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"page-break-before: always\">page</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(PageBreakAction.Always, div.PageBreakBefore);
    }

    [Fact]
    public void InlineStyle_PageBreakBeforeAvoid_SetsPageBreakAction()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"page-break-before: avoid\">page</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(PageBreakAction.Avoid, div.PageBreakBefore);
    }

    [Fact]
    public void InlineStyle_PageBreakAfterAlways_SetsPageBreakAction()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"page-break-after: always\">page</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(PageBreakAction.Always, div.PageBreakAfter);
    }

    [Fact]
    public void InlineStyle_PageBreakAfterAvoid_SetsPageBreakAction()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"page-break-after: avoid\">page</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(PageBreakAction.Avoid, div.PageBreakAfter);
    }

    [Fact]
    public void InlineStyle_PageBreakInsideAvoid_SetsPageBreakInside()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"page-break-inside: avoid\">content</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(PageBreakInside.Avoid, div.PageBreakInside);
    }

    [Fact]
    public void PageBreakInside_DefaultIsAuto()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div>no style</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(PageBreakInside.Auto, div.PageBreakInside);
    }

    // ── Flexbox properties ──────────────────────────────────────────────

    [Fact]
    public void InlineStyle_DisplayFlex_ParsesCorrectly()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"display: flex\">flex</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(DisplayType.Flex, div.Display);
    }

    [Fact]
    public void InlineStyle_FlexDirectionRow_ParsesCorrectly()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"display: flex; flex-direction: row\">flex</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(FlexDirection.Row, div.FlexDirection);
    }

    [Fact]
    public void InlineStyle_FlexDirectionColumn_ParsesCorrectly()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"display: flex; flex-direction: column\">flex</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(FlexDirection.Column, div.FlexDirection);
    }

    [Fact]
    public void InlineStyle_JustifyContentCenter_ParsesCorrectly()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"display: flex; justify-content: center\">flex</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(JustifyContent.Center, div.JustifyContent);
    }

    [Fact]
    public void InlineStyle_JustifyContentSpaceBetween_ParsesCorrectly()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"display: flex; justify-content: space-between\">flex</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(JustifyContent.SpaceBetween, div.JustifyContent);
    }

    [Fact]
    public void InlineStyle_AlignItemsCenter_ParsesCorrectly()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"display: flex; align-items: center\">flex</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(AlignItems.Center, div.AlignItems);
    }

    [Fact]
    public void InlineStyle_AlignItemsStretch_ParsesCorrectly()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"display: flex; align-items: stretch\">flex</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(AlignItems.Stretch, div.AlignItems);
    }

    // ── BoxModel ──────────────────────────────────────────────────────────────

    [Fact]
    public void BoxModel_ResolveWidth_UsesAvailableWidthWhenAuto()
    {
        ComputedStyle style = new()
        {
            Margin = new CssEdges(new CssLength(10f)),
            Padding = new CssEdges(new CssLength(5f)),
        };

        HtmlNode node = HtmlNode.CreateNode("<div/>");
        BoxModel box = new(node, style);
        box.ResolveWidth(availableWidth: 400f);

        // ContentWidth = 400 - margin(10+10) - padding(5+5) = 370
        Assert.Equal(370f, box.ContentWidth, precision: 0);
    }

    [Fact]
    public void BoxModel_ResolveWidth_UsesExplicitWidthWhenSet()
    {
        ComputedStyle style = new()
        {
            Width = new CssLength(200f),
            Padding = new CssEdges(new CssLength(10f)),
        };

        HtmlNode node = HtmlNode.CreateNode("<div/>");
        BoxModel box = new(node, style);
        box.ResolveWidth(availableWidth: 500f);

        Assert.Equal(200f, box.ContentWidth, precision: 0);
    }

    [Fact]
    public void BoxModel_BorderBoxWidth_IncludesPaddingAndBorder()
    {
        CssBorderSide side = new(new CssLength(2f), BorderStyle.Solid, CssColor.Black);
        ComputedStyle style = new()
        {
            Padding = new CssEdges(new CssLength(8f)),
            BorderLeft = side,
            BorderRight = side,
        };

        HtmlNode node = HtmlNode.CreateNode("<div/>");
        BoxModel box = new(node, style) { ContentWidth = 100f };

        // 100 content + 8+8 padding + 2+2 border = 120
        Assert.Equal(120f, box.BorderBoxWidth, precision: 0);
     }

     // ── Custom Style Features (text-transform, border-radius, text-decoration) ──

     [Fact]
     public void StyleResolver_BorderRadius_ParsesCorrectly()
     {
         Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"border-radius: 8px\">box</div>");
         ComputedStyle div = StyleOf(styles, "div");

         Assert.Equal(6f, div.BorderRadius.Points, precision: 2); // 8 * 0.75 = 6pt
     }

     [Fact]
     public void StyleResolver_TextTransform_ParsesAndInherits()
     {
         Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"text-transform: uppercase\"><span id=\"child\">test</span></div>");
         ComputedStyle div = StyleOf(styles, "div");
         ComputedStyle span = StyleOf(styles, "span");

         Assert.Equal(TextTransForm.Uppercase, div.TextTransForm);
         Assert.Equal(TextTransForm.Uppercase, span.TextTransForm);
     }

     [Fact]
     public void StyleResolver_TextDecoration_ParsesAndInherits()
     {
         Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style=\"text-decoration: underline\"><span id=\"child\">test</span></div>");
         ComputedStyle div = StyleOf(styles, "div");
         ComputedStyle span = StyleOf(styles, "span");

         Assert.Equal(TextDecoration.Underline, div.TextDecoration);
         Assert.Equal(TextDecoration.Underline, span.TextDecoration);
     }

     [Fact]
     public void StyleResolver_AnchorTag_HasDefaultUnderline()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<a href=\"#\">link</a>");
        ComputedStyle a = StyleOf(styles, "a");

        Assert.Equal(TextDecoration.Underline, a.TextDecoration);
    }

    // ── Gap property ──────────────────────────────────────────────────────────

    /// <summary>
    /// Bug: CSS gap property was not parsed for flex containers.
    ///
    /// GIVEN a div with style "gap: 10px"
    /// WHEN resolved
    /// THEN ComputedStyle.Gap.Points MUST be 7.5 (10px * 0.75 PointsPerPx)
    /// </summary>
    [Fact]
    public void GapProperty_ParsesPxValue()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style='gap: 10px;'>content</div>");
        ComputedStyle div = StyleOf(styles, "div");

        // 10px = 7.5pt
        Assert.Equal(7.5f, div.Gap.Points, precision: 2);
    }

    /// <summary>
    /// GIVEN a div with style "gap: 5pt"
    /// WHEN resolved
    /// THEN ComputedStyle.Gap.Points MUST be 5
    /// </summary>
    [Fact]
    public void GapProperty_ParsesPtValue()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style='gap: 5pt;'>content</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(5f, div.Gap.Points);
    }

    /// <summary>
    /// GIVEN a div with style "gap: 0"
    /// WHEN resolved
    /// THEN ComputedStyle.Gap MUST be zero
    /// </summary>
    [Fact]
    public void GapProperty_Zero_ParsesAsZero()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style='gap: 0;'>content</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(0f, div.Gap.Points);
    }

    /// <summary>
    /// GIVEN an element without gap style
    /// WHEN resolved
    /// THEN ComputedStyle.Gap MUST default to zero
    /// </summary>
    [Fact]
    public void GapProperty_Default_IsZero()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div>content</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(0f, div.Gap.Points);
    }

    /// <summary>
    /// GIVEN a negative gap value
    /// WHEN resolved
    /// THEN Gap MUST be treated as zero (invalid according to CSS spec)
    /// </summary>
    [Fact]
    public void GapProperty_Negative_FallsBackToZero()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<div style='gap: -10px;'>content</div>");
        ComputedStyle div = StyleOf(styles, "div");

        Assert.Equal(0f, div.Gap.Points);
    }
}