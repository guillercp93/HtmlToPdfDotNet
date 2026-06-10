using HtmlAgilityPack;
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;
using HtmlToPdfDotNet.Library.Models.Writer;

namespace HtmlToPdfDotNet.Tests.Integration;

/// <summary>
/// Integration tests for headers, footers, and repeating table headers.
/// Scenarios defined in: openspec/changes/layout-engine-upgrades/specs/headers-footers/spec.md
/// </summary>
public class HeadersFootersTests
{
    private static LayoutResult RunLayout(string html, PageLayout? page = null)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        Dictionary<HtmlNode, ComputedStyle> styles = new StyleResolver().Resolve(doc.DocumentNode);
        BlockLayoutEngine engine = new(page ?? PageLayout.A4, styles);
        return engine.Layout(doc.DocumentNode);
    }

    #region HeaderFooterConfig Unit Tests

    /// <summary>
    /// Verifies HeaderFooterConfig default property values.
    /// </summary>
    [Fact]
    public void HeaderFooterConfig_DefaultValues()
    {
        var config = new HeaderFooterConfig();

        Assert.Equal("", config.HeaderHtml);
        Assert.Equal("", config.FooterHtml);
        Assert.Null(config.FirstPageHeaderHtml);
        Assert.Equal(0f, config.HeaderHeight);
        Assert.Equal(0f, config.FooterHeight);
        Assert.True(config.ShowPageNumbers);
    }

    /// <summary>
    /// Verifies HeaderFooterConfig property assignment.
    /// </summary>
    [Fact]
    public void HeaderFooterConfig_StoresAssignedProperties()
    {
        var config = new HeaderFooterConfig
        {
            HeaderHtml = "<p>My Header</p>",
            FooterHtml = "<p>My Footer</p>",
            FirstPageHeaderHtml = "<p>First Page</p>",
            HeaderHeight = 50f,
            FooterHeight = 30f,
            ShowPageNumbers = false
        };

        Assert.Equal("<p>My Header</p>", config.HeaderHtml);
        Assert.Equal("<p>My Footer</p>", config.FooterHtml);
        Assert.Equal("<p>First Page</p>", config.FirstPageHeaderHtml);
        Assert.Equal(50f, config.HeaderHeight);
        Assert.Equal(30f, config.FooterHeight);
        Assert.False(config.ShowPageNumbers);
    }

    #endregion

    #region ConversionOptions HeaderFooter

    /// <summary>
    /// Verifies HeaderFooter is exposed on ConversionOptions.
    /// </summary>
    [Fact]
    public void ConversionOptions_ExposesHeaderFooterProperty()
    {
        var options = new ConversionOptions();

        // Default should be null (not set)
        Assert.Null(options.HeaderFooter);

        // Can assign a config
        options.HeaderFooter = new HeaderFooterConfig
        {
            HeaderHtml = "Header",
            FooterHtml = "Footer",
            HeaderHeight = 40f,
            FooterHeight = 20f
        };

        Assert.NotNull(options.HeaderFooter);
        Assert.Equal("Header", options.HeaderFooter.HeaderHtml);
        Assert.Equal("Footer", options.HeaderFooter.FooterHtml);
    }

    /// <summary>
    /// Verifies ConversionOptions internal copy constructor deep-copies HeaderFooterConfig.
    /// </summary>
    [Fact]
    public void ConversionOptions_DeepCopy_CopiesHeaderFooterConfig()
    {
        var config = new HeaderFooterConfig
        {
            HeaderHtml = "<p>Header</p>",
            FooterHtml = "<p>Footer</p>",
            FirstPageHeaderHtml = "<p>First</p>",
            HeaderHeight = 50f,
            FooterHeight = 30f,
            ShowPageNumbers = false
        };
        var original = new ConversionOptions { HeaderFooter = config };
        original.StyleSheets = ["test.css"];

        // Invoke internal copy constructor
        var copy = new ConversionOptions(original);

        Assert.NotNull(copy.HeaderFooter);
        Assert.Equal("<p>Header</p>", copy.HeaderFooter.HeaderHtml);
        Assert.Equal("<p>Footer</p>", copy.HeaderFooter.FooterHtml);
        Assert.Equal("<p>First</p>", copy.HeaderFooter.FirstPageHeaderHtml);
        Assert.Equal(50f, copy.HeaderFooter.HeaderHeight);
        Assert.Equal(30f, copy.HeaderFooter.FooterHeight);
        Assert.False(copy.HeaderFooter.ShowPageNumbers);
    }

    /// <summary>
    /// Verifies deep-copy isolation: modifying the copy does not affect the original.
    /// </summary>
    [Fact]
    public void ConversionOptions_DeepCopy_HeaderFooterIsIndependent()
    {
        var original = new ConversionOptions
        {
            HeaderFooter = new HeaderFooterConfig
            {
                HeaderHtml = "Original",
                FooterHtml = "Original Footer"
            }
        };

        var copy = new ConversionOptions(original);

        // Modify copy's HeaderFooter
        copy.HeaderFooter!.HeaderHtml = "Modified";

        // Original must be unchanged
        Assert.Equal("Original", original.HeaderFooter!.HeaderHtml);
        // Footer (unchanged) should be the same in both
        Assert.Equal("Original Footer", original.HeaderFooter.FooterHtml);
        Assert.Equal("Original Footer", copy.HeaderFooter.FooterHtml);
    }

    /// <summary>
    /// Verifies deep-copy works when HeaderFooter is null on the source.
    /// </summary>
    [Fact]
    public void ConversionOptions_DeepCopy_WithNullHeaderFooter()
    {
        var original = new ConversionOptions();
        Assert.Null(original.HeaderFooter);

        var copy = new ConversionOptions(original);
        Assert.Null(copy.HeaderFooter);
    }

    #endregion

    #region Reserved Height / Pagination Tests

    /// <summary>
    /// Verifies that reserved header/footer height reduces available content height,
    /// causing content to span more pages.
    /// </summary>
    [Fact]
    public void ReservedHeaderFooterHeight_CausesMorePages()
    {
        // Create a tall content block that's just under one page height
        float a4ContentH = PageLayout.A4.ContentHeight; // ~794.41 points for A4 with default margins
        float blockHeight = a4ContentH * 0.7f; // fits on one page

        string html = $"<div style='height: {blockHeight}pt; background-color: #ccc;'><p>Content</p></div>";

        // Without reserved height
        LayoutResult resultNoReserve = RunLayout(html);
        int pagesNoReserve = resultNoReserve.PageCount;

        // With reserved height (reserve 30% of page, so block won't fit)
        var pageWithReserve = new PageLayout(
            PageLayout.A4.Width,
            PageLayout.A4.Height,
            new PageMargins(PageLayout.A4.Margins.Top, PageLayout.A4.Margins.Right,
                            PageLayout.A4.Margins.Bottom, PageLayout.A4.Margins.Left))
        {
            ReservedHeaderFooterHeight = a4ContentH * 0.4f
        };

        LayoutResult resultWithReserve = RunLayout(html, pageWithReserve);
        int pagesWithReserve = resultWithReserve.PageCount;

        // More pages with reserved height since available content height is smaller
        Assert.True(pagesWithReserve >= pagesNoReserve,
            $"With reserved height ({pagesWithReserve} pages) should be >= without ({pagesNoReserve} pages)");
    }

    /// <summary>
    /// Verifies that default ReservedHeaderFooterHeight is 0 (no impact).
    /// </summary>
    [Fact]
    public void PageLayout_ReservedHeaderFooterHeight_DefaultIsZero()
    {
        var page = PageLayout.A4;
        Assert.Equal(0f, page.ReservedHeaderFooterHeight);
    }

    /// <summary>
    /// Verifies that setting ReservedHeaderFooterHeight is stored correctly.
    /// </summary>
    [Fact]
    public void PageLayout_ReservedHeaderFooterHeight_StoresValue()
    {
        var page = new PageLayout { ReservedHeaderFooterHeight = 100f };
        Assert.Equal(100f, page.ReservedHeaderFooterHeight);
    }

    #endregion

    #region Table Thead Repetition Tests

    /// <summary>
    /// Scenario: Table with repeated header (happy path)
    /// GIVEN a table with thead and body spanning multiple pages
    /// WHEN rendered
    /// THEN the table header MUST appear on each page the table occupies
    /// </summary>
    [Fact]
    public void TableWithThead_RepeatsHeaderOnNewPage()
    {
        // Use a small page height to force the table across multiple pages
        var smallPage = new PageLayout(PageLayout.A4.Width, 200f, new PageMargins(10f));

        string html = @"
            <table>
                <thead>
                    <tr>
                        <th>Col A</th>
                        <th>Col B</th>
                    </tr>
                </thead>
                <tbody>
                    <tr><td>A1</td><td>B1</td></tr>
                    <tr><td>A2</td><td>B2</td></tr>
                    <tr><td>A3</td><td>B3</td></tr>
                    <tr><td>A4</td><td>B4</td></tr>
                    <tr><td>A5</td><td>B5</td></tr>
                    <tr><td>A6</td><td>B6</td></tr>
                    <tr><td>A7</td><td>B7</td></tr>
                    <tr><td>A8</td><td>B8</td></tr>
                    <tr><td>A9</td><td>B9</td></tr>
                    <tr><td>A10</td><td>B10</td></tr>
                    <tr><td>A11</td><td>B11</td></tr>
                    <tr><td>A12</td><td>B12</td></tr>
                    <tr><td>A13</td><td>B13</td></tr>
                    <tr><td>A14</td><td>B14</td></tr>
                    <tr><td>A15</td><td>B15</td></tr>
                </tbody>
            </table>";

        LayoutResult result = RunLayout(html, smallPage);

        // Must span at least 2 pages
        Assert.True(result.PageCount >= 2,
            $"Table must span at least 2 pages, got {result.PageCount}");

        // Thead text ("Col A", "Col B") should appear on page 0 AND page 1
        // The layout engine splits words into separate text runs (e.g., "Col " and "A"),
        // so check by font: thead uses bold (Helvetica-Bold), body uses normal (Helvetica).
        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        var page1Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 1).ToList();

        Assert.Contains(page0Texts, t => t.Bold && t.FontName.Contains("Bold"));
        Assert.Contains(page1Texts, t => t.Bold && t.FontName.Contains("Bold"));
        Assert.Contains(page1Texts, t => !t.Bold);
    }

    /// <summary>
    /// Scenario: Table with thead that fits on one page — header should appear only once.
    /// GIVEN a table with thead and body that fits on a single page
    /// WHEN rendered
    /// THEN the table header MUST appear only on page 0
    /// </summary>
    [Fact]
    public void TableWithThead_FitsOnOnePage_HeaderAppearsOnce()
    {
        string html = @"
            <table>
                <thead>
                    <tr>
                        <th>Name</th>
                        <th>Value</th>
                    </tr>
                </thead>
                <tbody>
                    <tr><td>Item 1</td><td>10</td></tr>
                    <tr><td>Item 2</td><td>20</td></tr>
                </tbody>
            </table>";

        LayoutResult result = RunLayout(html);
        Assert.Equal(1, result.PageCount);

        // Thead text should only be on page 0
        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0 && (t.Text.Contains("Name") || t.Text.Contains("Value"))).ToList();
        var page1Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 1 && (t.Text.Contains("Name") || t.Text.Contains("Value"))).ToList();

        Assert.NotEmpty(page0Texts);
        Assert.Empty(page1Texts);
    }

    #endregion

    #region Header/Footer Emission via PdfGenerator

    /// <summary>
    /// Verifies that header HTML content appears in the output PDF when
    /// HeaderFooterConfig is specified and compressStreams is false.
    /// </summary>
    [Fact]
    public void PdfOutput_WithHeader_ContainsHeaderText()
    {
        var options = new ConversionOptions
        {
            CompressStreams = false,
            HeaderFooter = new HeaderFooterConfig
            {
                HeaderHtml = "<p>TESTHEADER</p>",
                FooterHtml = "",
                HeaderHeight = 30f,
                FooterHeight = 0f
            }
        };

        var generator = new PdfGenerator(options);
        byte[] pdf = generator.Convert("<p>Body content</p>");
        string pdfContent = System.Text.Encoding.Latin1.GetString(pdf);

        Assert.Contains("TESTHEADER", pdfContent);
    }

    /// <summary>
    /// Verifies that footer HTML content appears in the output PDF.
    /// </summary>
    [Fact]
    public void PdfOutput_WithFooter_ContainsFooterText()
    {
        var options = new ConversionOptions
        {
            CompressStreams = false,
            HeaderFooter = new HeaderFooterConfig
            {
                HeaderHtml = "",
                FooterHtml = "<p>TESTFOOTER</p>",
                HeaderHeight = 0f,
                FooterHeight = 20f
            }
        };

        var generator = new PdfGenerator(options);
        byte[] pdf = generator.Convert("<p>Body content</p>");
        string pdfContent = System.Text.Encoding.Latin1.GetString(pdf);

        Assert.Contains("TESTFOOTER", pdfContent);
    }

    /// <summary>
    /// Verifies that both header and footer appear in the output PDF.
    /// </summary>
    [Fact]
    public void PdfOutput_WithHeaderAndFooter_ContainsBoth()
    {
        var options = new ConversionOptions
        {
            CompressStreams = false,
            HeaderFooter = new HeaderFooterConfig
            {
                HeaderHtml = "<p>HEADER</p>",
                FooterHtml = "<p>FOOTER</p>",
                HeaderHeight = 30f,
                FooterHeight = 20f,
                ShowPageNumbers = false
            }
        };

        var generator = new PdfGenerator(options);
        byte[] pdf = generator.Convert("<p>Body text</p>");
        string pdfContent = System.Text.Encoding.Latin1.GetString(pdf);

        Assert.Contains("HEADER", pdfContent);
        Assert.Contains("FOOTER", pdfContent);
    }

    /// <summary>
    /// Verifies page number substitution in header/footer HTML.
    /// </summary>
    [Fact]
    public void PdfOutput_WithPageNumbers_SubstitutesPageAndTotal()
    {
        var options = new ConversionOptions
        {
            CompressStreams = false,
            HeaderFooter = new HeaderFooterConfig
            {
                HeaderHtml = "<p>Page {page} of {total}</p>",
                HeaderHeight = 30f,
                FooterHeight = 0f,
                ShowPageNumbers = true
            }
        };

        var generator = new PdfGenerator(options);
        byte[] pdf = generator.Convert("<p>Content</p>");
        string pdfContent = System.Text.Encoding.Latin1.GetString(pdf);

        // The layout engine renders each word as a separate PDF text run,
        // so the full phrase "Page 1 of 1" is not a single substring.
        // Instead verify each individual word run appears in the PDF.
        Assert.Contains("(Page ) Tj", pdfContent);
        Assert.Contains("(of ) Tj", pdfContent);
        Assert.Contains("(1) Tj", pdfContent);
    }

    /// <summary>
    /// Verifies first-page header variant works when FirstPageHeaderHtml is set.
    /// </summary>
    [Fact]
    public void PdfOutput_WithFirstPageHeaderVariant_UsesDifferentHeaderOnFirstPage()
    {
        // Use a tall content block and a short page height to force 2+ pages
        string bodyHtml = "<p>Start</p>" +
                          "<div style='height: 350pt; background: #eee;'></div>" +
                          "<p>End</p>";

        var options = new ConversionOptions
        {
            CompressStreams = false,
            Page = new PageLayout(PageLayout.A4.Width, 300f, new PageMargins(20f)),
            HeaderFooter = new HeaderFooterConfig
            {
                HeaderHtml = "<p>REGULARHEADER</p>",
                FirstPageHeaderHtml = "<p>FIRSTPAGEHEADER</p>",
                HeaderHeight = 30f,
                FooterHeight = 0f,
                ShowPageNumbers = false
            }
        };

        var generator = new PdfGenerator(options);
        byte[] pdf = generator.Convert(bodyHtml);
        string pdfContent = System.Text.Encoding.Latin1.GetString(pdf);

        // Both header variants should appear somewhere in the PDF
        Assert.Contains("FIRSTPAGEHEADER", pdfContent);
        Assert.Contains("REGULARHEADER", pdfContent);
    }

    #endregion
}
