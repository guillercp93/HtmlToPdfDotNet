using System.Text;
using HtmlAgilityPack;
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Tests.Integration;

/// <summary>
/// Integration tests for PDF link annotations (Phases 4-6).
/// Verifies end-to-end: HTML → layout annotations → PDF annotation objects.
/// </summary>
public class LinkAnnotationIntegrationTests
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
    /// 6.1: Full integration test — renders HTML with a link, verifies LayoutResult
    /// annotations are correct AND the PDF output contains the annotation structure.
    /// </summary>
    [Fact]
    public void LinkAnnotation_FullPipeline_ProducesCorrectLayoutAndPdf()
    {
        const string html = "<a href=\"https://example.com\">Click here</a>";

        // Phase 1-2: Layout
        LayoutResult layout = RunLayout(html);

        Assert.NotEmpty(layout.Annotations);
        Assert.All(layout.Annotations, a =>
        {
            Assert.Equal("https://example.com", a.Uri);
            Assert.True(a.Width > 0, "Annotation width must be positive");
            Assert.True(a.Height > 0, "Annotation height must be positive");
            Assert.Equal(0, a.PageIndex);
        });

        // Phase 3: PDF Writer
        var options = new ConversionOptions { CompressStreams = false };
        var converter = new PdfGenerator(options);
        byte[] pdf = converter.Convert(html);
        string pdfStr = Encoding.Latin1.GetString(pdf);

        Assert.Contains("/Annots", pdfStr);
        Assert.Contains("/Type /Annot", pdfStr);
        Assert.Contains("/Subtype /Link", pdfStr);
        Assert.Contains("/URI", pdfStr);
        Assert.Contains("https://example.com", pdfStr);
    }

    /// <summary>
    /// 6.1: Verifies that a link that spans multiple pages produces annotations
    /// on each page it occupies.
    /// </summary>
    [Fact]
    public void LinkSpanningMultiplePages_HasAnnotationsOnEachPage()
    {
        // Use a very short page so the link content spans multiple pages.
        // ContentHeight ≈ 60 - 10 - 10 = 40pt. Default font 12pt, line height 1.2 → 14.4pt/line.
        // Each page fits ≈ 2 lines. Need ~100+ words to guarantee 3+ pages.
        var shortPage = new PageLayout(PageLayout.A4.Width, 60f, new PageMargins(10f));
        LayoutResult layout = RunLayout(
            "<a href=\"https://multi-page.com\">" +
            "this is a very long link text that should span across multiple pages " +
            "because the page height is very short and this text needs to wrap " +
            "many many times to fill up several pages of content with this tiny " +
            "page size that only fits a couple of lines per page so here are more " +
            "words words words words words words words words words words words " +
            "words words words words words words words words words words words " +
            "words words words words words words words words words words words " +
            "words words words words words words words words words words words " +
            "words words words words words words words words words words words " +
            "</a>",
            shortPage);

        Assert.True(layout.PageCount >= 2,
            $"Expected at least 2 pages, got {layout.PageCount}");

        // Verify annotations exist on multiple pages
        var pageIndices = layout.Annotations
            .Where(a => a.Uri == "https://multi-page.com")
            .Select(a => a.PageIndex)
            .Distinct()
            .OrderBy(i => i)
            .ToList();

        Assert.True(pageIndices.Count >= 2,
            $"Expected annotations on at least 2 pages, got {pageIndices.Count} page(s): {string.Join(", ", pageIndices)}");

        // Each annotation should have the correct URI and valid bounds
        Assert.All(layout.Annotations, a =>
        {
            Assert.Equal("https://multi-page.com", a.Uri);
            Assert.True(a.Width > 0);
            Assert.True(a.Height > 0);
        });
    }

    /// <summary>
    /// 6.1: Verifies that multiple links on the same page all produce correct annotations
    /// and PDF content.
    /// </summary>
    [Fact]
    public void MultipleLinksOnSamePage_AllProduceCorrectAnnotations()
    {
        const string html = "<p><a href=\"https://first.com\">first link</a></p>" +
                            "<p><a href=\"https://second.com\">second link</a></p>";

        // Layout check
        LayoutResult layout = RunLayout(html);

        List<string> uris = layout.Annotations.Select(a => a.Uri).Distinct().OrderBy(u => u).ToList();
        Assert.Contains("https://first.com", uris);
        Assert.Contains("https://second.com", uris);
        Assert.All(layout.Annotations, a => Assert.Equal(0, a.PageIndex));

        // PDF check
        var options = new ConversionOptions { CompressStreams = false };
        var converter = new PdfGenerator(options);
        byte[] pdf = converter.Convert(html);
        string pdfStr = Encoding.Latin1.GetString(pdf);

        Assert.Contains("https://first.com", pdfStr);
        Assert.Contains("https://second.com", pdfStr);
    }

    /// <summary>
    /// 6.1: Verifies that the annotation rectangle values are numeric and in the PDF coordinate space.
    /// The /Rect array should have 4 numeric values.
    /// </summary>
    [Fact]
    public void LinkAnnotation_RectHasFourValues()
    {
        var options = new ConversionOptions { CompressStreams = false };
        var converter = new PdfGenerator(options);
        byte[] pdf = converter.Convert("<a href=\"https://example.com/path\">link</a>");
        string pdfStr = Encoding.Latin1.GetString(pdf);

        // Find the /Rect [...] pattern and verify it contains 4 space-separated numbers
        int rectIdx = pdfStr.IndexOf("/Rect [", StringComparison.Ordinal);
        Assert.True(rectIdx >= 0, "/Rect not found in PDF");

        int endBracket = pdfStr.IndexOf(']', rectIdx);
        Assert.True(endBracket > rectIdx, "Closing bracket for /Rect not found");

        string rectContent = pdfStr[(rectIdx + 7)..endBracket].Trim();
        string[] parts = rectContent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, parts.Length);

        // All parts should be parseable as floats
        foreach (string part in parts)
        {
            Assert.True(float.TryParse(part, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _),
                $"'{part}' is not a valid float in Rect array");
        }
    }

    /// <summary>
    /// 6.1: Verifies that link URIs with special characters (parentheses) are properly escaped.
    /// </summary>
    [Fact]
    public void LinkUriWithSpecialCharacters_IsEscapedInPdf()
    {
        // URI with parentheses — these must be escaped in PDF string literals
        var options = new ConversionOptions { CompressStreams = false };
        var converter = new PdfGenerator(options);
        byte[] pdf = converter.Convert("<a href=\"https://example.com/a(b)c\">link</a>");
        string pdfStr = Encoding.Latin1.GetString(pdf);

        // The URI in the PDF should have escaped parentheses
        Assert.Contains("a\\(b\\)c", pdfStr);
    }

    /// <summary>
    /// 6.1: Verifies that annotations still emit /Annots when compression is enabled.
    /// </summary>
    [Fact]
    public void LinkAnnotation_WithCompression_StillContainsAnnots()
    {
        // With compression, we can't read the content stream,
        // but /Annots and annotation dictionaries are not compressed
        // (they're in the page object dictionary, not the content stream).
        var options = new ConversionOptions { CompressStreams = true };
        var converter = new PdfGenerator(options);
        byte[] pdf = converter.Convert("<a href=\"https://example.com\">link</a>");
        string pdfStr = Encoding.Latin1.GetString(pdf);

        Assert.Contains("/Annots", pdfStr);
        Assert.Contains("/Type /Annot", pdfStr);
        Assert.Contains("/Subtype /Link", pdfStr);
        Assert.Contains("/URI", pdfStr);
    }
}
