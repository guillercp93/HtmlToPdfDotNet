
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
        => _options = new ConversionOptions(options ?? new ConversionOptions());

    /// <summary>
    /// Converts an HTML string into a PDF byte array.
    /// </summary>
    /// <param name="html">The raw HTML content to convert.</param>
    /// <returns>A byte array representing the generated PDF.</returns>
    public byte[] Convert(string html)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(html);

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
        ArgumentNullException.ThrowIfNullOrEmpty(html);
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite)
        {
            throw new ArgumentException("The output stream must be writable.", nameof(output));
        }

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
        ArgumentNullException.ThrowIfNullOrEmpty(html);
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            throw new ArgumentException("The PDF output path cannot be null, empty, or whitespace.", nameof(pdfPath));
        }

        string resolvedPath = pdfPath;
        if (!string.IsNullOrEmpty(_options.BasePath))
        {
            string canonicalBasePath = Path.GetFullPath(_options.BasePath);
            if (!canonicalBasePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                canonicalBasePath += Path.DirectorySeparatorChar;
            }

            string fullPdfPath = Path.GetFullPath(pdfPath);
            if (!fullPdfPath.StartsWith(canonicalBasePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access to the path '{pdfPath}' is denied. It lies outside the allowed base directory.");
            }
            resolvedPath = fullPdfPath;
        }

        byte[] rawPdf = Convert(html);
        using FileStream fs = File.OpenWrite(resolvedPath);
        fs.Write(rawPdf);
    }

    /// <summary>
    /// Internal pipeline that executes the first two phases: HTML parsing/styling and layout engine.
    /// </summary>
    /// <param name="html">The HTML content to process.</param>
    /// <returns>The calculated layout result containing primitives for all pages.</returns>
    public LayoutResult RunLayout(string html)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(html);

        HtmlDocument doc = new();

        doc.LoadHtml(Helpers.CleanHtml(html));

        List<CssRule> cssRules = ResolveStyleSheets(_options.StyleSheets)
                                         .SelectMany(CssStyleSheetParser.Parse)
                                         .Concat(CssStyleSheetParser.ParseFromHtml(html))
                                         .ToList();

        StyleResolver resolver = new(cssRules);
        Dictionary<HtmlNode, ComputedStyle> styles = resolver.Resolve(doc.DocumentNode);

        BlockLayoutEngine engine = new(_options.Page, styles, _options.Fonts, _options.BasePath);
        return engine.Layout(doc.DocumentNode);
    }

    private static IEnumerable<string> ResolveStyleSheets(IEnumerable<string> styleSheets)
    {
        foreach (string styleSheet in styleSheets)
        {
            if (string.IsNullOrWhiteSpace(styleSheet))
            {
                throw new ArgumentException("Stylesheet entries cannot be null, empty, or whitespace.", nameof(styleSheets));
            }

            if (File.Exists(styleSheet))
            {
                yield return File.ReadAllText(styleSheet);
                continue;
            }

            if (LooksLikePath(styleSheet))
            {
                throw new FileNotFoundException($"Stylesheet file not found: {styleSheet}", styleSheet);
            }

            yield return styleSheet;
        }
    }

    private static bool LooksLikePath(string value)
        => value.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
           || value.Contains(Path.DirectorySeparatorChar)
           || value.Contains(Path.AltDirectorySeparatorChar);
}
