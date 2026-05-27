using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Fonts;

namespace HtmlToPdfDotNet.Library.Models.Layout;

/// <summary>
/// A run of text with the same style.
/// </summary>
public sealed class InlineRun
{
    public string Text { get; init; } = "";
    public string FontName { get; init; } = "Helvetica";
    public float FontSize { get; init; } = Constants.DefaultFontSize;
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public CssColor Color { get; init; }
    public TextDecoration TextDecoration { get; init; } = TextDecoration.None;
    public float Width { get; set; }   // measured

    /// <summary>
    /// When non-null, this run is rendered with an embedded TTF/OTF font instead
    /// of a standard PDF Type1 font.  The value is taken from <see cref="FontRegistry"/>.
    /// </summary>
    public EmbeddedFontInfo? EmbeddedFont { get; init; }
}