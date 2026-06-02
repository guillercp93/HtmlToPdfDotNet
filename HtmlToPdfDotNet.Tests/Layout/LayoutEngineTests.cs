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
}