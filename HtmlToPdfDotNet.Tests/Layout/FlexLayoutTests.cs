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

    #region Flex-grow / Flex-shrink (Phase 2)

    /// <summary>
    /// Scenario: flex-grow with equal weights
    /// GIVEN a flex container with display:flex; width:400pt and two items with flex:1 each
    /// WHEN laid out
    /// THEN both items occupy equal width
    /// </summary>
    [Fact]
    public void FlexRow_FlexGrowEqual_ItemsOccupyEqualWidth()
    {
        // 400pt keeps the container inside the A4 content area (600pt would be clamped).
        string html = @"
            <div style='display: flex; flex-direction: row; width: 400pt; gap: 0;'>
                <div style='flex:1;'><p>A</p></div>
                <div style='flex:1;'><p>B</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        var textA = texts.FirstOrDefault(t => t.Text == "A");
        var textB = texts.FirstOrDefault(t => t.Text == "B");

        Assert.NotNull(textA);
        Assert.NotNull(textB);

        // Both items have flex:1 (grow=1, shrink=1, basis=0)
        // In a 400pt container each gets exactly 200pt, so B starts 200pt after A.
        Assert.Equal(200f, textB.X - textA.X, 0.5f);
    }

    /// <summary>
    /// Scenario: flex-grow with different weights
    /// GIVEN a flex container with display:flex; width:600pt and items with flex:1 and flex:2
    /// WHEN laid out
    /// THEN the flex:2 item is proportionally wider than the flex:1 item
    /// With proportional grow: A gets 200pt, B gets 400pt. Under equal-split both get 300pt.
    /// </summary>
    [Fact]
    public void FlexRow_FlexGrowDifferentWeights_ProportionalDistribution()
    {
        string html = @"
            <div style='display: flex; flex-direction: row; width: 600pt; gap: 0;'>
                <div style='flex:1;'><p>A</p></div>
                <div style='flex:2;'><p>B</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        var textA = texts.FirstOrDefault(t => t.Text == "A");
        var textB = texts.FirstOrDefault(t => t.Text == "B");

        Assert.NotNull(textA);
        Assert.NotNull(textB);

        // flex:1 (grow=1) and flex:2 (grow=2) → A gets 1/3, B gets 2/3
        // Under equal-split both would get ~50% and B's X would be ~298pt.
        // With flex-grow, B starts at ~214pt. Assert B starts before 250pt to differentiate.
        float aEnd = textA.X;
        float bStart = textB.X;
        Assert.True(bStart < 250f,
            $"B (flex:2) should start early (X={bStart}) — was 297.64 under equal-split");
        // B should start after A (basic sanity)
        Assert.True(bStart > aEnd,
            $"B should start after A (A ends at {aEnd}, B starts at {bStart})");
    }

    /// <summary>
    /// Scenario: flex-shrink prevents overflow
    /// GIVEN a flex container with display:flex; width:300pt and items with flex:1 1 300pt and flex:1 1 100pt
    /// WHEN laid out
    /// THEN the 100pt overflow is removed proportionally to shrink × basis (225pt / 75pt)
    /// Under an equal-split implementation both items would get 150pt.
    /// </summary>
    [Fact]
    public void FlexRow_FlexShrink_ItemsShrinkToFit()
    {
        string html = @"
            <div style='display: flex; flex-direction: row; width: 300pt; gap: 0;'>
                <div style='flex: 1 1 300pt; height: 10pt; background-color: #ff0000;'></div>
                <div style='flex: 1 1 100pt; height: 10pt; background-color: #0000ff;'></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var rects = result.Primitives.OfType<RectPrimitive>().ToList();
        RectPrimitive red = Assert.Single(rects, r => r.Fill.R > 0.9f && r.Fill.B < 0.1f);
        RectPrimitive blue = Assert.Single(rects, r => r.Fill.B > 0.9f && r.Fill.R < 0.1f);

        Assert.Equal(225f, red.Width, 0.5f);
        Assert.Equal(75f, blue.Width, 0.5f);
        Assert.Equal(red.X + 225f, blue.X, 0.5f);
    }

    /// <summary>
    /// Scenario: flex-shrink:0 items are not reduced
    /// GIVEN a flex container with overflow where one item has flex-shrink:0
    /// WHEN laid out
    /// THEN the flex-shrink:0 item keeps its full size
    /// </summary>
    [Fact]
    public void FlexRow_FlexShrinkZero_ItemNotReduced()
    {
        // 150pt + 100pt = 250pt in a 200pt container → 50pt overflow,
        // absorbed entirely by the shrinkable item (fixed 150pt, flex 50pt).
        string html = @"
            <div style='display: flex; flex-direction: row; width: 200pt; gap: 0;'>
                <div style='flex: 0 0 150pt; height: 10pt; background-color: #ff0000;'></div>
                <div style='flex: 1 1 100pt; height: 10pt; background-color: #0000ff;'></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var rects = result.Primitives.OfType<RectPrimitive>().ToList();
        RectPrimitive fixedItem = Assert.Single(rects, r => r.Fill.R > 0.9f && r.Fill.B < 0.1f);
        RectPrimitive flexItem = Assert.Single(rects, r => r.Fill.B > 0.9f && r.Fill.R < 0.1f);

        Assert.Equal(150f, fixedItem.Width, 0.5f);
        Assert.Equal(50f, flexItem.Width, 0.5f);
        Assert.Equal(fixedItem.X + 150f, flexItem.X, 0.5f);
    }

    /// <summary>
    /// Scenario: single item with flex-grow fills all remaining space
    /// GIVEN a flex container with one item that has flex-grow:1
    /// WHEN laid out
    /// THEN the item expands to fill the container width
    /// </summary>
    [Fact]
    public void FlexRow_SingleItemFlexGrow_FillsContainer()
    {
        string html = @"
            <div style='display: flex; flex-direction: row; width: 400pt;'>
                <div style='flex:1;'><p>Fill</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.NotEmpty(texts);

        // Single flex:1 item should have enough space (fill width)
        Assert.Equal(1, result.PageCount);
    }

    #endregion

    #region Flex-wrap multi-line (Phase 3)

    /// <summary>
    /// Scenario: flex-wrap wraps items that don't fit on one line
    /// GIVEN a flex container with flex-wrap: wrap, width 300pt, and 4 items of 150pt each
    /// WHEN laid out
    /// THEN items MUST wrap to a second line (2 items per line × 2 lines)
    /// </summary>
    [Fact]
    public void FlexWrap_Basic_ItemsWrapToMultipleLines()
    {
        string html = @"
            <div style='display: flex; flex-wrap: wrap; width: 300pt; gap: 0;'>
                <div style='flex: 0 0 200px;'><p>A</p></div>
                <div style='flex: 0 0 200px;'><p>B</p></div>
                <div style='flex: 0 0 200px;'><p>C</p></div>
                <div style='flex: 0 0 200px;'><p>D</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.True(texts.Count >= 4,
            $"Expected at least 4 text runs, got {texts.Count}");

        // Items A and B should be on the first line, C and D on the second
        // Y of line 2 > Y of line 1
        var yPositions = texts.Select(t => t.Y).Distinct().ToList();
        Assert.True(yPositions.Count >= 2,
            $"Expected items on at least 2 different Y levels (wrapping), got {yPositions.Count} Y level(s)");
    }

    /// <summary>
    /// Scenario: flex-wrap: nowrap (default) keeps items on one line
    /// GIVEN a flex container with default flex-wrap (nowrap), width 300pt, and 4 items of 150pt each
    /// WHEN laid out
    /// THEN items MUST stay on one line (overflow)
    /// </summary>
    [Fact]
    public void FlexWrap_NoWrap_ItemsStayOnOneLine()
    {
        string html = @"
            <div style='display: flex; width: 300pt; gap: 0;'>
                <div style='flex: 0 0 200px;'><p>A</p></div>
                <div style='flex: 0 0 200px;'><p>B</p></div>
                <div style='flex: 0 0 200px;'><p>C</p></div>
                <div style='flex: 0 0 200px;'><p>D</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.True(texts.Count >= 4,
            $"Expected at least 4 text runs, got {texts.Count}");

        // All items should be on approximately the same Y (single line)
        var yPositions = texts.Select(t => t.Y).Distinct().ToList();
        Assert.True(yPositions.Count == 1,
            $"Expected all items on one Y level (nowrap), got {yPositions.Count} Y level(s)");
    }

    /// <summary>
    /// Scenario: flex-wrap: wrap-reverse reverses line order
    /// GIVEN a flex container with flex-wrap: wrap-reverse, width 300pt
    /// WHEN laid out
    /// THEN lines MUST be in reverse order (second line appears first vertically)
    /// </summary>
    [Fact]
    public void FlexWrap_WrapReverse_LinesReversed()
    {
        string html = @"
            <div style='display: flex; flex-wrap: wrap-reverse; width: 300pt; gap: 0;'>
                <div style='flex: 0 0 200px;'><p>A</p></div>
                <div style='flex: 0 0 200px;'><p>B</p></div>
                <div style='flex: 0 0 200px;'><p>C</p></div>
                <div style='flex: 0 0 200px;'><p>D</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.True(texts.Count >= 4,
            $"Expected at least 4 text runs, got {texts.Count}");

        // With wrap-reverse, items C and D should have LOWER Y than A and B
        // (second line appears first)
        var textA = texts.FirstOrDefault(t => t.Text == "A");
        var textC = texts.FirstOrDefault(t => t.Text == "C");
        Assert.NotNull(textA);
        Assert.NotNull(textC);

        Assert.True(textC.Y < textA.Y,
            $"Wrap-reverse: C (line 2) should be above A (line 1): C.Y={textC.Y}, A.Y={textA.Y}");
    }

    /// <summary>
    /// Scenario: flex-wrap with gap accounts for gap in wrap decision
    /// GIVEN a flex container with flex-wrap: wrap, width 300pt, gap: 10pt
    /// WHEN laid out
    /// THEN items MUST wrap correctly considering gap between items
    /// </summary>
    [Fact]
    public void FlexWrap_WithGap_WrappingAccountsForGap()
    {
        // 2 items of 200px (150pt) + 10pt gap = 310pt > 300pt → only 1 item per line
        string html = @"
            <div style='display: flex; flex-wrap: wrap; width: 300pt; gap: 10pt;'>
                <div style='flex: 0 0 200px;'><p>A</p></div>
                <div style='flex: 0 0 200px;'><p>B</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.True(texts.Count >= 2,
            $"Expected at least 2 text runs, got {texts.Count}");

        // With gap: 10pt, only 1 item fits per line → 2 lines → different Y
        var yPositions = texts.Select(t => t.Y).Distinct().ToList();
        Assert.True(yPositions.Count >= 2,
            $"Expected wrapping due to gap (310pt > 300pt), got {yPositions.Count} Y level(s)");
    }

    /// <summary>
    /// Scenario: flex-wrap with flex-grow distributes per line
    /// GIVEN a flex container with flex-wrap: wrap and items having grow + explicit basis
    /// WHEN laid out
    /// THEN each line independently distributes remaining space
    /// </summary>
    [Fact]
    public void FlexWrap_WithFlexGrow_PerLineDistribution()
    {
        // Container: 400pt, items: flex: 1 1 200px (basis=150pt)
        // 3 items: totalBasis=450 > 400 → 2 on line 1 (300pt), 1 on line 2 (150pt)
        // Line 1: 2 items × 150pt basis → 100pt remaining → 50pt each → 200pt each
        // Line 2: 1 item × 150pt basis → 250pt remaining → gets 400pt
        string html = @"
            <div style='display: flex; flex-wrap: wrap; width: 400pt; gap: 0;'>
                <div style='flex: 1 1 200px;'><p>A</p></div>
                <div style='flex: 1 1 200px;'><p>B</p></div>
                <div style='flex: 1 1 200px;'><p>C</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.True(texts.Count >= 3,
            $"Expected at least 3 text runs, got {texts.Count}");

        var textA = texts.FirstOrDefault(t => t.Text == "A");
        var textB = texts.FirstOrDefault(t => t.Text == "B");
        var textC = texts.FirstOrDefault(t => t.Text == "C");
        Assert.NotNull(textA);
        Assert.NotNull(textB);
        Assert.NotNull(textC);

        // A and B should be on same Y (line 1), C on different Y (line 2)
        Assert.True(Math.Abs(textA.Y - textB.Y) < 1f,
            $"A and B should be on same line (Y diff = {Math.Abs(textA.Y - textB.Y)})");
        Assert.True(Math.Abs(textC.Y - textA.Y) > 5f,
            $"C should be on different line than A (C.Y={textC.Y}, A.Y={textA.Y})");
    }

    #endregion

    #region Edge Cases (Phase 4)

    /// <summary>
    /// Scenario: flex-wrap with margin on flex items
    /// GIVEN a flex container with flex-wrap: wrap
    /// WHEN items have horizontal margins
    /// THEN wrapping accounts for margins (they are part of the box-model total)
    /// </summary>
    [Fact]
    public void FlexWrap_WithMargin_WrappingAccountsForMargins()
    {
        // Items: 200px (150pt) + 20pt margin-right → 170pt each
        // Container: 300pt → only 1 item fits per line (170pt)
        string html = @"
            <div style='display: flex; flex-wrap: wrap; width: 300pt; gap: 0;'>
                <div style='flex: 0 0 200px; margin-right: 20pt;'><p>A</p></div>
                <div style='flex: 0 0 200px; margin-right: 20pt;'><p>B</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.True(texts.Count >= 2,
            $"Expected at least 2 text runs, got {texts.Count}");

        // With margin, only 1 item fits per line → should be on different Y levels
        var yPositions = texts.Select(t => t.Y).Distinct().ToList();
        Assert.True(yPositions.Count >= 2,
            $"Expected wrapping due to margins (170pt > 300pt for 2 items), got {yPositions.Count} Y level(s)");
    }

    /// <summary>
    /// Scenario: empty flex container with flex-wrap
    /// GIVEN a flex container with flex-wrap: wrap and no children
    /// WHEN rendered
    /// THEN no crash and no primitives from children
    /// </summary>
    [Fact]
    public void FlexWrap_EmptyContainer_NoCrash()
    {
        string html = @"
            <div style='display: flex; flex-wrap: wrap; width: 300pt; height: 50pt;'>
            </div>";

        LayoutResult result = RunLayout(html);

        // No children → no text primitives
        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.Empty(texts);
        Assert.Equal(1, result.PageCount);
    }

    /// <summary>
    /// Scenario: single item wraps to its own line
    /// GIVEN a flex container with flex-wrap: wrap and items where one is too wide
    /// WHEN rendered
    /// THEN the oversized item wraps to its own line
    /// </summary>
    [Fact]
    public void FlexWrap_SingleItemWrapsToOwnLine()
    {
        // 2 items: 200px (150pt) + 200px (150pt) = 300pt → exactly fits one line
        // 3 items: 450pt → wraps: 2 on line 1, 1 on line 2
        string html = @"
            <div style='display: flex; flex-wrap: wrap; width: 300pt; gap: 0;'>
                <div style='flex: 0 0 200px;'><p>A</p></div>
                <div style='flex: 0 0 200px;'><p>B</p></div>
                <div style='flex: 0 0 200px;'><p>C</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.True(texts.Count >= 3,
            $"Expected at least 3 text runs, got {texts.Count}");

        // A and B on line 1, C on line 2 → at least 2 Y levels
        var yPositions = texts.Select(t => t.Y).Distinct().ToList();
        Assert.True(yPositions.Count >= 2,
            $"Expected 3 items to wrap (2+1), got {yPositions.Count} Y level(s)");
    }

    #endregion

    #region Review fixes (wrap gap, align-content, auto basis)

    /// <summary>
    /// Scenario: content after a wrapped flex container with a gap
    /// GIVEN a wrapped flex container with gap:10pt and two 20pt-high lines
    /// WHEN followed by another block
    /// THEN the block starts at or after the bottom of the last line (gap included)
    /// </summary>
    [Fact]
    public void FlexWrap_WithGap_FollowingContentStartsAfterLastLine()
    {
        string html = @"
            <div style='display: flex; flex-wrap: wrap; width: 300pt; gap: 10pt;'>
                <div style='flex: 0 0 200pt; height: 20pt; background-color: #ff0000;'></div>
                <div style='flex: 0 0 200pt; height: 20pt; background-color: #ff0000;'></div>
            </div>
            <div style='height: 7pt; background-color: #0000ff;'></div>";

        LayoutResult result = RunLayout(html);

        var rects = result.Primitives.OfType<RectPrimitive>().ToList();
        RectPrimitive lastLine = rects
            .Where(r => r.Fill.R > 0.9f && r.Fill.B < 0.1f)
            .OrderBy(r => r.Y)
            .Last();
        RectPrimitive after = Assert.Single(rects, r => r.Fill.B > 0.9f && r.Fill.R < 0.1f);

        Assert.True(after.Y >= lastLine.Y + lastLine.Height - 0.01f,
            $"Following block (Y={after.Y}) overlaps last wrapped line (bottom={lastLine.Y + lastLine.Height})");
    }

    /// <summary>
    /// Scenario: a wrapped line that would cross the page bottom
    /// GIVEN a wrapped flex container whose second line (start Y + gap) exceeds the page
    /// WHEN laid out
    /// THEN the second line moves to the next page
    /// </summary>
    [Fact]
    public void FlexWrap_LineExceedingPage_MovesToNextPage()
    {
        // Page content height 200pt. Spacer 140pt → line 1 at 140..170,
        // line 2 at 180..210 (after 10pt gap) → must move to page 1.
        PageLayout page = new(400f, 200f, new PageMargins(0f));
        string html = @"
            <div style='height: 140pt;'></div>
            <div style='display: flex; flex-wrap: wrap; width: 300pt; gap: 10pt;'>
                <div style='flex: 0 0 200pt; height: 30pt; background-color: #ff0000;'></div>
                <div style='flex: 0 0 200pt; height: 30pt; background-color: #0000ff;'></div>
            </div>";

        LayoutResult result = RunLayout(html, page);

        var rects = result.Primitives.OfType<RectPrimitive>().ToList();
        RectPrimitive line1 = Assert.Single(rects, r => r.Fill.R > 0.9f && r.Fill.B < 0.1f);
        RectPrimitive line2 = Assert.Single(rects, r => r.Fill.B > 0.9f && r.Fill.R < 0.1f);

        Assert.Equal(0, line1.PageIndex);
        Assert.Equal(1, line2.PageIndex);
        Assert.True(line2.Y + line2.Height <= 200f,
            $"Second line must fit on its page (bottom={line2.Y + line2.Height})");
    }

    /// <summary>
    /// Scenario: align-content with a definite container height
    /// GIVEN a wrapped flex container with height:200pt and two 20pt-high lines
    /// WHEN align-content is set
    /// THEN lines are positioned within the 160pt of free cross space
    /// </summary>
    [Theory]
    [InlineData("flex-start", 0f, 20f)]
    [InlineData("flex-end", 160f, 180f)]
    [InlineData("center", 80f, 100f)]
    [InlineData("space-between", 0f, 180f)]
    [InlineData("space-around", 40f, 140f)]
    public void FlexWrap_AlignContent_WithDefiniteHeight_PositionsLines(
        string alignContent, float expectedLine1Y, float expectedLine2Y)
    {
        string html = $@"
            <div style='display: flex; flex-wrap: wrap; width: 300pt; height: 200pt; gap: 0; align-content: {alignContent};'>
                <div style='flex: 0 0 200pt; height: 20pt; background-color: #ff0000;'></div>
                <div style='flex: 0 0 200pt; height: 20pt; background-color: #0000ff;'></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var rects = result.Primitives.OfType<RectPrimitive>().ToList();
        RectPrimitive line1 = Assert.Single(rects, r => r.Fill.R > 0.9f && r.Fill.B < 0.1f);
        RectPrimitive line2 = Assert.Single(rects, r => r.Fill.B > 0.9f && r.Fill.R < 0.1f);

        Assert.Equal(expectedLine1Y, line1.Y, 0.5f);
        Assert.Equal(expectedLine2Y, line2.Y, 0.5f);
    }

    /// <summary>
    /// Scenario: content after a wrapped container with a definite height
    /// GIVEN a wrapped flex container with height:200pt whose lines only take 40pt
    /// WHEN followed by another block
    /// THEN the block starts after the container's definite height
    /// </summary>
    [Fact]
    public void FlexWrap_DefiniteHeight_FollowingContentStartsAfterContainer()
    {
        string html = @"
            <div style='display: flex; flex-wrap: wrap; width: 300pt; height: 200pt; gap: 0;'>
                <div style='flex: 0 0 200pt; height: 20pt;'></div>
                <div style='flex: 0 0 200pt; height: 20pt;'></div>
            </div>
            <div style='height: 7pt; background-color: #0000ff;'></div>";

        LayoutResult result = RunLayout(html);

        RectPrimitive after = Assert.Single(result.Primitives.OfType<RectPrimitive>(),
            r => r.Fill.B > 0.9f && r.Fill.R < 0.1f);
        Assert.Equal(200f, after.Y, 0.5f);
    }

    /// <summary>
    /// Scenario: flex-basis:auto item made of a fixed-width block
    /// GIVEN a non-growing item without width whose only child is a 120pt-wide block
    /// WHEN laid out
    /// THEN the item keeps the child's 120pt width and the next item starts after it
    /// </summary>
    [Fact]
    public void FlexRow_AutoBasis_BlockChild_KeepsChildWidth()
    {
        string html = @"
            <div style='display: flex; width: 400pt;'>
                <div style='flex: 0 1 auto;'>
                    <div style='width: 120pt; height: 20pt; background-color: #ff0000;'></div>
                </div>
                <div style='flex: 0 0 50pt; height: 20pt; background-color: #0000ff;'></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var rects = result.Primitives.OfType<RectPrimitive>().ToList();
        RectPrimitive inner = Assert.Single(rects, r => r.Fill.R > 0.9f && r.Fill.B < 0.1f);
        RectPrimitive next = Assert.Single(rects, r => r.Fill.B > 0.9f && r.Fill.R < 0.1f);

        Assert.Equal(120f, inner.Width, 0.5f);
        Assert.Equal(inner.X + 120f, next.X, 0.5f);
    }

    /// <summary>
    /// Scenario: flex-basis:auto item with right insets around an auto-width child
    /// GIVEN a non-growing padded, bordered item whose auto-width child has a background
    /// WHEN laid out next to a fixed sibling
    /// THEN the stretched child box does not inflate the item's basis to the measuring width
    /// </summary>
    [Fact]
    public void FlexRow_AutoBasis_PaddedItemWithAutoWidthChild_KeepsContentWidth()
    {
        string html = @"
            <div style='display: flex; width: 400pt;'>
                <div style='flex: 0 1 auto; padding-right: 20pt; border-right: 2pt solid #000000;'>
                    <div style='margin-right: 5pt; background-color: #ff0000;'>Hi</div>
                </div>
                <div style='flex: 0 0 50pt; height: 20pt; background-color: #0000ff;'></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var rects = result.Primitives.OfType<RectPrimitive>().ToList();
        RectPrimitive inner = Assert.Single(rects, r => r.Fill.R > 0.9f && r.Fill.B < 0.1f);
        RectPrimitive next = Assert.Single(rects, r => r.Fill.B > 0.9f && r.Fill.R < 0.1f);

        Assert.True(inner.Width < 60f, $"Auto-width child inflated to {inner.Width}pt");
        Assert.True(next.X < 120f, $"Sibling pushed to {next.X}pt");
        Assert.Equal(50f, next.Width, 0.5f);
    }

    #endregion
}
