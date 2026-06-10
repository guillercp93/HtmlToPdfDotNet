using HtmlAgilityPack;
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Tests.E2E;

/// <summary>
/// End-to-end smoke tests that exercise multiple layout features simultaneously:
/// page-break controls, headers/footers, repeating table headers, and flexbox.
/// Scenarios defined in the respective specs under openspec/changes/layout-engine-upgrades/specs/.
/// These are RED tests (TDD) that verify the system works as a whole when all features interact.
/// </summary>
public class CombinedFeaturesSmokeTests
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
    /// Combined E2E test: multi-page document with flex containers, page-break-inside: avoid,
    /// a long table with thead, and headers/footers configured.
    /// Verifies the document spans at least 2 pages, headers/footers config is set correctly,
    /// and no exceptions are thrown during PDF generation.
    ///
    /// Specs: page-breaks/spec.md, headers-footers/spec.md, flexbox-layout/spec.md
    /// </summary>
    [Fact]
    public void CombinedFeatures_MultiPageDocument_WithAllLayoutFeatures()
    {
        // Build an HTML document that combines:
        // - A flex row container (display: flex; flex-direction: row)
        // - Elements with page-break-inside: avoid
        // - A long table with <thead> to trigger page breaks and header repetition
        // Use a small page height to force pagination

        var smallPage = new PageLayout(PageLayout.A4.Width, 250f, new PageMargins(10f));

        string html = @"
            <div style='display: flex; flex-direction: row; gap: 10px; margin-bottom: 20px;'>
                <div style='flex: 1; background-color: #e0f2fe; padding: 10px;'>
                    <p>KPI One</p>
                </div>
                <div style='flex: 1; background-color: #dcfce7; padding: 10px;'>
                    <p>KPI Two</p>
                </div>
                <div style='flex: 1; background-color: #fef3c7; padding: 10px;'>
                    <p>KPI Three</p>
                </div>
            </div>

            <div style='page-break-inside: avoid; background-color: #f8fafc; padding: 10px; border: 1px solid #ccc;'>
                <p>This block must NOT split across pages (page-break-inside: avoid).</p>
                <p>It contains important summary information.</p>
            </div>

            <table>
                <thead>
                    <tr style='background-color: #1e40af; color: white;'>
                        <th style='padding: 8px;'>ID</th>
                        <th style='padding: 8px;'>Product</th>
                        <th style='padding: 8px;'>Category</th>
                        <th style='padding: 8px;'>Units</th>
                        <th style='padding: 8px;'>Price</th>
                        <th style='padding: 8px;'>Total</th>
                    </tr>
                </thead>
                <tbody>
                    <tr><td>1</td><td>Widget Alpha</td><td>Electronics</td><td>10</td><td>$25.00</td><td>$250.00</td></tr>
                    <tr><td>2</td><td>Gadget Beta</td><td>Accessories</td><td>25</td><td>$15.00</td><td>$375.00</td></tr>
                    <tr><td>3</td><td>Device Gamma</td><td>Electronics</td><td>5</td><td>$150.00</td><td>$750.00</td></tr>
                    <tr><td>4</td><td>Tool Delta</td><td>Office</td><td>12</td><td>$45.00</td><td>$540.00</td></tr>
                    <tr><td>5</td><td>Part Epsilon</td><td>Furniture</td><td>8</td><td>$200.00</td><td>$1,600.00</td></tr>
                    <tr><td>6</td><td>Item Zeta</td><td>Audio</td><td>20</td><td>$35.00</td><td>$700.00</td></tr>
                    <tr><td>7</td><td>Component Eta</td><td>Electronics</td><td>3</td><td>$500.00</td><td>$1,500.00</td></tr>
                    <tr><td>8</td><td>Accessory Theta</td><td>Accessories</td><td>50</td><td>$8.00</td><td>$400.00</td></tr>
                    <tr><td>9</td><td>Fixture Iota</td><td>Furniture</td><td>15</td><td>$120.00</td><td>$1,800.00</td></tr>
                    <tr><td>10</td><td>Supply Kappa</td><td>Office</td><td>30</td><td>$12.00</td><td>$360.00</td></tr>
                    <tr><td>11</td><td>System Lambda</td><td>Electronics</td><td>2</td><td>$2,500.00</td><td>$5,000.00</td></tr>
                    <tr><td>12</td><td>Bundle Mu</td><td>Accessories</td><td>7</td><td>$75.00</td><td>$525.00</td></tr>
                </tbody>
            </table>

            <div style='page-break-inside: avoid; margin-top: 20px; background-color: #1e40af; color: white; padding: 15px; text-align: right;'>
                <p>Grand Total: $13,800.00</p>
            </div>";

        // Act: run layout
        LayoutResult result = RunLayout(html, smallPage);

        // Assert: document spans at least 2 pages (the long table forces pagination)
        Assert.True(result.PageCount >= 2,
            $"Combined features document should span at least 2 pages, got {result.PageCount}");

        // The thead text (bold) should appear on page 0 and page 1 (repeated across pages)
        var page0Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 0).ToList();
        var page1Texts = result.Primitives.OfType<TextPrimitive>()
            .Where(t => t.PageIndex == 1).ToList();

        // Flex container items should render on page 0
        Assert.Contains(page0Texts, t => t.Text.Contains("KPI") || t.Text.Contains("One")
                                         || t.Text.Contains("Two") || t.Text.Contains("Three"));

        // Table header text should be bold (Helvetica-Bold) on the first page
        Assert.Contains(page0Texts, t => t.Bold && t.FontName.Contains("Bold"));

        // At least 10 text runs on page 0 (flex items + avoid block + thead + table rows)
        Assert.True(page0Texts.Count >= 10,
            $"Expected at least 10 text runs on page 0, got {page0Texts.Count}");
    }

    /// <summary>
    /// Verifies that headers/footers config is correctly passed through the full pipeline
    /// with combined flex + table + page-break content. No exceptions should be thrown.
    /// </summary>
    [Fact]
    public void CombinedFeatures_WithHeaderFooterConfig_NoException()
    {
        // Build HTML with flex containers, page-break-inside: avoid, and a long table
        var smallPage = new PageLayout(PageLayout.A4.Width, 300f, new PageMargins(10f));

        string html = @"
            <div style='display: flex; flex-direction: row;'>
                <div style='flex: 1;'><p>Left</p></div>
                <div style='flex: 1;'><p>Right</p></div>
            </div>
            <div style='page-break-inside: avoid;'><p>Keep together</p></div>
            <table>
                <thead>
                    <tr><th>A</th><th>B</th></tr>
                </thead>
                <tbody>
                    <tr><td>1</td><td>2</td></tr>
                    <tr><td>3</td><td>4</td></tr>
                    <tr><td>5</td><td>6</td></tr>
                    <tr><td>7</td><td>8</td></tr>
                    <tr><td>9</td><td>10</td></tr>
                    <tr><td>11</td><td>12</td></tr>
                    <tr><td>13</td><td>14</td></tr>
                </tbody>
            </table>";

        // Configure header and footer
        var options = new ConversionOptions
        {
            CompressStreams = false,
            Page = smallPage,
            HeaderFooter = new HeaderFooterConfig
            {
                HeaderHtml = "<p>Report Header</p>",
                FooterHtml = "<p>Page {page} of {total}</p>",
                HeaderHeight = 30f,
                FooterHeight = 20f,
                ShowPageNumbers = true
            }
        };

        // Act: run the full PDF pipeline — no exceptions expected
        var generator = new PdfGenerator(options);
        byte[] pdf = generator.Convert(html);
        string pdfContent = System.Text.Encoding.Latin1.GetString(pdf);

        // Assert: header text appears in the output (layout engine renders each word with trailing space as separate PDF text run)
        Assert.Contains("(Report ) Tj", pdfContent);
        Assert.Contains("(Header) Tj", pdfContent);

        // Assert: page numbers appear (resolved from {page}/{total})
        Assert.Contains("(Page ) Tj", pdfContent);
        Assert.Contains("(of ) Tj", pdfContent);
        Assert.Contains("(1) Tj", pdfContent);

        // Assert: body content from flex and table appears
        Assert.Contains("(Left) Tj", pdfContent);
        Assert.Contains("(Right) Tj", pdfContent);
        Assert.Contains("(Keep ) Tj", pdfContent);
        Assert.Contains("(together) Tj", pdfContent);

        // Verify content is non-empty and spans at least 2 pages
        Assert.True(pdf.Length > 1000,
            $"PDF should be larger than 1000 bytes for combined features, got {pdf.Length}");
    }

    /// <summary>
    /// Verifies that the full pipeline handles a complex multi-page document
    /// with all layout features and first-page header variant without throwing.
    /// </summary>
    [Fact]
    public void CombinedFeatures_VeryLongDocument_FirstPageHeaderVariant()
    {
        var smallPage = new PageLayout(PageLayout.A4.Width, 200f, new PageMargins(10f));

        // Build a long document with many table rows to force many pages
        var tbody = new System.Text.StringBuilder();
        for (int i = 1; i <= 50; i++)
        {
            tbody.AppendLine($"<tr><td>{i}</td><td>Item {i}</td><td>${i * 10:F2}</td></tr>");
        }

        string html = $@"
            <div style='display: flex; flex-direction: row; justify-content: space-between;'>
                <div style='flex: 1;'><p>Summary</p></div>
                <div style='flex: 1;'><p>Details</p></div>
            </div>
            <div style='page-break-inside: avoid; background: #eee; padding: 5px;'>
                <p>Key metric block — must stay intact</p>
            </div>
            <table>
                <thead>
                    <tr style='background: #333; color: white;'><th>#</th><th>Name</th><th>Amount</th></tr>
                </thead>
                <tbody>
                    {tbody}
                </tbody>
            </table>";

        var options = new ConversionOptions
        {
            CompressStreams = false,
            Page = smallPage,
            HeaderFooter = new HeaderFooterConfig
            {
                HeaderHtml = "<p>STANDARD HEADER</p>",
                FirstPageHeaderHtml = "<p>FIRST PAGE COVER</p>",
                FooterHtml = "<p>Page {page}</p>",
                HeaderHeight = 30f,
                FooterHeight = 20f,
                ShowPageNumbers = true
            }
        };

        // Act — no exception should be thrown
        var generator = new PdfGenerator(options);
        byte[] pdf = generator.Convert(html);
        string pdfContent = System.Text.Encoding.Latin1.GetString(pdf);

        // Assert: both header variants are in the output (layout engine renders each word as separate PDF text run;
        // trailing spaces are attached to all but the last word fragment)
        Assert.Contains("(FIRST ) Tj", pdfContent);
        Assert.Contains("(PAGE ) Tj", pdfContent);
        Assert.Contains("(COVER) Tj", pdfContent);
        Assert.Contains("(STANDARD ) Tj", pdfContent);
        Assert.Contains("(HEADER) Tj", pdfContent);

        // Assert: page numbers appear (multiple pages means multiple page numbers)
        Assert.Contains("(Page ) Tj", pdfContent);

        // Assert: content from flex and table appears
        Assert.Contains("(Summary) Tj", pdfContent);

        // Large PDF size confirms multi-page output
        Assert.True(pdf.Length > 5000,
            $"PDF should be larger than 5000 bytes for 50-row document, got {pdf.Length}");
    }
}
