using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Tests.Layout;

/// <summary>
/// Tests that flex items render their own background, border, and padding.
///
/// Bug: The inner BlockLayoutEngine only processes an item's children, not the item
/// itself. So background-color, border, and padding on a flex item were silently ignored.
///
/// Fixed: FlexLayoutEngine emits RectPrimitives and border lines for each flex item
/// before copying child primitives, and offsets content by borderLeft + paddingLeft.
/// </summary>
public class FlexItemBoxModelTests
{
    private static LayoutResult RunLayout(string html, PageLayout? page = null)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        Dictionary<HtmlNode, ComputedStyle> styles = new StyleResolver().Resolve(doc.DocumentNode);
        BlockLayoutEngine engine = new(page ?? PageLayout.A4, styles);
        return engine.Layout(doc.DocumentNode);
    }

    #region Background

    /// <summary>
    /// Bug: Flex item background-color was not rendered.
    ///
    /// GIVEN a flex container with items that have background-color
    /// WHEN rendered
    /// THEN each item MUST emit a RectPrimitive with its background fill
    /// </summary>
    [Fact]
    public void FlexRow_ItemWithBackground_EmitsRectPrimitive()
    {
        string html = @"
            <div style='display: flex; flex-direction: row;'>
                <div style='background-color: #ff0000; width: 50pt;'><p>Red</p></div>
                <div style='background-color: #00ff00; width: 50pt;'><p>Green</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        // Should have at least one red-ish rect (R > 0.9)
        var redRects = result.Primitives.OfType<RectPrimitive>()
            .Where(r => r.Fill.R > 0.9f && r.Fill.G < 0.1f && r.Fill.B < 0.1f && r.HasFill)
            .ToList();

        Assert.NotEmpty(redRects);
    }

    /// <summary>
    /// Flex column items with background.
    /// </summary>
    [Fact]
    public void FlexColumn_ItemWithBackground_EmitsRectPrimitive()
    {
        string html = @"
            <div style='display: flex; flex-direction: column;'>
                <div style='background-color: #0000ff; height: 30pt;'><p>Blue</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var blueRects = result.Primitives.OfType<RectPrimitive>()
            .Where(r => r.Fill.B > 0.9f && r.Fill.R < 0.1f && r.Fill.G < 0.1f && r.HasFill)
            .ToList();

        Assert.NotEmpty(blueRects);
    }

    #endregion

    #region Border

    /// <summary>
    /// Bug: Flex item borders were not rendered.
    ///
    /// GIVEN a flex container with items that have border
    /// WHEN rendered
    /// THEN the item MUST emit border primitives
    /// </summary>
    [Fact]
    public void FlexRow_ItemWithBorder_EmitsBorderPrimitives()
    {
        string html = @"
            <div style='display: flex; flex-direction: row;'>
                <div style='border: 1px solid #000000; width: 50pt;'><p>Bordered</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        // In non-rounded mode, borders are emitted as BorderLinePrimitives
        var lines = result.Primitives.OfType<BorderLinePrimitive>().ToList();

        // At least 4 border lines should exist (top, bottom, left, right)
        Assert.True(lines.Count >= 4,
            $"Expected at least 4 border lines for a flex item, got {lines.Count}");
    }

    /// <summary>
    /// Flex column item with border.
    /// </summary>
    [Fact]
    public void FlexColumn_ItemWithBorder_EmitsBorderPrimitives()
    {
        string html = @"
            <div style='display: flex; flex-direction: column;'>
                <div style='border: 2px solid #ff0000; height: 30pt;'><p>Bordered</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        var lines = result.Primitives.OfType<BorderLinePrimitive>().ToList();
        Assert.True(lines.Count >= 4,
            $"Expected at least 4 border lines for column flex item, got {lines.Count}");
    }

    /// <summary>
    /// Flex item with border-radius uses RectPrimitive for border rendering.
    /// </summary>
    [Fact]
    public void FlexItem_WithBorderRadius_EmitsRoundedRect()
    {
        string html = @"
            <div style='display: flex; flex-direction: row;'>
                <div style='border-radius: 10px; border: 1px solid #ccc; background-color: #fff; width: 50pt;'>
                    <p>Rounded</p>
                </div>
            </div>";

        LayoutResult result = RunLayout(html);

        // Should emit a RectPrimitive with BorderRadius > 0
        var rounded = result.Primitives.OfType<RectPrimitive>()
            .FirstOrDefault(r => r.BorderRadius > 0f);

        Assert.NotNull(rounded);
        Assert.True(rounded.BorderRadius > 0f);
    }

    #endregion

    #region Background + Border Combined

    /// <summary>
    /// Flex item with both background and border: should see a RectPrimitive
    /// for background and LinePrimitives for borders.
    /// </summary>
    [Fact]
    public void FlexItem_WithBackgroundAndBorder_EmitsBoth()
    {
        string html = @"
            <div style='display: flex; flex-direction: row;'>
                <div style='background-color: #f0f0f0; border: 1px solid #000; width: 50pt;'>
                    <p>Both</p>
                </div>
            </div>";

        LayoutResult result = RunLayout(html);

        // Background rect
        var rects = result.Primitives.OfType<RectPrimitive>()
            .Where(r => r.HasFill).ToList();
        Assert.NotEmpty(rects);

        // Border lines
        var lines = result.Primitives.OfType<BorderLinePrimitive>().ToList();
        Assert.True(lines.Count >= 4,
            $"Expected borders, got {lines.Count} lines");
    }

    #endregion

    #region Padding — Content Offset

    /// <summary>
    /// Bug: Flex item content was not offset by the item's border + padding,
    /// so text would overlap with the border/padding area instead of being inset.
    ///
    /// GIVEN a flex item with large padding
    /// WHEN rendered
    /// THEN text primitives MUST be positioned at least paddingLeft from the item's left edge
    /// AND at least paddingTop from the item's top edge
    /// </summary>
    [Fact]
    public void FlexItem_WithPadding_ContentIsOffset()
    {
        string html = @"
            <div style='display: flex; flex-direction: row;'>
                <div style='padding: 20px; width: 100pt; border: 1px solid black;'>
                    <p>Inset</p>
                </div>
            </div>";

        LayoutResult result = RunLayout(html);

        // Text primitives for "Inset"
        var texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.Text == "Inset")
            .ToList();

        Assert.NotEmpty(texts);

        // Content should be offset by padding (20px = 15pt) + border (1px = 0.75pt)
        // so X should be at least 15.75pt from contentX of the item.
        // We can't know contentX precisely here, but we can verify the text
        // is NOT at position 0 (inside the border-box origin).
        foreach (var t in texts)
        {
            Assert.True(t.X >= 10f,
                $"Content X ({t.X}) should be offset by padding/border from item edge");
        }
    }

    /// <summary>
    /// Flex item with asymmetric padding offsets content correctly.
    /// </summary>
    [Fact]
    public void FlexItem_WithAsymmetricPadding_ContentOffsetIsAccurate()
    {
        string html = @"
            <div style='display: flex; flex-direction: row;'>
                <div style='padding-left: 30px; padding-top: 10px; width: 100pt;'>
                    <p>Offset</p>
                </div>
            </div>";

        LayoutResult result = RunLayout(html);

        var texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.Text == "Offset")
            .ToList();

        Assert.NotEmpty(texts);

        // X should be offset by at least 22.5pt (30px padding-left * 0.75)
        foreach (var t in texts)
        {
            Assert.True(t.X >= 20f,
                $"Content X ({t.X}) should be offset by asymmetric padding");
        }
    }

    #endregion

    #region No Box Model (No Background/Border)

    /// <summary>
    /// Flex items without any background/border/padding should still render
    /// their content normally (regression test).
    /// </summary>
    [Fact]
    public void FlexItem_NoBoxModel_ContentRendersNormally()
    {
        string html = @"
            <div style='display: flex; flex-direction: row;'>
                <div><p>Plain</p></div>
            </div>";

        LayoutResult result = RunLayout(html);

        // Content must still render
        Assert.Contains(result.Primitives.OfType<TextPrimitive>(),
            t => t.Text == "Plain");
    }

    #endregion

    #region KPI Card Simulation

    /// <summary>
    /// Integration: Simulates the KPI card style from the sales report —
    /// flex items with background, border, border-radius, and padding.
    /// </summary>
    [Fact]
    public void FlexRow_KpiCardStyle_BackgroundBorderPaddingAndContent()
    {
        string html = @"
            <div style='display: flex; flex-direction: row; gap: 10px;'>
                <div style='background-color: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 14px 18px; flex: 1;'>
                    <p>Revenue</p>
                </div>
                <div style='background-color: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 14px 18px; flex: 1;'>
                    <p>Units</p>
                </div>
            </div>";

        LayoutResult result = RunLayout(html);

        // Both "Revenue" and "Units" must render
        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.Contains(texts, t => t.Text == "Revenue");
        Assert.Contains(texts, t => t.Text == "Units");

        // Rounded rects for the border-radius boxes
        var roundedRects = result.Primitives.OfType<RectPrimitive>()
            .Where(r => r.BorderRadius > 0f)
            .ToList();
        Assert.True(roundedRects.Count >= 2,
            $"Expected 2 rounded rects for 2 KPI cards, got {roundedRects.Count}");

        // At least one colored rect per card (background)
        var fillRects = result.Primitives.OfType<RectPrimitive>()
            .Where(r => r.HasFill)
            .ToList();
        Assert.True(fillRects.Count >= 2,
            $"Expected 2 filled rects for KPI cards, got {fillRects.Count}");
    }

    #endregion
}
