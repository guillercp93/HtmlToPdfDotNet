using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Tests.Layout;

/// <summary>
/// Tests for <see cref="StandardFontMetrics"/> class.
/// </summary>
public class StandardFontMetricsTests
{
    [Theory]
    [InlineData("helvetica", false, false, "Helvetica")]
    [InlineData("helvetica", true, false, "Helvetica-Bold")]
    [InlineData("helvetica", false, true, "Helvetica-Oblique")]
    [InlineData("helvetica", true, true, "Helvetica-BoldOblique")]
    [InlineData("times", false, false, "Times-Roman")]
    [InlineData("times", true, false, "Times-Bold")]
    [InlineData("courier", false, false, "Courier")]
    public void Resolve_ReturnsCorrectPdfFontName(string family, bool bold, bool italic, string expected)
    {
        Assert.Equal(expected, StandardFontMetrics.Resolve(family, bold, italic));
    }

    [Fact]
    public void MeasureWidth_EmptyString_ReturnsZero()
    {
        Assert.Equal(0f, StandardFontMetrics.MeasureWidth("", "Helvetica", Constants.DefaultFontSize));
    }

    [Fact]
    public void MeasureWidth_CourierIsMonospace()
    {
        // Todos los caracteres Courier tienen 600 unidades AFM → ancho = 600/1000 * fontSize
        float w1 = StandardFontMetrics.MeasureWidth("A", "Courier", Constants.DefaultFontSize);
        float w2 = StandardFontMetrics.MeasureWidth("i", "Courier", Constants.DefaultFontSize);
        Assert.Equal(w1, w2, precision: 2);
        Assert.Equal(7.2f, w1, precision: 2); // 600/1000 * 12
    }

    [Fact]
    public void MeasureWidth_ScalesWithFontSize()
    {
        float w12 = StandardFontMetrics.MeasureWidth("Hello", "Helvetica", Constants.DefaultFontSize);
        float w24 = StandardFontMetrics.MeasureWidth("Hello", "Helvetica", Constants.DefaultFontSize * 2f);
        Assert.Equal(w12 * 2f, w24, precision: 2);
    }

    [Fact]
    public void MeasureWidth_LongerTextIsWider()
    {
        float wShort = StandardFontMetrics.MeasureWidth("Hi", "Helvetica", Constants.DefaultFontSize);
        float wLong = StandardFontMetrics.MeasureWidth("Hello", "Helvetica", Constants.DefaultFontSize);
        Assert.True(wLong > wShort);
    }
}

/// <summary>
/// Tests for <see cref="InlineLayoutEngine"/> class.
/// </summary>
public class InlineLayoutEngineTests
{
    private static InlineRun MakeRun(string text, float fontSize = Constants.DefaultFontSize) => new()
    {
        Text = text,
        FontName = "Courier",   // monospace → predictable width
        FontSize = fontSize,
        Color = CssColor.Black,
    };

    [Fact]
    public void Layout_ShortText_FitsOnOneLine()
    {
        // "Hi" con Courier 12pt → 2 * 7.2pt = 14.4pt < 200pt
        InlineRun[] runs = [MakeRun("Hi")];
        List<LineBox> lines = InlineLayoutEngine.Layout(runs, availableWidth: 200f);

        Assert.Single(lines);
    }

    [Fact]
    public void Layout_LongText_WrapsToMultipleLines()
    {
        // Courier 12pt → cada char = 7.2pt. 10 chars = 72pt > 50pt → wraps
        InlineRun[] runs = [MakeRun("AAAA BBBB CCCC DDDD")];
        List<LineBox> lines = InlineLayoutEngine.Layout(runs, availableWidth: 50f);

        Assert.True(lines.Count > 1);
    }

    [Fact]
    public void Layout_EmptyText_ReturnsNoLines()
    {
        InlineRun[] runs = [MakeRun("")];
        List<LineBox> lines = InlineLayoutEngine.Layout(runs, availableWidth: 200f);

        Assert.Empty(lines);
    }

    [Fact]
    public void Layout_AlignRight_ShiftsItemsRight()
    {
        InlineRun[] runs = [MakeRun("Hi")];
        List<LineBox> lines = InlineLayoutEngine.Layout(runs, availableWidth: 200f, TextAlign.Right);

        // The first item should be shifted to the right
        (InlineRun _, float x) = lines[0].Items[0];
        Assert.True(x > 0f, $"Expected x > 0, got {x}");
    }

    [Fact]
    public void Layout_AlignCenter_ItemIsRoughlyInMiddle()
    {
        // "Hi" en Courier 12pt → 14.4pt. Centro en 200pt → offset ≈ 92.8pt
        InlineRun[] runs = [MakeRun("Hi")];
        List<LineBox> lines = InlineLayoutEngine.Layout(runs, availableWidth: 200f, TextAlign.Center);

        (InlineRun run, float x) = lines[0].Items[0];
        float expectedOffset = (200f - run.Width) / 2f;
        Assert.Equal(expectedOffset, x, precision: 1);
    }

    [Fact]
    public void Layout_MultipleRuns_MergeOnSameLine()
    {
        // Two short runs should fit on the same line
        InlineRun[] runs = [
            MakeRun("Hello "),
            MakeRun("World"),
        ];
        List<LineBox> lines = InlineLayoutEngine.Layout(runs, availableWidth: 200f);

        Assert.Single(lines);
        Assert.Equal(2, lines[0].Items.Count);
    }

    [Fact]
    public void Layout_LineHeight_IsPositive()
    {
        InlineRun[] runs = [MakeRun("Test line")];
        List<LineBox> lines = InlineLayoutEngine.Layout(runs, availableWidth: 500f);

        Assert.True(lines[0].LineHeight > 0f);
    }
}

/// <summary>
/// Tests for <see cref="BlockLayoutEngine"/> class.
/// </summary>
public class BlockLayoutEngineTests
{
    private static LayoutResult RunLayout(string html, PageLayout? page = null)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        Dictionary<HtmlNode, ComputedStyle> styles = new StyleResolver().Resolve(doc.DocumentNode);
        BlockLayoutEngine engine = new(page ?? PageLayout.A4, styles);
        return engine.Layout(doc.DocumentNode);
    }

    [Fact]
    public void SimpleDiv_ProducesTextPrimitives()
    {
        LayoutResult result = RunLayout("<div>Hello World</div>");

        Assert.Contains(result.Primitives, p => p is TextPrimitive);
    }

    [Fact]
    public void DisplayNone_ProducesNoPrimitives()
    {
        LayoutResult result = RunLayout("<div style=\"display:none\">hidden</div>");

        Assert.DoesNotContain(result.Primitives, p => p is TextPrimitive);
    }

    [Fact]
    public void BackgroundColor_ProducesRectPrimitive()
    {
        LayoutResult result = RunLayout("<div style=\"background-color:#ff0000\">text</div>");

        Assert.Contains(result.Primitives, p => p is RectPrimitive r && r.Fill.R > 0.9f);
    }

    [Fact]
    public void BackgroundIsDrawnBefore_Text_ForZOrder()
    {
        // For correct Z-order in PDF, backgrounds must be emitted BEFORE text.
        LayoutResult result = RunLayout("<div style=\"background-color:#0000ff\">Hello World</div>");

        int rectIdx = result.Primitives.FindIndex(p => p is RectPrimitive);
        int textIdx = result.Primitives.FindIndex(p => p is TextPrimitive);

        Assert.True(rectIdx >= 0, "RectPrimitive not found");
        Assert.True(textIdx >= 0, "TextPrimitive not found");
        Assert.True(rectIdx < textIdx, $"Background (idx {rectIdx}) must be before Text (idx {textIdx})");
    }

    [Fact]
    public void Border_ProducesBorderLinePrimitives()
    {
        LayoutResult result = RunLayout("<div style=\"border:1pt solid black\">box</div>");

        List<BorderLinePrimitive> borders = result.Primitives.OfType<BorderLinePrimitive>().ToList();
        Assert.True(borders.Count >= 4, $"Expected ≥4 border lines, got {borders.Count}");
    }

    [Fact]
    public void BackgroundIsDrawnBefore_Image_ForZOrder()
    {
        // For correct Z-order in PDF, backgrounds must be emitted BEFORE image.
        // We use a base64 dummy image to avoid file system issues in tests.
        string html = "<img src=\"data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==\" style=\"background-color:#00ff00\" />";
        LayoutResult result = RunLayout(html);

        int rectIdx = result.Primitives.FindIndex(p => p is RectPrimitive);
        int imgIdx = result.Primitives.FindIndex(p => p is ImagePrimitive);

        Assert.True(rectIdx >= 0, "RectPrimitive not found");
        Assert.True(imgIdx >= 0, "ImagePrimitive not found");
        Assert.True(rectIdx < imgIdx, $"Background (idx {rectIdx}) must be before Image (idx {imgIdx})");
    }


    [Fact]
    public void BlockBackground_SynchronizesWithPageBreak()
    {
        // This test verifies that if a block starts near the end of a page and 
        // jumps to the next page, its background (RectPrimitive) follows it.
        string html = @"<div style='height:1200px'>Space</div>
                        <div style='background-color:#ff0000; padding:10px'>
                            <p>This should be on page 2 with red background</p>
                        </div>";
        LayoutResult result = RunLayout(html);

        // Find the red background
        RectPrimitive? bg = result.Primitives.OfType<RectPrimitive>().FirstOrDefault(r => r.Fill.R > 0.9f);
        TextPrimitive? text = result.Primitives.OfType<TextPrimitive>().FirstOrDefault(t => t.PageIndex == 1);

        Assert.NotNull(bg);
        Assert.NotNull(text);

        // They should both be on the same page (Page 2, index 1)
        Assert.Equal(1, bg.PageIndex);
        Assert.Equal(1, text.PageIndex);

        // The background Y should be less than or equal to text Y
        Assert.True(bg.Y <= text.Y, $"Background Y ({bg.Y}) should be <= Text Y ({text.Y})");
    }



    [Fact]
    public void RoundedRectBox_SplitsAcrossPages()
    {
        // Use a small page to force a page break mid-content
        var smallPage = new PageLayout(PageLayout.A4.Width, 120f, new PageMargins(10f));
        string html = @"
            <div style='height:80px'>Space filler</div>
            <div style='background-color:#ffffff; border:2px solid #94a3b8; border-radius:10px; padding:10px'>
                <p>Line 1</p>
                <p>Line 2</p>
                <p>Line 3</p>
                <p>Line 4</p>
                <p>Line 5</p>
            </div>";

        LayoutResult result = RunLayout(html, smallPage);

        // Find all RectPrimitives with border-radius (the container's split parts)
        var rects = result.Primitives.OfType<RectPrimitive>()
            .Where(r => r.BorderRadius > 0f)
            .OrderBy(r => r.PageIndex).ThenBy(r => r.Y).ToList();

        // Should have 3 parts: Part 1, Part 2 fill, Part 2 stroke
        Assert.True(rects.Count >= 3,
            $"Expected at least 3 rounded-rect parts, got {rects.Count}");

        var part1 = rects[0];

        // Part 1 must be on the first page
        Assert.Equal(0, part1.PageIndex);

        // Part 1's top must be after the spacer (80px = 60pt)
        Assert.True(part1.Y >= 50f,
            $"Part 1 Y ({part1.Y}) should be ≥ 50 (after spacer)");

        // Part 1 must have both stroke and fill
        Assert.True(part1.StrokeWidth > 0f, "Part 1 should have stroke width > 0");
        Assert.True(part1.Fill.A > 0f, "Part 1 should have fill");

        // Part 2 on page 1 is split into fill rect + stroke rect
        var page1Rounded = rects.Where(r => r.PageIndex == 1).ToList();
        var part2Fill = page1Rounded.FirstOrDefault(r => r.Fill.A > 0f);
        var part2Stroke = page1Rounded.FirstOrDefault(r => r.StrokeWidth > 0f);

        Assert.NotNull(part2Fill);
        Assert.NotNull(part2Stroke);
        Assert.Equal(0f, part2Stroke.Y);
        Assert.True(part2Fill.Fill.A > 0f, "Part 2 fill should have fill");
        Assert.True(part2Stroke.StrokeWidth > 0f, "Part 2 stroke should have stroke");
    }

    [Fact]
    public void RoundedRectContainer_WithNestedList_SplitsCorrectly()
    {
        // Replicate the real scenario with enough content before to force a page break
        // mid-container on A4 page with default margins.
        string html = @"
            <div style='height:680px'>Filler</div>
            <div style='background-color:#ffffff; border:2px solid #94a3b8; border-radius:10px'>
                <ol style='margin:0; padding:16px 20px 16px 44px'>
                    <li style='margin-bottom:8px; font-size:12px; line-height:1.5'>
                        <span style='font-weight:700'>Laptop Pro 15</span>
                        - <span style='color:#16a34a'>$179,880.00</span>
                        (120 units)
                        <ul style='margin-top:6px; margin-left:24px'>
                            <li style='font-size:11px; color:#64748b'>Category margin: $524.65 per unit</li>
                            <li style='font-size:11px; color:#64748b'>Quarter-over-quarter growth: +15%</li>
                        </ul>
                    </li>
                    <li style='margin-bottom:8px; font-size:12px; line-height:1.5'>
                        <span style='font-weight:700'>Wireless Headphones</span>
                        - <span style='color:#16a34a'>$49,997.50</span>
                        (250 units)
                        <ul style='margin-top:6px; margin-left:24px'>
                            <li style='font-size:11px; color:#64748b'>Category margin: $69.00 per unit</li>
                            <li style='font-size:11px; color:#64748b'>Quarter-over-quarter growth: +22%</li>
                        </ul>
                    </li>
                    <li style='margin-bottom:8px; font-size:12px; line-height:1.5'>
                        <span style='font-weight:700'>Ergonomic Office Chair</span>
                        - <span style='color:#16a34a'>$29,707.50</span>
                        (85 units)
                        <ul style='margin-top:6px; margin-left:24px'>
                            <li style='font-size:11px; color:#64748b'>Category margin: $122.32 per unit</li>
                            <li style='font-size:11px; color:#64748b'>Quarter-over-quarter growth: +8%</li>
                        </ul>
                    </li>
                    <li style='margin-bottom:8px; font-size:12px; line-height:1.5'>
                        <span style='font-weight:700'>4K UltraWide Monitor</span>
                        - <span style='color:#16a34a'>$41,940.00</span>
                        (60 units)
                        <ul style='margin-top:6px; margin-left:24px'>
                            <li style='font-size:11px; color:#64748b'>Category margin: $244.65 per unit</li>
                            <li style='font-size:11px; color:#64748b'>Quarter-over-quarter growth: +18%</li>
                        </ul>
                    </li>
                    <li style='margin-bottom:8px; font-size:12px; line-height:1.5'>
                        <span style='font-weight:700'>Mechanical Keyboard</span>
                        - <span style='color:#16a34a'>$52,890.00</span>
                        (410 units)
                        <ul style='margin-top:6px; margin-left:24px'>
                            <li style='font-size:11px; color:#64748b'>Category margin: $45.15 per unit</li>
                            <li style='font-size:11px; color:#64748b'>Quarter-over-quarter growth: +25%</li>
                        </ul>
                    </li>
                </ol>
            </div>";

        LayoutResult result = RunLayout(html);

        var roundedRects = result.Primitives.OfType<RectPrimitive>()
            .Where(r => r.BorderRadius > 0f)
            .OrderBy(r => r.PageIndex).ThenBy(r => r.Y).ToList();

        Assert.True(result.PageCount >= 2,
            $"Content should span at least 2 pages, got {result.PageCount}");

        Assert.True(roundedRects.Count >= 3,
            $"Expected at least 3 rounded-rect parts, got {roundedRects.Count}. " +
            $"PageCount={result.PageCount}");

        var part1 = roundedRects[0];

        Assert.Equal(0, part1.PageIndex);
        Assert.True(part1.Y > 0f, "Part 1 Y should be > 0");
        Assert.True(part1.StrokeWidth > 0f);
        Assert.True(part1.Fill.A > 0f);

        // Part 2 on page 1 is split into fill rect + stroke rect
        var page1Rounded = roundedRects.Where(r => r.PageIndex == 1).ToList();
        var part2Fill = page1Rounded.FirstOrDefault(r => r.Fill.A > 0f);
        var part2Stroke = page1Rounded.FirstOrDefault(r => r.StrokeWidth > 0f);

        Assert.NotNull(part2Fill);
        Assert.NotNull(part2Stroke);
        Assert.Equal(0f, part2Stroke.Y);
        Assert.True(part2Fill.Fill.A > 0f);
        Assert.True(part2Stroke.StrokeWidth > 0f);
    }

    [Fact]
    public void RoundedRectContainer_ExactRealTemplate_SplitsAcrossPages()
    {
        // Use the EXACT HTML from the real SalesReport template — the full structure
        // that the user says shows "full border on page 0, only top border on page 1".
        string html = @"
            <div style='background-color:#1e40af; color:#ffffff; padding:20px 24px; margin-bottom:20px'>
                <div style='font-size:10px; text-transform:uppercase'>ACME CORPORATION</div>
                <h1 style='font-size:22px'>Quarterly Sales Report</h1>
                <div style='font-size:13px'>Q1 2026 - Executive Summary</div>
                <div style='font-size:10px; border-top:1px solid #ffffff40; padding-top:10px; margin-top:10px'>Generated: July 08, 2026</div>
            </div>
            <div style='display:flex; flex-direction:row; gap:10px; margin-bottom:10px'>
                <div style='background-color:#ffffff; border:1px solid #e2e8f0; border-radius:10px; padding:14px 18px; flex:1'>
                    <div style='font-size:10px; text-transform:uppercase; text-decoration:underline; color:#64748b; margin-bottom:6px'>Total Revenue</div>
                    <div style='font-size:24px; font-weight:700; color:#16a34a'>$59,999.99</div>
                </div>
                <div style='background-color:#ffffff; border:1px solid #e2e8f0; border-radius:10px; padding:14px 18px; flex:1'>
                    <div style='font-size:10px; text-transform:uppercase; text-decoration:underline; color:#64748b; margin-bottom:6px'>Units Sold</div>
                    <div style='font-size:24px; font-weight:700; color:#475569'>5,000</div>
                </div>
                <div style='background-color:#ffffff; border:1px solid #e2e8f0; border-radius:10px; padding:14px 18px; flex:1'>
                    <div style='font-size:10px; text-transform:uppercase; text-decoration:underline; color:#64748b; margin-bottom:6px'>Product Lines</div>
                    <div style='font-size:24px; font-weight:700; color:#1e40af'>50</div>
                </div>
            </div>
            <div style='font-size:14px; font-weight:700; margin-bottom:10px; padding-bottom:8px; border-bottom:2px solid #e2e8f0'>Layout Demo — Flexbox Enhancements</div>
            <div style='background-color:#ffffff; border:1px solid #e2e8f0; border-radius:10px; padding:18px; margin-bottom:16px'>
                <div style='font-size:10px; text-transform:uppercase; color:#64748b; margin-bottom:8px'>1. flex-wrap: wrap</div>
                <div style='display:flex; flex-wrap:wrap; gap:6px; margin-bottom:16px'>
                    <span style='display:inline-block; padding:4px 12px; font-size:10px; font-weight:600; border-radius:20px; background-color:#e0e7ff; color:#3730a3'>Electronics</span>
                    <span style='display:inline-block; padding:4px 12px; font-size:10px; font-weight:600; border-radius:20px; background-color:#fef3c7; color:#92400e'>Accessories</span>
                    <span style='display:inline-block; padding:4px 12px; font-size:10px; font-weight:600; border-radius:20px; background-color:#e0e7ff; color:#3730a3'>Audio</span>
                    <span style='display:inline-block; padding:4px 12px; font-size:10px; font-weight:600; border-radius:20px; background-color:#e0e7ff; color:#3730a3'>Furniture</span>
                    <span style='display:inline-block; padding:4px 12px; font-size:10px; font-weight:600; border-radius:20px; background-color:#fef3c7; color:#92400e'>Stationery</span>
                    <span style='display:inline-block; padding:4px 12px; font-size:10px; font-weight:600; border-radius:20px; background-color:#e0e7ff; color:#3730a3'>Office</span>
                </div>
            </div>
            <div style='background-color:#ffffff; border:1px solid #e2e8f0; border-radius:10px; padding:18px; margin-bottom:16px'>
                <div style='font-size:10px; text-transform:uppercase; color:#64748b; margin-bottom:8px'>2. flex-grow</div>
                <div style='display:flex; flex-direction:row; gap:4px'>
                    <div style='height:32px; flex:1; background-color:#3b82f6; border-radius:6px; font-size:10px; font-weight:700; color:white; text-align:center; line-height:32px'>flex: 1</div>
                    <div style='height:32px; flex:2; background-color:#8b5cf6; border-radius:6px; font-size:10px; font-weight:700; color:white; text-align:center; line-height:32px'>flex: 2</div>
                    <div style='height:32px; flex:1; background-color:#10b981; border-radius:6px; font-size:10px; font-weight:700; color:white; text-align:center; line-height:32px'>flex: 1</div>
                </div>
            </div>
            <div style='background-color:#ffffff; border:1px solid #e2e8f0; border-radius:10px; padding:18px; margin-bottom:16px'>
                <div style='font-size:10px; text-transform:uppercase; color:#64748b; margin-bottom:8px'>3. flex-shrink</div>
                <div style='display:flex; flex-direction:row; gap:6px; width:100%'>
                    <div style='padding:8px 10px; border-radius:6px; font-size:10px; font-weight:600; text-align:center; color:white; flex:1 1 160px; background-color:#f59e0b'>shrink: 1</div>
                    <div style='padding:8px 10px; border-radius:6px; font-size:10px; font-weight:600; text-align:center; color:white; flex:1 1 160px; background-color:#ef4444'>shrink: 2</div>
                    <div style='padding:8px 10px; border-radius:6px; font-size:10px; font-weight:600; text-align:center; color:white; flex:1 1 160px; background-color:#06b6d4'>shrink: 1</div>
                </div>
            </div>
            <div style='font-size:14px; font-weight:700; margin-bottom:10px; padding-bottom:8px; border-bottom:2px solid #e2e8f0'>Key Highlights</div>
            <ul style='background-color:#ffffff; border:1px solid #e2e8f0; border-radius:10px; padding:16px 20px 16px 36px; margin-bottom:16px; page-break-inside:avoid'>
                <li style='margin-bottom:6px; font-size:12px; line-height:1.5'>Total revenue reached <strong>$59,999.99</strong></li>
                <li style='margin-bottom:6px; font-size:12px; line-height:1.5'>Over <strong>5,000</strong> units sold</li>
                <li style='margin-bottom:6px; font-size:12px; line-height:1.5'>New product categories expanded</li>
                <li style='margin-bottom:0; font-size:12px; line-height:1.5'>Operational efficiency improved</li>
            </ul>
            <div style='font-size:14px; font-weight:700; margin-bottom:10px; padding-bottom:8px; border-bottom:2px solid #e2e8f0'>Top 5 Best Sellers</div>
            <div style='background-color:#ffffff; border:2px solid #94a3b8; border-radius:10px; margin-bottom:16px'>
                <ol style='padding:16px 20px 16px 44px; margin:0'>
                    <li style='margin-bottom:8px; font-size:12px; line-height:1.5'>
                        <span style='font-weight:700'>Laptop Pro 15</span>
                        - <span style='color:#16a34a'>$179,880.00</span>
                        (120 units)
                        <ul style='margin-top:6px; margin-left:24px'>
                            <li style='font-size:11px; color:#64748b'>Category margin: $524.65 per unit</li>
                            <li style='font-size:11px; color:#64748b'>Quarter-over-quarter growth: +15%</li>
                        </ul>
                    </li>
                    <li style='margin-bottom:8px; font-size:12px; line-height:1.5'>
                        <span style='font-weight:700'>Wireless Headphones</span>
                        - <span style='color:#16a34a'>$49,997.50</span>
                        (250 units)
                        <ul style='margin-top:6px; margin-left:24px'>
                            <li style='font-size:11px; color:#64748b'>Category margin: $69.00 per unit</li>
                            <li style='font-size:11px; color:#64748b'>Quarter-over-quarter growth: +22%</li>
                        </ul>
                    </li>
                    <li style='margin-bottom:8px; font-size:12px; line-height:1.5'>
                        <span style='font-weight:700'>Ergonomic Office Chair</span>
                        - <span style='color:#16a34a'>$29,707.50</span>
                        (85 units)
                        <ul style='margin-top:6px; margin-left:24px'>
                            <li style='font-size:11px; color:#64748b'>Category margin: $122.32 per unit</li>
                            <li style='font-size:11px; color:#64748b'>Quarter-over-quarter growth: +8%</li>
                        </ul>
                    </li>
                    <li style='margin-bottom:8px; font-size:12px; line-height:1.5'>
                        <span style='font-weight:700'>4K UltraWide Monitor</span>
                        - <span style='color:#16a34a'>$41,940.00</span>
                        (60 units)
                        <ul style='margin-top:6px; margin-left:24px'>
                            <li style='font-size:11px; color:#64748b'>Category margin: $244.65 per unit</li>
                            <li style='font-size:11px; color:#64748b'>Quarter-over-quarter growth: +18%</li>
                        </ul>
                    </li>
                    <li style='margin-bottom:8px; font-size:12px; line-height:1.5'>
                        <span style='font-weight:700'>Mechanical Keyboard</span>
                        - <span style='color:#16a34a'>$52,890.00</span>
                        (410 units)
                        <ul style='margin-top:6px; margin-left:24px'>
                            <li style='font-size:11px; color:#64748b'>Category margin: $45.15 per unit</li>
                            <li style='font-size:11px; color:#64748b'>Quarter-over-quarter growth: +25%</li>
                        </ul>
                    </li>
                </ol>
            </div>";

        LayoutResult result = RunLayout(html);

        var roundedRects = result.Primitives.OfType<RectPrimitive>()
            .Where(r => r.BorderRadius > 0f && r.Fill.A > 0f && r.StrokeWidth > 0f)
            .OrderBy(r => r.PageIndex).ThenBy(r => r.Y).ToList();

        Assert.True(result.PageCount >= 2,
            $"Content should span at least 2 pages, got {result.PageCount}");

        // Debug: dump ALL rounded rect details and page break positions
        var pageBreakPositions = result.Primitives
            .Select((p, i) => (Idx: i, Prim: p))
            .Where(x => x.Prim is PageBreakPrimitive)
            .Select(x => x.Idx)
            .ToList();

        var rectDebug = string.Join("; ",
            roundedRects.Select(r => $"P{r.PageIndex}[Y={r.Y:F1},H={r.Height:F1},SW={r.StrokeWidth:F2}]"));
        var breakDebug = $"pageBreaks=[{string.Join(",", pageBreakPositions)}]";

        // The container has border:2px (SW≈1.5). Find rects with the larger stroke.
        var containerRects = result.Primitives.OfType<RectPrimitive>()
            .Where(r => r.BorderRadius > 0f && r.Fill.A > 0f && r.StrokeWidth > 1f)
            .OrderBy(r => r.PageIndex).ThenBy(r => r.Y).ToList();

        Assert.True(containerRects.Count >= 1,
            $"Expected at least 1 full container rect, got {containerRects.Count}");

        var part1 = containerRects[0];

        Assert.Equal(0, part1.PageIndex);
        Assert.True(part1.Y > 0f, "Part 1 Y should be > 0");
        Assert.True(part1.Height > 0f, "Part 1 height should be > 0");

        Assert.True(part1.StrokeWidth > 0f, "Part 1 must have stroke");
        Assert.True(part1.Fill.A > 0f, "Part 1 must have fill");

        // Part 2 on page 1 is split into fill rect + stroke rect
        var allRounded = result.Primitives.OfType<RectPrimitive>()
            .Where(r => r.BorderRadius > 0f)
            .ToList();

        var part2Stroke = allRounded.FirstOrDefault(r => r.PageIndex == 1 && r.StrokeWidth > 1f);
        var part2Fill = allRounded.FirstOrDefault(r => r.PageIndex == 1 && r.Fill.A > 0f);

        Assert.NotNull(part2Stroke);
        Assert.NotNull(part2Fill);
        Assert.Equal(0f, part2Stroke.Y);
        Assert.True(part2Stroke.Height > 0f, "Part 2 stroke height should be > 0");
        Assert.True(part2Stroke.StrokeWidth > 0f, "Part 2 must have stroke");
        Assert.True(part2Fill.Fill.A > 0f, "Part 2 must have fill");
    }

    [Fact]
    public void TextPrimitive_IsOnCorrectPage()
    {
        LayoutResult result = RunLayout("<div>Only page</div>");

        List<TextPrimitive> texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.All(texts, t => Assert.Equal(0, t.PageIndex));
    }

    [Fact]
    public void NestedBlocks_LayoutCorrectly()
    {
        LayoutResult result = RunLayout(
            "<div style=\"padding:10pt\">" +
            "  <p>First paragraph</p>" +
            "  <p>Second paragraph</p>" +
            "</div>");

        List<TextPrimitive> texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.True(texts.Count >= 2, $"Expected ≥2 text primitives, got {texts.Count}");

        // El segundo párrafo debe estar más abajo que el primero
        Assert.True(texts[^1].Y > texts[0].Y,
            $"Second text Y={texts[^1].Y} should be > first text Y={texts[0].Y}");
    }

    [Fact]
    public void H1_IsLargerThanBodyText()
    {
        LayoutResult result = RunLayout("<h1>Title</h1><p>Body</p>");

        List<TextPrimitive> texts = result.Primitives.OfType<TextPrimitive>().ToList();
        TextPrimitive? h1 = texts.FirstOrDefault(t => t.FontSize >= 20f);
        TextPrimitive? body = texts.FirstOrDefault(t => t.FontSize <= Constants.DefaultFontSize);

        Assert.NotNull(h1);
        Assert.NotNull(body);
        Assert.True(h1.FontSize > body.FontSize);
    }

    [Fact]
    public void ForPage_FiltersByPageIndex()
    {
        LayoutResult result = RunLayout(
            "<div>Page 1</div>" +
            "<div style=\"page-break-before:always\">Page 2</div>");

        List<RenderPrimitive> page0 = result.ForPage(0).ToList();
        List<RenderPrimitive> page1 = result.ForPage(1).ToList();

        // Each page must have its own primitives
        Assert.NotEmpty(page0);
        Assert.NotEmpty(page1);
    }

    // ── List markers ────────────────────────────────────────────────────────

    [Fact]
    public void UnorderedList_EmitsBulletMarker()
    {
        LayoutResult result = RunLayout("<ul><li>Item</li></ul>");

        // Should contain a bullet character primitive for the <li> marker
        Assert.Contains(result.Primitives, p => p is TextPrimitive t && t.Text.Contains("\u00B7"));
    }
}