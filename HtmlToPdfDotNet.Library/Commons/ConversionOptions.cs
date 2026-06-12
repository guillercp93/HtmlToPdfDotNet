using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Fonts;

namespace HtmlToPdfDotNet.Library.Commons;

/// <summary>
/// Configuration for the conversion pipeline. Passed to the <see cref="PdfGenerator"/>
/// to control page layout, compression, fonts, stylesheets, and header/footer templates.
/// </summary>
public class ConversionOptions
{
    /// <summary>
    /// Page settings (size and margins).
    /// </summary>
    public PageLayout Page { get; set; } = PageLayout.A4;

    /// <summary>
    /// If true, content streams are compressed using FlateDecode.
    /// </summary>
    public bool CompressStreams { get; set; } = true;

    /// <summary>
    /// Optional base path for resolving relative paths (images, stylesheets).
    /// </summary>
    public string? BasePath { get; set; }

    /// <summary>
    /// Optional list of CSS file paths to include in the PDF.
    /// </summary>
    public IEnumerable<string> StyleSheets { get; set; } = [];

    /// <summary>
    /// Optional registry of embedded TTF/OTF fonts.
    /// When a font family referenced in the HTML/CSS is found here,
    /// it is parsed for exact glyph metrics and embedded as a subset
    /// in the output PDF instead of falling back to the standard PDF fonts.
    ///
    /// Usage:
    /// <code>
    ///   var opts = new ConversionOptions();
    ///   opts.Fonts.RegisterFont("/path/to/Roboto-Regular.ttf", "Roboto");
    ///   opts.Fonts.RegisterFont("/path/to/Roboto-Bold.ttf",    "Roboto", bold: true);
    /// </code>
    /// </summary>
    public FontRegistry Fonts { get; } = new FontRegistry();

    /// <summary>
    /// Optional configuration for running headers and footers on each page.
    /// When set, the specified header/footer HTML templates are laid out
    /// and rendered on every page of the output PDF.
    /// </summary>
    public HeaderFooterConfig? HeaderFooter { get; set; }

    /// <summary>
    /// Creates a new instance with default settings.
    /// </summary>
    public ConversionOptions()
    {
    }

    /// <summary>
    /// Internal copy constructor used by <see cref="PdfGenerator"/>.
    /// Deep-copies all mutable reference types including <see cref="HeaderFooterConfig"/>.
    /// </summary>
    /// <param name="source">The source options to copy from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or
    /// its <see cref="Page"/> or <see cref="StyleSheets"/> are null.</exception>
    internal ConversionOptions(ConversionOptions source)
    {
        ArgumentNullException.ThrowIfNull(source.Page);
        ArgumentNullException.ThrowIfNull(source.StyleSheets);

        Page = new PageLayout(
            source.Page.Width,
            source.Page.Height,
            new PageMargins(
                source.Page.Margins.Top,
                source.Page.Margins.Right,
                source.Page.Margins.Bottom,
                source.Page.Margins.Left))
        {
            ReservedHeaderFooterHeight = source.Page.ReservedHeaderFooterHeight
        };
        CompressStreams = source.CompressStreams;
        BasePath = source.BasePath;
        StyleSheets = source.StyleSheets.ToArray();
        Fonts = new FontRegistry(source.Fonts);

        // Deep-copy HeaderFooterConfig
        if (source.HeaderFooter != null)
        {
            HeaderFooter = new HeaderFooterConfig
            {
                HeaderHtml = source.HeaderFooter.HeaderHtml,
                FooterHtml = source.HeaderFooter.FooterHtml,
                FirstPageHeaderHtml = source.HeaderFooter.FirstPageHeaderHtml,
                HeaderHeight = source.HeaderFooter.HeaderHeight,
                FooterHeight = source.HeaderFooter.FooterHeight,
                ShowPageNumbers = source.HeaderFooter.ShowPageNumbers,
            };
        }

        if (!string.IsNullOrWhiteSpace(BasePath) && !Directory.Exists(BasePath))
        {
            throw new DirectoryNotFoundException($"Base path not found: {BasePath}");
        }
    }
}
