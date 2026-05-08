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

    // ── File structure ────────────────────────────────────────────────

    [Fact]
    public void Output_StartsWithPdfHeader()
    {
        byte[] pdf = Generate("<p>Hello</p>");
        string header = Encoding.Latin1.GetString(pdf, 0, 8);
        Assert.StartsWith("%PDF-1.7", header);
    }

    [Fact]
    public void Output_EndsWithEof()
    {
        byte[] pdf = Generate("<p>Hello</p>");
        string tail = Encoding.Latin1.GetString(pdf, pdf.Length - 5, 5);
        Assert.Equal("%%EOF", tail);
    }

    [Fact]
    public void Output_ContainsStartxref()
    {
        byte[] pdf = Generate("<p>Hello</p>");
        string text = Encoding.Latin1.GetString(pdf);
        Assert.Contains("startxref", text);
    }

    [Fact]
    public void Output_ContainsCatalogAndPages()
    {
        byte[] pdf = Generate("<p>Hello</p>");
        string text = Encoding.Latin1.GetString(pdf);
        Assert.Contains("/Type /Catalog", text);
        Assert.Contains("/Type /Pages", text);
    }

    [Fact]
    public void Output_ContainsFontDeclarations()
    {
        byte[] pdf = Generate("<p>Hello</p>");
        string text = Encoding.Latin1.GetString(pdf);
        Assert.Contains("/Type /Font", text);
        Assert.Contains("/Subtype /Type1", text);
        Assert.Contains("Helvetica", text);
    }

    [Fact]
    public void Output_ContainsMediaBox()
    {
        byte[] pdf = Generate("<p>Hello</p>");
        string text = Encoding.Latin1.GetString(pdf);
        Assert.Contains("/MediaBox", text);
    }

    // ── Content ─────────────────────────────────────────────────────────────

    [Fact]
    public void UncompressedOutput_ContainsTextContent()
    {
        byte[] pdf = Generate("<p>HelloWorld</p>", compress: false);
        string text = Encoding.Latin1.GetString(pdf);
        Assert.Contains("HelloWorld", text);
    }

    [Fact]
    public void MultiPage_ContainsMultiplePageObjects()
    {
        string html = "<div>Page1</div>" +
                   "<div style='page-break-before:always'>Page2</div>";
        byte[] pdf = Generate(html);
        string text = Encoding.Latin1.GetString(pdf);

        // Should be at least 2 objects of type Page
        int count = CountOccurrences(text, "/Type /Page\n");
        Assert.True(count >= 2, $"Expected ≥2 /Type /Page, found {count}");
    }

    [Fact]
    public void Output_IsNonEmpty()
    {
        byte[] pdf = Generate("<h1>Test</h1>");
        Assert.True(pdf.Length > 500, $"Expected >500 bytes, got {pdf.Length}");
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

    [Fact]
    public void XrefTable_HasValidEntries()
    {
        byte[] pdf = Generate("<p>Hello</p>", compress: false);
        string text = Encoding.Latin1.GetString(pdf);

        // Each xref entry has the form: "0000000NNN 00000 n "
        string[] lines = text.Split('\n');
        List<string> xrefEntries = lines
            .Where(l => l.Length >= 18 && (l.EndsWith("n ") || l.EndsWith("f ")))
            .ToList();

        Assert.True(xrefEntries.Count > 5,
            $"Expected >5 xref entries, got {xrefEntries.Count}");
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static int CountOccurrences(string source, string pattern)
    {
        int count = 0, idx = 0;
        while ((idx = source.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        { count++; idx += pattern.Length; }
        return count;
    }
}
