using System.Text;
using System.Text.RegularExpressions;
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;

namespace HtmlToPdfDotNet.Tests.Writer;

public class PdfIntegrationSnapshotTests
{
    private const string OneByOnePngDataUri =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4//8/AAX+Av4N70a4AAAAAElFTkSuQmCC";

    private const string DejaVuRegular = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";

    private static byte[] Generate(string html, bool compress = false, Action<ConversionOptions>? configure = null)
    {
        ConversionOptions options = new() { CompressStreams = compress };
        configure?.Invoke(options);
        return new PdfGenerator(options).Convert(html);
    }

    [Fact]
    public void CompletePdf_HasValidXrefTrailerAndObjectOffsets()
    {
        byte[] pdf = Generate("<h1>Invoice</h1><p>Total: $42.00</p>");
        string text = Encoding.Latin1.GetString(pdf);

        Assert.StartsWith("%PDF-1.7", text);
        Assert.EndsWith("%%EOF", text);

        int xrefOffset = ParseStartXref(text);
        Assert.Equal("xref", ReadLineAt(text, xrefOffset));

        int xrefIndex = text.IndexOf("xref\n", StringComparison.Ordinal);
        Assert.True(xrefIndex >= 0, "Expected xref table.");

        string[] xrefLines = text[xrefIndex..].Split('\n');
        Match subsection = Regex.Match(xrefLines[1], @"^0 (?<size>\d+)\r?$");
        Assert.True(subsection.Success, "Expected xref subsection header.");

        int size = int.Parse(subsection.Groups["size"].Value);
        Assert.Contains($"/Size {size}", text);
        Assert.Contains("/Root 1 0 R", text);
        Assert.Contains("/Info 2 0 R", text);

        for (int objectNumber = 1; objectNumber < size; objectNumber++)
        {
            Match entry = Regex.Match(xrefLines[objectNumber + 2], @"^(?<offset>\d{10}) 00000 n \r?$");
            Assert.True(entry.Success, $"Missing xref entry for object {objectNumber}.");

            int offset = int.Parse(entry.Groups["offset"].Value);
            string expectedObjectHeader = $"{objectNumber} 0 obj";
            Assert.StartsWith(expectedObjectHeader, text[offset..]);
        }
    }

    [Fact]
    public void UncompressedStandardFontPdf_ContainsExpectedTextAndDrawingOperators()
    {
        byte[] pdf = Generate("""
            <html>
              <body>
                <style>td { border: 1pt solid #000000; }</style>
                <h1>SnapshotInvoice</h1>
                <p class='total'>TotalDue</p>
                <div style='background-color:#ffeeee;width:100pt;height:20pt'>BoxFill</div>
                <table>
                  <tr><td>Item</td><td>Amount</td></tr>
                  <tr><td>Service</td><td>42.00</td></tr>
                </table>
              </body>
            </html>
            """, compress: false);

        string text = Encoding.Latin1.GetString(pdf);

        Assert.Contains("(SnapshotInvoice)", text);
        Assert.Contains("(TotalDue)", text);
        Assert.Contains("(Service)", text);
        Assert.Contains("(42.00)", text);
        Assert.Contains(" re f", text);
        Assert.Contains(" l S", text);
        Assert.Contains("/Subtype /Type1", text);
    }

    [Fact]
    public void ImageDocument_ContainsImageXObjectAndDrawCommand()
    {
        byte[] pdf = Generate($"""
            <html>
              <body>
                <p>Logo</p>
                <img src="{OneByOnePngDataUri}" width="12" height="12" />
              </body>
            </html>
            """, compress: false);

        string text = Encoding.Latin1.GetString(pdf);

        Assert.Contains("/Type /XObject", text);
        Assert.Contains("/Subtype /Image", text);
        Assert.Contains("/Width 1", text);
        Assert.Contains("/Height 1", text);
        Assert.Contains("/XObject <<", text);
        Assert.Matches(@"/Im\d+ Do", text);
    }

    [Fact]
    public void EmbeddedFontDocument_ContainsCompleteFontObjectChain()
    {
        if (!File.Exists(DejaVuRegular)) return;

        byte[] pdf = Generate(
            "<p style='font-family:DejaVu Sans'>Embedded Hello</p>",
            compress: false,
            options => options.Fonts.RegisterFont(DejaVuRegular, "DejaVu Sans"));

        string text = Encoding.Latin1.GetString(pdf);

        Assert.Contains("/Subtype /Type0", text);
        Assert.Contains("/Subtype /CIDFontType2", text);
        Assert.Contains("/Type /FontDescriptor", text);
        Assert.Contains("/FontFile2", text);
        Assert.Contains("/ToUnicode", text);
        Assert.Contains("/Identity-H", text);
        Assert.Contains("/W [", text);
    }

    [Fact]
    public void EquivalentStandardFontDocuments_AreDeterministicAfterCreationDateNormalization()
    {
        const string html = "<h1>Stable</h1><p>Same input should produce same output.</p>";

        byte[] first = Generate(html, compress: false);
        byte[] second = Generate(html, compress: false);

        string normalizedFirst = NormalizeCreationDate(Encoding.Latin1.GetString(first));
        string normalizedSecond = NormalizeCreationDate(Encoding.Latin1.GetString(second));

        Assert.Equal(normalizedFirst, normalizedSecond);
    }

    private static int ParseStartXref(string text)
    {
        Match match = Regex.Match(text, @"startxref\s+(?<offset>\d+)\s+%%EOF$", RegexOptions.Singleline);
        Assert.True(match.Success, "Expected startxref value before EOF.");
        return int.Parse(match.Groups["offset"].Value);
    }

    private static string ReadLineAt(string text, int offset)
    {
        int end = text.IndexOf('\n', offset);
        Assert.True(end >= offset, "Expected a line at the requested offset.");
        return text[offset..end].TrimEnd('\r');
    }

    private static string NormalizeCreationDate(string text)
        => Regex.Replace(text, @"/CreationDate \(D:\d{14}Z\)", "/CreationDate (D:00000000000000Z)");
}
