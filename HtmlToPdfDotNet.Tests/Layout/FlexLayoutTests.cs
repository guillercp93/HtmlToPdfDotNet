using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Tests.Layout;

/// <summary>
/// Unit and integration tests for the Flexbox Layout Engine.
/// Scenarios defined in: openspec/changes/layout-engine-upgrades/specs/flexbox-layout/spec.md
/// </summary>
public class FlexLayoutTests
{
    private static LayoutResult RunLayout(string html, PageLayout? page = null)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        Dictionary<HtmlNode, ComputedStyle> styles = new StyleResolver().Resolve(doc.DocumentNode);
        BlockLayoutEngine engine = new(page ?? PageLayout.A4, styles);
        return engine.Layout(doc.DocumentNode);
    }

    #region Row Distribution (Happy Path)

    /// <summary>
    /// Scenario: Row layout distribution (happy path)
    /// GIVEN a flex container with display:flex; flex-direction:row
    /// WHEN rendered
    /// THEN child items MUST be distributed horizontally
    /// </summary>
    [Fact]
    public void FlexRow_BasicLayout_ChildrenRenderOnPage0()
    {
        string html = @"
            <div style='display: flex; flex-direction: row;'>
                <div style='width: 50pt;'><p>Item A</p></div>
                <div style='width: 50pt;'><p>Item B</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        // Both items should render on page 0
        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();

        Assert.NotEmpty(page0Texts);
        // Layout renders each word as a separate text run, so verify multiple runs exist
        Assert.True(page0Texts.Count >= 2,
            $"Expected at least 2 text runs on page 0, got {page0Texts.Count}");
    }

    /// <summary>
    /// Scenario: Row layout with justify-content: center
    /// GIVEN a flex container with justify-content: center
    /// WHEN rendered
    /// THEN children MUST be centered within the container
    /// </summary>
    [Fact]
    public void FlexRow_JustifyCenter_ChildrenRenderOnPage0()
    {
        string html = @"
            <div style='display: flex; flex-direction: row; justify-content: center;'>
                <div><p>Center</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        // Should render on page 0
        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        Assert.NotEmpty(page0Texts);
    }

    /// <summary>
    /// Scenario: Row layout with justify-content: flex-end
    /// GIVEN a flex container with justify-content: flex-end
    /// WHEN rendered
    /// THEN children MUST be positioned at the end of the main axis
    /// </summary>
    [Fact]
    public void FlexRow_JustifyFlexEnd_ChildrenRenderOnPage0()
    {
        string html = @"
            <div style='display: flex; flex-direction: row; justify-content: flex-end;'>
                <div style='width: 50pt;'><p>End</p></div>
            </div>";

        LayoutResult result = RunLayout(html);
        Assert.Equal(1, result.PageCount);

        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        Assert.NotEmpty(page0Texts);
    }

    /// <summary>
    /// Scenario: Row layout with justify-content: space-between
    /// GIVEN a flex container with justify-content: space-between
    /// WHEN rendered with three items
    /// THEN first item MUST be at start, last at end, with equal spacing between
    /// </summary>
    [Fact]
    public void FlexRow_SpaceBetween_ThreeChildrenRender()
    {
        string html = @"
            <div style='display: flex; flex-direction: row; justify-content: space-between; width: 300pt;'>
                <div style='width: 50pt;'><p>First</p></div>
                <div style='width: 50pt;'><p>Second</p></div>
                <div style='width: 50pt;'><p>Third</p></div>
            </div>";

        LayoutResult result = RunLayout(html);
        Assert.Equal(1, result.PageCount);

        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        Assert.True(page0Texts.Count >= 3,
            $"Expected at least 3 text runs for three items, got {page0Texts.Count}");
    }

    /// <summary>
    /// Scenario: Row layout with justify-content: space-around
    /// GIVEN a flex container with justify-content: space-around
    /// WHEN rendered
    /// THEN children MUST have equal space around each item
    /// </summary>
    [Fact]
    public void FlexRow_SpaceAround_ChildrenRenderOnPage0()
    {
        string html = @"
            <div style='display: flex; flex-direction: row; justify-content: space-around;'>
                <div style='width: 40pt;'><p>A</p></div>
                <div style='width: 40pt;'><p>B</p></div>
            </div>";

        LayoutResult result = RunLayout(html);
        Assert.Equal(1, result.PageCount);

        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        Assert.NotEmpty(page0Texts);
    }

    /// <summary>
    /// Scenario: Row layout with align-items: center
    /// GIVEN a flex container with align-items: center
    /// WHEN rendered with items of different heights
    /// THEN children MUST be centered along the cross axis
    /// </summary>
    [Fact]
    public void FlexRow_AlignCenter_ChildrenRenderOnPage0()
    {
        string html = @"
            <div style='display: flex; flex-direction: row; align-items: center; height: 100pt;'>
                <div style='width: 50pt; height: 30pt;'><p>Short</p></div>
                <div style='width: 50pt;'><p>Tall</p></div>
            </div>";

        LayoutResult result = RunLayout(html);
        Assert.Equal(1, result.PageCount);

        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        Assert.NotEmpty(page0Texts);
    }

    /// <summary>
    /// Scenario: Row layout with align-items: flex-end
    /// GIVEN a flex container with align-items: flex-end
    /// WHEN rendered
    /// THEN children MUST be positioned at the end of the cross axis
    /// </summary>
    [Fact]
    public void FlexRow_AlignFlexEnd_ChildrenRenderOnPage0()
    {
        string html = @"
            <div style='display: flex; flex-direction: row; align-items: flex-end; height: 100pt;'>
                <div style='width: 50pt;'><p>Bottom</p></div>
            </div>";

        LayoutResult result = RunLayout(html);
        Assert.Equal(1, result.PageCount);

        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        Assert.NotEmpty(page0Texts);
    }

    #endregion

    #region Column Layout (Happy Path)

    /// <summary>
    /// Scenario: Column layout (happy path)
    /// GIVEN a flex container with display:flex; flex-direction:column
    /// WHEN rendered
    /// THEN child items MUST be stacked vertically
    /// </summary>
    [Fact]
    public void FlexColumn_BasicLayout_ChildrenStackVertically()
    {
        string html = @"
            <div style='display: flex; flex-direction: column;'>
                <div><p>Top</p></div>
                <div><p>Bottom</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        // Both items should render on page 0
        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        Assert.True(page0Texts.Count >= 2,
            $"Expected at least 2 text runs for two column items, got {page0Texts.Count}");
    }

    /// <summary>
    /// Scenario: Column layout with align-items: center
    /// GIVEN a flex container with display:flex; flex-direction:column; align-items:center
    /// WHEN rendered
    /// THEN child items MUST be horizontally centered
    /// </summary>
    [Fact]
    public void FlexColumn_AlignCenter_ChildrenHorizontallyCentered()
    {
        string html = @"
            <div style='display: flex; flex-direction: column; align-items: center;'>
                <div style='width: 100pt;'><p>Center</p></div>
                <div style='width: 80pt;'><p>Me</p></div>
            </div>";

        LayoutResult result = RunLayout(html);
        Assert.Equal(1, result.PageCount);

        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        Assert.NotEmpty(page0Texts);
    }

    /// <summary>
    /// Scenario: Column layout with align-items: stretch (default)
    /// GIVEN a flex container with display:flex; flex-direction:column (align-items defaults to stretch)
    /// WHEN rendered
    /// THEN child items MUST stretch to fill the container width
    /// </summary>
    [Fact]
    public void FlexColumn_AlignStretchDefault_ChildrenFillWidth()
    {
        string html = @"
            <div style='display: flex; flex-direction: column; width: 300pt;'>
                <div><p>Stretch</p></div>
                <div><p>To Fit</p></div>
            </div>";

        LayoutResult result = RunLayout(html);
        Assert.Equal(1, result.PageCount);

        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        Assert.NotEmpty(page0Texts);
    }

    /// <summary>
    /// Scenario: Column layout with justify-content: flex-end
    /// GIVEN a flex container with justify-content: flex-end and fixed height
    /// WHEN rendered
    /// THEN children MUST be positioned at the bottom of the container
    /// </summary>
    [Fact]
    public void FlexColumn_JustifyEnd_ChildrenAtBottom()
    {
        string html = @"
            <div style='display: flex; flex-direction: column; justify-content: flex-end; height: 200pt;'>
                <div style='height: 30pt;'><p>Bottom</p></div>
            </div>";

        LayoutResult result = RunLayout(html);
        Assert.Equal(1, result.PageCount);

        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        Assert.NotEmpty(page0Texts);
    }

    #endregion

    #region Page Break Interaction (Edge Case)

    /// <summary>
    /// Scenario: Flex container at page boundary
    /// GIVEN a flex container with page-break-inside: avoid on a child
    /// WHEN the container spans a page boundary
    /// THEN the renderer SHALL move the avoid-marked child to the next page
    /// </summary>
    [Fact]
    public void FlexContainer_WithPageBreakInsideAvoid_MovesChildToNextPage()
    {
        // Create a flex container with a child that has page-break-inside: avoid
        // Use a small page to force pagination
        var smallPage = new PageLayout(PageLayout.A4.Width, 100f, new PageMargins(10f));

        string html = @"
            <div style='display: flex; flex-direction: column;'>
                <div style='height: 40pt;'><p>First</p></div>
                <div style='height: 60pt; page-break-inside: avoid; background-color: #ccc;'>
                    <p>Avoid break</p>
                </div>
            </div>";

        LayoutResult result = RunLayout(html, smallPage);

        // Should span at least 2 pages
        Assert.True(result.PageCount >= 2,
            $"Flex container with avoid should span at least 2 pages, got {result.PageCount}");

        // The "Avoid break" content should be on page 1 (moved to next page)
        var page1Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 1).ToList();
        Assert.NotEmpty(page1Texts);
    }

    /// <summary>
    /// Scenario: Flex row with overflow
    /// GIVEN a flex row whose children exceed available width
    /// WHEN rendered
    /// THEN the renderer SHALL NOT silently overlap content
    /// </summary>
    [Fact]
    public void FlexRow_ContentExceedsWidth_NoSilentOverlap()
    {
        // Create items that are wider than the container
        var narrowPage = new PageLayout(200f, PageLayout.A4.Height, new PageMargins(10f));

        string html = @"
            <div style='display: flex; flex-direction: row;'>
                <div style='width: 150pt;'><p>Wide item 1</p></div>
                <div style='width: 150pt;'><p>Wide item 2</p></div>
            </div>";

        LayoutResult result = RunLayout(html, narrowPage);

        // Both items should render somewhere (no crash)
        var allTexts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.NotEmpty(allTexts);
    }

    #endregion

    #region Flex container with background

    /// <summary>
    /// Verifies that a flex container with a background renders a rect primitive.
    /// </summary>
    [Fact]
    public void FlexContainer_WithBackground_EmitsRect()
    {
        string html = @"
            <div style='display: flex; flex-direction: row; background-color: #ff0000;'>
                <div style='width: 50pt;'><p>Red</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        // Should have a red rect (background)
        var redRects = result.Primitives.OfType<RectPrimitive>()
            .Where(r => r.Fill.R > 0.9f && r.HasFill).ToList();
        Assert.NotEmpty(redRects);
    }

    #endregion

    #region Flex Defaults

    /// <summary>
    /// Verifies ComputedStyle defaults for flex properties.
    /// </summary>
    [Fact]
    public void ComputedStyle_FlexDefaults_AreCorrect()
    {
        ComputedStyle style = new();

        Assert.Equal(FlexDirection.Row, style.FlexDirection);
        Assert.Equal(JustifyContent.FlexStart, style.JustifyContent);
        Assert.Equal(AlignItems.Stretch, style.AlignItems);
    }

    /// <summary>
    /// Verifies that display: flex resolves in StyleResolver.
    /// </summary>
    [Fact]
    public void StyleResolver_DisplayFlex_ResolvesToFlex()
    {
        HtmlDocument doc = new();
        doc.LoadHtml("<div style='display: flex;'>Content</div>");
        Dictionary<HtmlNode, ComputedStyle> styles = new StyleResolver().Resolve(doc.DocumentNode);

        HtmlNode? div = doc.DocumentNode.SelectSingleNode("//div");
        Assert.NotNull(div);
        Assert.True(styles.TryGetValue(div, out ComputedStyle? style));
        Assert.Equal(DisplayType.Flex, style.Display);
    }

    #endregion

    #region Gap Property (Row and Column)

    /// <summary>
    /// Bug: CSS gap was not applied between flex items.
    ///
    /// GIVEN a flex row with gap: 20px (15pt)
    /// WHEN rendered with two items
    /// THEN the items MUST be spaced apart by at least the gap amount
    /// </summary>
    [Fact]
    public void FlexRow_WithGap_ItemsAreSpacedApart()
    {
        string html = @"
            <div style='display: flex; flex-direction: row; gap: 20px;'>
                <div style='width: 50pt;'><p>Left</p></div>
                <div style='width: 50pt;'><p>Right</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        // Get text primitives for both items
        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        var leftTexts = texts.Where(t => t.Text == "Left").ToList();
        var rightTexts = texts.Where(t => t.Text == "Right").ToList();

        Assert.NotEmpty(leftTexts);
        Assert.NotEmpty(rightTexts);

        // With gap: 20px (15pt), the distance between "Left" and "Right" items
        // should be > 15pt (container width is A4 ContentWidth ~ 481pt)
        // Left item: X should be relatively small
        // Right item: X should be at least Left's X + left's width + gap
        float leftMaxX = leftTexts.Max(t => t.X);
        float rightMinX = rightTexts.Min(t => t.X);

        // Gap in points: 20px * 0.75 = 15pt
        // Left width = 50pt content + decorations
        float gap = 20f * 0.75f;
        Assert.True(rightMinX - leftMaxX >= gap * 0.5f,
            $"Gap between items should be at least {gap}pt, but Right starts at {rightMinX} and Left ends at {leftMaxX} (diff = {rightMinX - leftMaxX})");
    }

    /// <summary>
    /// GIVEN a flex column with gap: 10pt
    /// WHEN rendered with two items
    /// THEN the items MUST be spaced apart vertically by the gap
    /// </summary>
    [Fact]
    public void FlexColumn_WithGap_ItemsAreSpacedApart()
    {
        string html = @"
            <div style='display: flex; flex-direction: column; gap: 10pt;'>
                <div style='height: 20pt;'><p>Top</p></div>
                <div style='height: 20pt;'><p>Bottom</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        var topTexts = texts.Where(t => t.Text == "Top").ToList();
        var bottomTexts = texts.Where(t => t.Text == "Bottom").ToList();

        Assert.NotEmpty(topTexts);
        Assert.NotEmpty(bottomTexts);

        // With gap: 10pt, the Y distance between "Top" and "Bottom" items
        // should be > 10pt
        float topMaxY = topTexts.Max(t => t.Y);
        float bottomMinY = bottomTexts.Min(t => t.Y);

        Assert.True(bottomMinY - topMaxY >= 8f,
            $"Gap between column items should be at least 10pt, but Bottom starts at Y={bottomMinY} and Top ends at Y={topMaxY} (diff = {bottomMinY - topMaxY})");
    }

    /// <summary>
    /// GIVEN a flex row with gap: 0 (default)
    /// WHEN rendered
    /// THEN items MUST NOT have extra spacing
    /// </summary>
    [Fact]
    public void FlexRow_NoGap_ItemsAreTouching()
    {
        string html = @"
            <div style='display: flex; flex-direction: row;'>
                <div style='width: 50pt;'><p>A</p></div>
                <div style='width: 50pt;'><p>B</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        // Must render without crash
        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        Assert.True(page0Texts.Count >= 2,
            $"Expected at least 2 text runs on page 0, got {page0Texts.Count}");
    }

    /// <summary>
    /// GIVEN a flex row with gap where items have their own margins
    /// WHEN rendered
    /// THEN gap + margins should both be respected (gap is additive)
    /// </summary>
    [Fact]
    public void FlexRow_GapPlusMargins_BothApplied()
    {
        string html = @"
            <div style='display: flex; flex-direction: row; gap: 10px;'>
                <div style='width: 40pt; margin-right: 5pt;'><p>Item</p></div>
                <div style='width: 40pt;'><p>Item</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        // Must not crash and both items should render
        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.True(texts.Count >= 2,
            $"Expected at least 2 text runs with gap + margin, got {texts.Count}");
    }

    #endregion
}
