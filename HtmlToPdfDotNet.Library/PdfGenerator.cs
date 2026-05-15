
using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;
using HtmlToPdfDotNet.Library.Models.Writer;

namespace HtmlToPdfDotNet.Library;

/// <summary>
/// Public entry point for the library.
/// Converts HTML to PDF through three phases:
///   1. CSS Engine  → ComputedStyle per node
///   2. Layout Engine → LayoutResult with RenderPrimitives
///   3. PDF Writer    → PDF 1.7 bytes
///
/// Usage:
/// <code>
/// var converter = new PdfGenerator();
/// byte[] pdf = converter.Convert("&lt;h1&gt;Hello&lt;/h1&gt;&lt;p&gt;World&lt;/p&gt;");
/// File.WriteAllBytes("output.pdf", pdf);
/// </code>
/// </summary>
public class PdfGenerator : IPdfGenerator
{
    private readonly ConversionOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfGenerator"/> class.
    /// </summary>
    /// <param name="options">Optional conversion options. If null, default settings (A4, compression enabled) are used.</param>
    public PdfGenerator(ConversionOptions? options = null)
        => _options = options ?? new ConversionOptions();

    /// <summary>
    /// Converts an HTML string into a PDF byte array.
    /// </summary>
    /// <param name="html">The raw HTML content to convert.</param>
    /// <returns>A byte array representing the generated PDF.</returns>
    public byte[] Convert(string html)
    {
        using MemoryStream ms = new();
        Convert(html, ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Converts an HTML string and writes the resulting PDF to the specified output stream.
    /// </summary>
    /// <param name="html">The raw HTML content to convert.</param>
    /// <param name="output">The destination stream for the PDF data.</param>
    public void Convert(string html, Stream output)
    {
        LayoutResult layout = RunLayout(html);
        PdfDocumentWriter writer = new(_options.Page, _options.CompressStreams);
        writer.Write(layout, output);
    }

    /// <summary>
    /// Writes the converted PDF to the specified file.
    /// </summary>
    /// <param name="html">The raw HTML content to convert.</param>
    /// <param name="pdfPath">The destination path for the generated PDF file.</param>
    public void WritePdfFile(string html, string pdfPath)
    {
        byte[] rawPdf = Convert(html);
        using FileStream fs = File.OpenWrite(pdfPath);
        fs.Write(rawPdf);
    }

    /// <summary>
    /// Internal pipeline that executes the first two phases: HTML parsing/styling and layout engine.
    /// </summary>
    /// <param name="html">The HTML content to process.</param>
    /// <returns>The calculated layout result containing primitives for all pages.</returns>
    public LayoutResult RunLayout(string html)
    {
        HtmlDocument doc = new();

        doc.LoadHtml(Helpers.CleanHtml(html));

        List<CssRule> cssRules = _options.StyleSheets
                                         .SelectMany(CssStyleSheetParser.Parse)
                                         .Concat(CssStyleSheetParser.ParseFromHtml(html))
                                         .ToList();

        StyleResolver resolver = new(cssRules);
        Dictionary<HtmlNode, ComputedStyle> styles = resolver.Resolve(doc.DocumentNode);

        BlockLayoutEngine engine = new(_options.Page, styles, _options.Fonts, _options.BasePath);
        return engine.Layout(doc.DocumentNode);
    }
}