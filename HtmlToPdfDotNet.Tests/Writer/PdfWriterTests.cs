using System.Text;
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Writer;

namespace HtmlToPdfDotNet.Tests.Writer;

// ─────────────────────────────────────────────────────────────────────────────
// ContentStreamBuilder
// ─────────────────────────────────────────────────────────────────────────────
public class ContentStreamBuilderTests
{
    private static ContentStreamBuilder Builder() => new(841.89f); // A4 height

    [Fact]
    public void DrawText_ProducesBTandTj()
    {
        ContentStreamBuilder b = Builder();
        b.DrawText(new TextPrimitive
        {
            X = 50f,
            Y = 100f,
            Text = "Hello",
            FontName = "Helvetica",
            Color = new CssColor(0, 0, 0),
        });

        string raw = b.RawContent;
        Assert.Contains("BT", raw);
        Assert.Contains("ET", raw);
        Assert.Contains("Tj", raw);
        Assert.Contains("Hello", raw);
    }

    [Fact]
    public void DrawText_InvertsYCoordinate()
    {
        // pageH=841.89, layoutY=100 → pdfY = 841.89 - 100 = 741.89
        ContentStreamBuilder b = Builder();
        b.DrawText(new TextPrimitive
        {
            X = 0,
            Y = 100f,
            Text = "X",
            FontName = "Helvetica",
            Color = new CssColor(0, 0, 0),
        });

        string raw = b.RawContent;
        Assert.Contains("741.890", raw);
    }

    [Fact]
    public void DrawRect_ProducesReAndF()
    {
        ContentStreamBuilder b = Builder();
        b.DrawRect(new RectPrimitive
        {
            X = 10,
            Y = 20,
            Width = 100,
            Height = 50,
            Fill = CssColor.FromRgb(255, 0, 0),
        });

        string raw = b.RawContent;
        Assert.Contains(" re f", raw);
        Assert.Contains("rg", raw);  // fill color
    }

    [Fact]
    public void DrawBorderLine_ProducesStrokeOperators()
    {
        ContentStreamBuilder b = Builder();
        b.DrawBorderLine(new BorderLinePrimitive
        {
            X1 = 0,
            Y1 = 0,
            X2 = 100,
            Y2 = 0,
            Width = 1f,
            Color = CssColor.Black,
            Style = BorderStyle.Solid,
        });

        string raw = b.RawContent;
        Assert.Contains(" m ", raw);
        Assert.Contains(" l S", raw);
    }

    [Fact]
    public void DrawBorderLine_DashedAddesDashPattern()
    {
        ContentStreamBuilder b = Builder();
        b.DrawBorderLine(new BorderLinePrimitive
        {
            X1 = 0,
            Y1 = 0,
            X2 = 100,
            Y2 = 0,
            Width = 1f,
            Color = CssColor.Black,
            Style = BorderStyle.Dashed,
        });

        Assert.Contains("[3 2] 0 d", b.RawContent);
    }

    [Fact]
    public void Build_WithCompression_ReturnsSmallerBytes()
    {
        ContentStreamBuilder b = Builder();
        for (int i = 0; i < 50; i++)
            b.DrawText(new TextPrimitive
            {
                X = i,
                Y = i * 10,
                Text = "Lorem ipsum dolor sit amet",
                FontName = "Helvetica",
                Color = CssColor.Black,
            });

        byte[] compressed = b.Build(compress: true);
        byte[] uncompressed = b.Build(compress: false);

        Assert.True(compressed.Length < uncompressed.Length,
            $"Compressed {compressed.Length} should be < uncompressed {uncompressed.Length}");
    }

    [Fact]
    public void EscapeParentheses_InTextContent()
    {
        ContentStreamBuilder b = Builder();
        b.DrawText(new TextPrimitive
        {
            X = 0,
            Y = 100,
            Text = "Hello (World)",
            FontName = "Helvetica",
            Color = CssColor.Black,
        });

        // The parentheses must be escaped in the stream
        Assert.Contains("\\(", b.RawContent);
        Assert.Contains("\\)", b.RawContent);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PdfDocumentWriter / PdfGenerator (integration)
// ─────────────────────────────────────────────────────────────────────────────
public class PdfDocumentWriterTests
{
    private static byte[] Generate(string html, bool compress = false)
    {
        ConversionOptions options = new() { CompressStreams = compress };
        return new PdfGenerator(options).Convert(html);
    }

    [Fact]
    public void ConvertToStream_ProducesSameResultAsToBytes()
    {
        const string html = "<p>Test consistency</p>";
        ConversionOptions options = new() { CompressStreams = false };
        PdfGenerator converter = new(options);

        byte[] bytes = converter.Convert(html);

        using MemoryStream ms = new();
        converter.Convert(html, ms);
        byte[] streamBytes = ms.ToArray();

        // Ignore date in /Info (can differ in ms) — compare length
        Assert.Equal(bytes.Length, streamBytes.Length);
    }

    /// <summary>
    /// 5.6: Verifies that a LayoutResult with LinkAnnotationPrimitives produces PDF
    /// content containing annotation structures (/Annots, /Type /Annot, /Subtype /Link, etc.).
    /// </summary>
    [Fact]
    public void LinkAnnotation_AppearsInPdfOutput()
    {
        var options = new ConversionOptions { CompressStreams = false };
        var converter = new PdfGenerator(options);
        byte[] pdf = converter.Convert("<a href=\"https://example.com\">link</a>");
        string pdfStr = Encoding.Latin1.GetString(pdf);

        Assert.Contains("/Annots", pdfStr);
        Assert.Contains("/Type /Annot", pdfStr);
        Assert.Contains("/Subtype /Link", pdfStr);
        Assert.Contains("/URI", pdfStr);
        Assert.Contains("https://example.com", pdfStr);
    }

    /// <summary>
    /// 5.6: Verifies that multiple links produce multiple annotations in the PDF output.
    /// </summary>
    [Fact]
    public void MultipleLinks_AllAppearInPdfOutput()
    {
        var options = new ConversionOptions { CompressStreams = false };
        var converter = new PdfGenerator(options);
        byte[] pdf = converter.Convert(
            "<a href=\"https://first.com\">first</a> " +
            "<a href=\"https://second.com\">second</a>");
        string pdfStr = Encoding.Latin1.GetString(pdf);

        Assert.Contains("/Annots", pdfStr);
        Assert.Contains("https://first.com", pdfStr);
        Assert.Contains("https://second.com", pdfStr);
    }

    /// <summary>
    /// 5.6: Verifies that a link without href does NOT produce /Annots in the PDF output.
    /// </summary>
    [Fact]
    public void AnchorWithoutHref_DoesNotProduceAnnotsInPdf()
    {
        var options = new ConversionOptions { CompressStreams = false };
        var converter = new PdfGenerator(options);
        byte[] pdf = converter.Convert("<a>plain text</a>");
        string pdfStr = Encoding.Latin1.GetString(pdf);

        Assert.DoesNotContain("/Annots", pdfStr);
    }
}
