using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;
using Xunit;

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
        Assert.Equal(0f, StandardFontMetrics.MeasureWidth("", "Helvetica", 12f));
    }

    [Fact]
    public void MeasureWidth_CourierIsMonospace()
    {
        // Todos los caracteres Courier tienen 600 unidades AFM → ancho = 600/1000 * fontSize
        float w1 = StandardFontMetrics.MeasureWidth("A", "Courier", 12f);
        float w2 = StandardFontMetrics.MeasureWidth("i", "Courier", 12f);
        Assert.Equal(w1, w2, precision: 2);
        Assert.Equal(7.2f, w1, precision: 2); // 600/1000 * 12
    }

    [Fact]
    public void MeasureWidth_ScalesWithFontSize()
    {
        float w12 = StandardFontMetrics.MeasureWidth("Hello", "Helvetica", 12f);
        float w24 = StandardFontMetrics.MeasureWidth("Hello", "Helvetica", 24f);
        Assert.Equal(w12 * 2f, w24, precision: 2);
    }

    [Fact]
    public void MeasureWidth_LongerTextIsWider()
    {
        float wShort = StandardFontMetrics.MeasureWidth("Hi", "Helvetica", 12f);
        float wLong = StandardFontMetrics.MeasureWidth("Hello", "Helvetica", 12f);
        Assert.True(wLong > wShort);
    }
}

/// <summary>
/// Tests for <see cref="InlineLayoutEngine"/> class.
/// </summary>
public class InlineLayoutEngineTests
{
    private static InlineRun MakeRun(string text, float fontSize = 12f) => new()
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
        var runs = new[] { MakeRun("Hi") };
        var lines = InlineLayoutEngine.Layout(runs, availableWidth: 200f);

        Assert.Single(lines);
    }

    [Fact]
    public void Layout_LongText_WrapsToMultipleLines()
    {
        // Courier 12pt → cada char = 7.2pt. 10 chars = 72pt > 50pt → wraps
        var runs = new[] { MakeRun("AAAA BBBB CCCC DDDD") };
        var lines = InlineLayoutEngine.Layout(runs, availableWidth: 50f);

        Assert.True(lines.Count > 1);
    }

    [Fact]
    public void Layout_EmptyText_ReturnsNoLines()
    {
        var runs = new[] { MakeRun("") };
        var lines = InlineLayoutEngine.Layout(runs, availableWidth: 200f);

        Assert.Empty(lines);
    }

    [Fact]
    public void Layout_AlignRight_ShiftsItemsRight()
    {
        var runs = new[] { MakeRun("Hi") };
        var lines = InlineLayoutEngine.Layout(runs, availableWidth: 200f, TextAlign.Right);

        // The first item should be shifted to the right
        var (_, x) = lines[0].Items[0];
        Assert.True(x > 0f, $"Expected x > 0, got {x}");
    }

    [Fact]
    public void Layout_AlignCenter_ItemIsRoughlyInMiddle()
    {
        // "Hi" en Courier 12pt → 14.4pt. Centro en 200pt → offset ≈ 92.8pt
        var runs = new[] { MakeRun("Hi") };
        var lines = InlineLayoutEngine.Layout(runs, availableWidth: 200f, TextAlign.Center);

        var (run, x) = lines[0].Items[0];
        float expectedOffset = (200f - run.Width) / 2f;
        Assert.Equal(expectedOffset, x, precision: 1);
    }

    [Fact]
    public void Layout_MultipleRuns_MergeOnSameLine()
    {
        // Two short runs should fit on the same line
        var runs = new[]
        {
            MakeRun("Hello "),
            MakeRun("World"),
        };
        var lines = InlineLayoutEngine.Layout(runs, availableWidth: 200f);

        Assert.Single(lines);
        Assert.Equal(2, lines[0].Items.Count);
    }

    [Fact]
    public void Layout_LineHeight_IsPositive()
    {
        var runs = new[] { MakeRun("Test line") };
        var lines = InlineLayoutEngine.Layout(runs, availableWidth: 500f);

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
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var styles = new StyleResolver().Resolve(doc.DocumentNode);
        var engine = new BlockLayoutEngine(page ?? PageLayout.A4, styles);
        return engine.Layout(doc.DocumentNode);
    }

    [Fact]
    public void SimpleDiv_ProducesTextPrimitives()
    {
        var result = RunLayout("<div>Hello World</div>");

        Assert.Contains(result.Primitives, p => p is TextPrimitive);
    }

    [Fact]
    public void DisplayNone_ProducesNoPrimitives()
    {
        var result = RunLayout("<div style=\"display:none\">hidden</div>");

        Assert.DoesNotContain(result.Primitives, p => p is TextPrimitive);
    }

    [Fact]
    public void BackgroundColor_ProducesRectPrimitive()
    {
        var result = RunLayout("<div style=\"background-color:#ff0000\">text</div>");

        Assert.Contains(result.Primitives, p => p is RectPrimitive r && r.Fill.R > 0.9f);
    }

    [Fact]
    public void Border_ProducesBorderLinePrimitives()
    {
        var result = RunLayout("<div style=\"border:1pt solid black\">box</div>");

        var borders = result.Primitives.OfType<BorderLinePrimitive>().ToList();
        Assert.True(borders.Count >= 4, $"Expected ≥4 border lines, got {borders.Count}");
    }

    [Fact]
    public void PageBreakBefore_CreatesNewPage()
    {
        var result = RunLayout(
            "<div>Page 1</div>" +
            "<div style=\"page-break-before:always\">Page 2</div>");

        Assert.True(result.PageCount >= 2);
    }

    [Fact]
    public void LongContent_SpillsToMultiplePages()
    {
        // Generate enough content to force pagination
        var paragraphs = string.Concat(Enumerable.Repeat("<p>Lorem ipsum dolor sit amet, consectetur adipiscing elit.</p>", 60));
        var result = RunLayout(paragraphs);

        Assert.True(result.PageCount > 1, $"Expected >1 page, got {result.PageCount}");
    }

    [Fact]
    public void TextPrimitive_IsOnCorrectPage()
    {
        var result = RunLayout("<div>Only page</div>");

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.All(texts, t => Assert.Equal(0, t.PageIndex));
    }

    [Fact]
    public void NestedBlocks_LayoutCorrectly()
    {
        var result = RunLayout(
            "<div style=\"padding:10pt\">" +
            "  <p>First paragraph</p>" +
            "  <p>Second paragraph</p>" +
            "</div>");

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        Assert.True(texts.Count >= 2, $"Expected ≥2 text primitives, got {texts.Count}");

        // El segundo párrafo debe estar más abajo que el primero
        Assert.True(texts[^1].Y > texts[0].Y,
            $"Second text Y={texts[^1].Y} should be > first text Y={texts[0].Y}");
    }

    [Fact]
    public void H1_IsLargerThanBodyText()
    {
        var result = RunLayout("<h1>Title</h1><p>Body</p>");

        var texts = result.Primitives.OfType<TextPrimitive>().ToList();
        var h1 = texts.FirstOrDefault(t => t.FontSize >= 20f);
        var body = texts.FirstOrDefault(t => t.FontSize <= 12f);

        Assert.NotNull(h1);
        Assert.NotNull(body);
        Assert.True(h1.FontSize > body.FontSize);
    }

    [Fact]
    public void ForPage_FiltersByPageIndex()
    {
        var result = RunLayout(
            "<div>Page 1</div>" +
            "<div style=\"page-break-before:always\">Page 2</div>");

        var page0 = result.ForPage(0).ToList();
        var page1 = result.ForPage(1).ToList();

        // Each page must have its own primitives
        Assert.NotEmpty(page0);
        Assert.NotEmpty(page1);
    }
}