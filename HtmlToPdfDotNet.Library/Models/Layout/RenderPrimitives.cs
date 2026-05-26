using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Fonts;
using HtmlToPdfDotNet.Library.Models.Imaging;

namespace HtmlToPdfDotNet.Library.Models.Layout;

// ─────────────────────────────────────────────────────────────────────────────
// Render primitives
// The layout engine converts the DOM into a flat list of these primitives.
// The PDF renderer consumes them to generate content streams.
// All coordinates are absolute in points, with the origin at the top-left
// corner of the page (Y increases downwards in this internal model;
// the writer flips it when writing to PDF).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Base class for all render primitives.
/// </summary>
public abstract class RenderPrimitive
{
    public int PageIndex { get; set; }
}

/// <summary>
/// Rectangle filled (background) or border.
/// </summary>
public sealed class RectPrimitive : RenderPrimitive
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public CssColor Fill { get; init; }
    public bool HasFill => Fill.A > 0f;
    public float BorderRadius { get; init; } = 0f;
    public CssColor Stroke { get; init; } = CssColor.Transparent;
    public float StrokeWidth { get; init; } = 0f;
}

/// <summary>
/// Border line of one side.
/// </summary>
public sealed class BorderLinePrimitive : RenderPrimitive
{
    public float X1 { get; init; }
    public float Y1 { get; init; }
    public float X2 { get; init; }
    public float Y2 { get; init; }
    public float Width { get; init; }
    public CssColor Color { get; init; }
    public BorderStyle Style { get; init; }
}

/// <summary>
/// Fragment of text at a specific position.
/// </summary>
public sealed class TextPrimitive : RenderPrimitive
{
    /// <summary>
    /// Bottom-left corner of the baseline (PDF coordinates after inversion).
    /// </summary>
    public float X { get; init; }
    public float Y { get; init; }
    public string Text { get; init; } = "";
    public string FontName { get; init; } = "Helvetica";
    public float FontSize { get; init; } = Constants.DefaultFontSize;
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public CssColor Color { get; init; }

    /// <summary>
    /// When non-null, this text primitive uses an embedded TTF/OTF font.
    /// The content-stream builder will encode the text as GID hex strings
    /// and the font writer will emit a Type0/CIDFontType2 object chain.
    /// </summary>
    public EmbeddedFontInfo? EmbeddedFont { get; init; }
}

/// <summary>
/// Rasterized or embedded image.
/// </summary>
public sealed class ImagePrimitive : RenderPrimitive
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    /// <summary>
    /// Data of image resolved (dimensions, bytes, format etc.)
    /// </summary>
    public ImageData? ImageData { get; init; }

    /// <summary>
    /// Alias of XObject in the resources PDF dictionary (/Im1, /Im2, etc.)
    /// </summary>
    public string XObjectAlias { get; init; } = string.Empty;
}

/// <summary>
/// Explicit page break (separation mark between pages).
/// </summary>
public sealed class PageBreakPrimitive : RenderPrimitive { }