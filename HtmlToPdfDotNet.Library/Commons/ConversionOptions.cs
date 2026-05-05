using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Writer.Font;

namespace HtmlToPdfDotNet.Library.Commons;

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
