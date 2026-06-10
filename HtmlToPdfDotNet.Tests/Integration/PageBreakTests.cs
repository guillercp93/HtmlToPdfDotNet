using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Tests.Integration;

/// <summary>
/// Integration tests for page-break-* CSS properties.
/// Scenarios defined in: openspec/changes/layout-engine-upgrades/specs/page-breaks/spec.md
/// </summary>
public class PageBreakTests
{
    private static LayoutResult RunLayout(string html, PageLayout? page = null)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        Dictionary<HtmlNode, ComputedStyle> styles = new StyleResolver().Resolve(doc.DocumentNode);
        BlockLayoutEngine engine = new(page ?? PageLayout.A4, styles);
        return engine.Layout(doc.DocumentNode);
    }

    /// <summary>
    /// Scenario: Keep small block together (happy path)
    /// GIVEN a block element with page-break-inside: avoid whose content fits on one page
    /// WHEN the document is rendered
    /// THEN the entire block MUST appear on the same page
    /// </summary>
    [Fact]
    public void PageBreakInsideAvoid_KeepsSmallBlockOnOnePage()
    {
        // A small block with page-break-inside: avoid should not split across pages
        // Padded block with red background that easily fits on one page
        string html = @"
            <div style='height: 100px; page-break-inside: avoid; background-color: #ff0000'>
                <p>Small content that fits on one page</p>
            </div>";

        LayoutResult result = RunLayout(html);

        // The block's background and text should be on page 0
        var rects = result.Primitives.OfType<RectPrimitive>().Where(r => r.Fill.R > 0.9f).ToList();
        var texts = result.Primitives.OfType<TextPrimitive>().ToList();

        Assert.NotEmpty(rects);
        Assert.NotEmpty(texts);

        Assert.All(rects, r => Assert.Equal(0, r.PageIndex));
        Assert.All(texts, t => Assert.Equal(0, t.PageIndex));
    }

    /// <summary>
    /// Scenario: Too-large block (edge case)
    /// GIVEN a block element with page-break-inside: avoid whose content exceeds one page
    /// WHEN the document is rendered
    /// THEN the renderer SHALL break the content across pages at natural break points
    /// </summary>
    [Fact]
    public void PageBreakInsideAvoid_TooLargeBlock_BreaksAcrossPages()
    {
        // A block with page-break-inside: avoid but content taller than one page
        // must still be breakable since it cannot physically fit on one page
        string html = @"
            <div style='page-break-inside: avoid'>
                <div style='height: 2000px'>Tall content that exceeds one page</div>
            </div>";

        LayoutResult result = RunLayout(html);

        // The content should span multiple pages since it's taller than A4 content
        Assert.True(result.PageCount > 1,
            $"Block larger than one page should span multiple pages, but got {result.PageCount} page(s)");
    }

    /// <summary>
    /// Scenario: Force new page before (happy path)
    /// GIVEN an element with page-break-before: always
    /// WHEN rendering
    /// THEN the element MUST begin at the top of a new page
    /// </summary>
    [Fact]
    public void PageBreakBeforeAlways_ElementStartsOnNewPage()
    {
        string html = @"
            <div>Page 1 content</div>
            <div style='page-break-before: always'>Page 2</div>";

        LayoutResult result = RunLayout(html);

        Assert.True(result.PageCount >= 2, "Should have at least 2 pages");

        // Page 1 should have text primitives (from the second div)
        IEnumerable<RenderPrimitive> page1Prims = result.ForPage(1);
        Assert.NotEmpty(page1Prims);
        Assert.Contains(page1Prims, p => p is TextPrimitive);
    }

    /// <summary>
    /// Scenario: Force new page after
    /// GIVEN an element with page-break-after: always
    /// WHEN rendering
    /// THEN subsequent content MUST begin at the top of a new page
    /// </summary>
    [Fact]
    public void PageBreakAfterAlways_NextContentStartsOnNewPage()
    {
        string html = @"
            <div style='page-break-after: always'>Page 1 content</div>
            <div>Page 2 content</div>";

        LayoutResult result = RunLayout(html);

        Assert.True(result.PageCount >= 2, "Should have at least 2 pages");

        // Page 1 should have text primitives
        IEnumerable<RenderPrimitive> page1Prims = result.ForPage(1);
        Assert.NotEmpty(page1Prims);
        Assert.Contains(page1Prims, p => p is TextPrimitive);
    }

    /// <summary>
    /// Scenario: Avoid break when space available
    /// GIVEN elements with page-break-before: avoid that fit on one page
    /// WHEN rendering
    /// THEN the elements stay on the same page
    /// </summary>
    [Fact]
    public void PageBreakBeforeAvoid_ElementsStayOnSamePageWhenTheyFit()
    {
        // Two small elements that both fit on one page
        string html = @"
            <div style='page-break-before: avoid'>First block</div>
            <div style='page-break-before: avoid'>Second block</div>";

        LayoutResult result = RunLayout(html);

        // Both elements fit on one page
        Assert.Equal(1, result.PageCount);

        // Both pages should have multiple text primitives on page 0
        List<TextPrimitive> page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        Assert.True(page0Texts.Count >= 2,
            $"Expected at least 2 text primitives on page 0, got {page0Texts.Count}");
    }

    /// <summary>
    /// Scenario: page-break-after: avoid when space available
    /// GIVEN elements with page-break-after: avoid that fit on one page
    /// WHEN rendering
    /// THEN the elements stay on the same page
    /// </summary>
    [Fact]
    public void PageBreakAfterAvoid_NextContentStaysOnSamePageWhenPossible()
    {
        string html = @"
            <div style='page-break-after: avoid'>First block</div>
            <div>Second block that fits</div>";

        LayoutResult result = RunLayout(html);

        // Both should still be on one page since they fit
        Assert.Equal(1, result.PageCount);

        // Multiple text primitives on page 0
        List<TextPrimitive> page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        Assert.True(page0Texts.Count >= 2,
            $"Expected at least 2 text primitives on page 0, got {page0Texts.Count}");
    }

    /// <summary>
    /// Scenario: PageBreakBefore default is Auto
    /// GIVEN an element without any page-break-before style
    /// THEN its PageBreakBefore MUST be PageBreakAction.Auto
    /// </summary>
    [Fact]
    public void PageBreakBefore_DefaultIsAuto()
    {
        ComputedStyle style = new();
        Assert.Equal(PageBreakAction.Auto, style.PageBreakBefore);
    }

    /// <summary>
    /// Scenario: PageBreakAfter default is Auto
    /// GIVEN an element without any page-break-after style
    /// THEN its PageBreakAfter MUST be PageBreakAction.Auto
    /// </summary>
    [Fact]
    public void PageBreakAfter_DefaultIsAuto()
    {
        ComputedStyle style = new();
        Assert.Equal(PageBreakAction.Auto, style.PageBreakAfter);
    }
}
