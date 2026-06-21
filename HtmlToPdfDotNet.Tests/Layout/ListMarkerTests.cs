using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Tests.Layout;

/// <summary>
/// Tests for list marker characters in &lt;ul&gt; and &lt;ol&gt; elements.
///
/// Bug: List markers used characters that were > U+00FF (like \u2022 for bullet),
/// which get replaced by space in standard PDF Type1 fonts (WinAnsi encoding).
/// Fixed: Disc → \u00B7 (MIDDLE DOT), Circle → \u00B0 (DEGREE SIGN),
/// Square → \u25AA (requires embedded font).
/// </summary>
public class ListMarkerTests
{
    private static LayoutResult RunLayout(string html, PageLayout? page = null)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        Dictionary<HtmlNode, ComputedStyle> styles = new StyleResolver().Resolve(doc.DocumentNode);
        BlockLayoutEngine engine = new(page ?? PageLayout.A4, styles);
        return engine.Layout(doc.DocumentNode);
    }

    #region Disc Marker

    /// <summary>
    /// Bug fix: Disc marker must be \u00B7 (MIDDLE DOT, U+00B7) — a Latin-1-safe
    /// character that renders correctly in standard PDF Type1 fonts (Helvetica).
    ///
    /// GIVEN an unordered list with default list-style-type (disc)
    /// WHEN rendered
    /// THEN the output MUST contain a TextPrimitive with \u00B7
    /// </summary>
    [Fact]
    public void UnorderedList_DiscMarker_EmitsMiddleDot()
    {
        string html = @"
            <ul>
                <li>Item A</li>
                <li>Item B</li>
            </ul>";

        LayoutResult result = RunLayout(html);

        // Find marker text primitives (·)
        var markers = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.Text == "\u00B7")
            .ToList();

        Assert.NotEmpty(markers);
    }

    /// <summary>
    /// GIVEN a list-style-type: disc explicitly
    /// WHEN rendered
    /// THEN the marker MUST be \u00B7
    /// </summary>
    [Fact]
    public void ExplicitDisc_UsesMiddleDot()
    {
        string html = @"
            <ul style='list-style-type: disc;'>
                <li>Item</li>
            </ul>";

        LayoutResult result = RunLayout(html);

        Assert.Contains(result.Primitives.OfType<TextPrimitive>(),
            t => t.Text == "\u00B7");
    }

    #endregion

    #region Circle Marker

    /// <summary>
    /// Bug fix: Circle marker must be \u00B0 (DEGREE SIGN, U+00B0) — Latin-1-safe.
    ///
    /// GIVEN a list with list-style-type: circle
    /// WHEN rendered
    /// THEN the marker MUST be \u00B0
    /// </summary>
    [Fact]
    public void CircleMarker_UsesDegreeSign()
    {
        string html = @"
            <ul style='list-style-type: circle;'>
                <li>Item</li>
            </ul>";

        LayoutResult result = RunLayout(html);

        Assert.Contains(result.Primitives.OfType<TextPrimitive>(),
            t => t.Text == "\u00B0");
    }

    #endregion

    #region Square Marker

    /// <summary>
    /// Square marker uses \u25AA (BLACK SMALL SQUARE, U+25AA).
    /// This is NOT Latin-1-safe (U+25AA > U+00FF), so it requires
    /// an embedded font to render correctly.
    ///
    /// GIVEN a list with list-style-type: square
    /// WHEN rendered
    /// THEN the marker MUST be \u25AA
    /// </summary>
    [Fact]
    public void SquareMarker_UsesBlackSmallSquare()
    {
        string html = @"
            <ul style='list-style-type: square;'>
                <li>Item</li>
            </ul>";

        LayoutResult result = RunLayout(html);

        Assert.Contains(result.Primitives.OfType<TextPrimitive>(),
            t => t.Text == "\u25AA");
    }

    #endregion

    #region Ordered List Markers (Unchanged)

    /// <summary>
    /// Ordered list (decimal) markers must render correctly.
    /// </summary>
    [Fact]
    public void OrderedList_DecimalMarker_RendersCounter()
    {
        string html = @"
            <ol>
                <li>First</li>
                <li>Second</li>
            </ol>";

        LayoutResult result = RunLayout(html);

        // Should find "1." and "2." markers
        var markers = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.Text == "1." || t.Text == "2.")
            .ToList();

        Assert.True(markers.Count >= 2,
            $"Expected 2 decimal markers (1., 2.), got {markers.Count}");
    }

    /// <summary>
    /// List with list-style-type: none must NOT emit a marker.
    /// </summary>
    [Fact]
    public void ListStyleNone_NoMarkerEmitted()
    {
        string html = @"
            <ul style='list-style-type: none;'>
                <li>Item</li>
            </ul>";

        LayoutResult result = RunLayout(html);

        // The "Item" text should still appear
        Assert.Contains(result.Primitives.OfType<TextPrimitive>(),
            t => t.Text == "Item");

        // But no bullet/marker text should be present
        // (Note: this counts all text runs, not just markers — but it's a sanity check)
        var markerlike = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.Text == "\u00B7" || t.Text == "\u00B0" || t.Text == "\u25AA")
            .ToList();

        Assert.Empty(markerlike);
    }

    #endregion

    #region Multiple Items

    /// <summary>
    /// Multiple list items each get their own marker.
    /// </summary>
    [Fact]
    public void MultipleListItems_EachHasMarker()
    {
        string html = @"
            <ul>
                <li>Alpha</li>
                <li>Bravo</li>
                <li>Charlie</li>
            </ul>";

        LayoutResult result = RunLayout(html);

        var markers = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.Text == "\u00B7")
            .ToList();

        // Each of the 3 items should have a marker
        Assert.True(markers.Count >= 3,
            $"Expected at least 3 disc markers, got {markers.Count}");
    }

    #endregion
}
