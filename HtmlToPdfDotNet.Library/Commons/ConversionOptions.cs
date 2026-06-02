using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Fonts;

namespace HtmlToPdfDotNet.Library.Commons;

public class ConversionOptions
{
    public ConversionOptions()
    {
    }

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
                source.Page.Margins.Left));
        CompressStreams = source.CompressStreams;
        BasePath = source.BasePath;
        StyleSheets = source.StyleSheets.ToArray();
        Fonts = new FontRegistry(source.Fonts);

        if (!string.IsNullOrWhiteSpace(BasePath) && !Directory.Exists(BasePath))
        {
            throw new DirectoryNotFoundException($"Base path not found: {BasePath}");
        }
    }

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
}
