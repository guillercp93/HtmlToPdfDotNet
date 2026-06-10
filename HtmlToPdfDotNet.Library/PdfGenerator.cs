
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

        // Layout and attach header/footer primitives if configured
        if (_options.HeaderFooter != null)
        {
            AddHeaderFooterPrimitives(layout, _options.HeaderFooter);
        }

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

    #region Header/Footer Layout

    /// <summary>
    /// Lays out header/footer HTML templates and attaches per-page primitives
    /// to the layout result so that <see cref="PdfDocumentWriter"/> can emit them.
    /// </summary>
    /// <param name="layout">The layout result.</param>
    /// <param name="config">The header/footer configuration.</param>
    private void AddHeaderFooterPrimitives(LayoutResult layout, HeaderFooterConfig config)
    {
        // Initialize per-page lists
        layout.PageHeaders = new List<List<RenderPrimitive>>(layout.PageCount);
        layout.PageFooters = new List<List<RenderPrimitive>>(layout.PageCount);

        for (int i = 0; i < layout.PageCount; i++)
        {
            int pageNum = i + 1;
            int totalPages = layout.PageCount;

            // --- Header ---
            if (!string.IsNullOrEmpty(config.HeaderHtml))
            {
                string headerHtml = config.HeaderHtml;
                if (i == 0 && config.FirstPageHeaderHtml != null)
                    headerHtml = config.FirstPageHeaderHtml;

                headerHtml = ResolvePageNumbers(headerHtml, config, pageNum, totalPages);
                List<RenderPrimitive> headerPrims = LayoutHtmlFragment(headerHtml, _options.Page.ContentWidth);

                // Shift header primitives to the top of the content area
                float yOffset = _options.Page.Margins.Top;
                for (int j = 0; j < headerPrims.Count; j++)
                    headerPrims[j] = ShiftPrimitiveY(headerPrims[j], yOffset);

                layout.PageHeaders.Add(headerPrims);
            }
            else
            {
                layout.PageHeaders.Add([]);
            }

            // --- Footer ---
            if (!string.IsNullOrEmpty(config.FooterHtml))
            {
                string footerHtml = ResolvePageNumbers(config.FooterHtml, config, pageNum, totalPages);
                List<RenderPrimitive> footerPrims = LayoutHtmlFragment(footerHtml, _options.Page.ContentWidth);

                // Compute footer position: footer sits at the bottom of the content area
                float footerContentHeight = ComputePrimitivesHeight(footerPrims);
                float yOffset = _options.Page.Margins.Top + _options.Page.ContentHeight - footerContentHeight;
                for (int j = 0; j < footerPrims.Count; j++)
                    footerPrims[j] = ShiftPrimitiveY(footerPrims[j], yOffset);

                layout.PageFooters.Add(footerPrims);
            }
            else
            {
                layout.PageFooters.Add([]);
            }
        }
    }

    /// <summary>
    /// Substitutes <c>{page}</c> and <c>{total}</c> placeholders in the HTML string
    /// when <see cref="HeaderFooterConfig.ShowPageNumbers"/> is enabled.
    /// </summary>
    /// <param name="html">The HTML string containing placeholders.</param>
    /// <param name="config">The header/footer configuration.</param>
    /// <param name="pageNum">The current page number.</param>
    /// <param name="totalPages">The total number of pages.</param>
    /// <returns>The HTML string with placeholders resolved.</returns>
    private static string ResolvePageNumbers(string html,
                                             HeaderFooterConfig config,
                                             int pageNum,
                                             int totalPages)
    {
        if (!config.ShowPageNumbers) return html;
        return html.Replace("{page}", pageNum.ToString())
                   .Replace("{total}", totalPages.ToString());
    }

    /// <summary>
    /// Lays out an HTML fragment using a mini layout engine and returns the resulting primitives.
    /// The fragment is laid out at Y=0 with the given available width.
    /// </summary>
    /// <param name="html">The HTML fragment to lay out.</param>
    /// <param name="contentWidth">The available width for the layout.</param>
    /// <returns>The list of render primitives for the HTML fragment.</returns>
    private static List<RenderPrimitive> LayoutHtmlFragment(string html, float contentWidth)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];

        HtmlDocument doc = new();
        doc.LoadHtml(html);
        StyleResolver resolver = new();
        Dictionary<HtmlNode, ComputedStyle> styles = resolver.Resolve(doc.DocumentNode);

        // Use a tall virtual page with no margins — we only care about X/Y placement
        PageLayout virtualPage = new(contentWidth, 10000f, new PageMargins(0f));
        BlockLayoutEngine engine = new(virtualPage, styles);
        LayoutResult result = engine.Layout(doc.DocumentNode);
        return result.Primitives;
    }

    /// <summary>
    /// Creates a copy of the primitive with its Y coordinate shifted by <paramref name="dy"/>.
    /// </summary>
    /// <param name="prim">The render primitive to shift.</param>
    /// <param name="dy">The amount to shift the Y coordinate by.</param>
    /// <returns>The shifted render primitive.</returns>
    private static RenderPrimitive ShiftPrimitiveY(RenderPrimitive prim, float dy)
    {
        return prim switch
        {
            TextPrimitive tp => new TextPrimitive
            {
                PageIndex = tp.PageIndex,
                X = tp.X,
                Y = tp.Y + dy,
                Text = tp.Text,
                FontName = tp.FontName,
                FontSize = tp.FontSize,
                Bold = tp.Bold,
                Italic = tp.Italic,
                Color = tp.Color,
                EmbeddedFont = tp.EmbeddedFont,
            },
            RectPrimitive rp => new RectPrimitive
            {
                PageIndex = rp.PageIndex,
                X = rp.X,
                Y = rp.Y + dy,
                Width = rp.Width,
                Height = rp.Height,
                Fill = rp.Fill,
                BorderRadius = rp.BorderRadius,
                Stroke = rp.Stroke,
                StrokeWidth = rp.StrokeWidth,
            },
            BorderLinePrimitive blp => new BorderLinePrimitive
            {
                PageIndex = blp.PageIndex,
                X1 = blp.X1,
                Y1 = blp.Y1 + dy,
                X2 = blp.X2,
                Y2 = blp.Y2 + dy,
                Width = blp.Width,
                Color = blp.Color,
                Style = blp.Style,
            },
            _ => prim,
        };
    }

    /// <summary>
    /// Computes the height of primitives by finding the maximum Y + font size / height.
    /// </summary>
    /// <param name="prims">The list of render primitives.</param>
    /// <returns>The height of the primitives.</returns>
    private static float ComputePrimitivesHeight(List<RenderPrimitive> prims)
    {
        if (prims.Count == 0) return 0f;

        float maxY = 0f;
        foreach (var p in prims)
        {
            switch (p)
            {
                case TextPrimitive tp:
                    maxY = Math.Max(maxY, tp.Y + tp.FontSize);
                    break;
                case RectPrimitive rp:
                    maxY = Math.Max(maxY, rp.Y + rp.Height);
                    break;
                case BorderLinePrimitive blp:
                    maxY = Math.Max(maxY, Math.Max(blp.Y1, blp.Y2));
                    break;
            }
        }

        return maxY;
    }

    #endregion

    /// <summary>
    /// Resolves the absolute paths of external CSS files.
    /// </summary>
    /// <param name="styleSheets">The list of CSS file paths to resolve.</param>
    /// <returns>An enumerable of absolute CSS file paths.</returns>
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

    /// <summary>
    /// Checks if a string looks like a file path.
    /// </summary>
    /// <param name="value">The string to check.</param>
    private static bool LooksLikePath(string value)
        => value.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
           || value.Contains(Path.DirectorySeparatorChar)
           || value.Contains(Path.AltDirectorySeparatorChar);
}
