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

    /// <summary>
    /// Returns a full copy of this primitive moved by (<paramref name="dx"/>, <paramref name="dy"/>)
    /// onto <paramref name="pageIndex"/>, leaving this instance untouched. Every other property
    /// (including ones added later) is preserved because the copy is memberwise.
    /// </summary>
    internal RenderPrimitive CopyRelocated(float dx, float dy, int pageIndex)
    {
        var copy = (RenderPrimitive)MemberwiseClone();
        copy.PageIndex = pageIndex;
        copy.Translate(dx, dy);
        return copy;
    }

    /// <summary>
    /// Shifts the coordinates of this primitive. Primitives without coordinates do nothing.
    /// </summary>
    private protected virtual void Translate(float dx, float dy) { }
}

/// <summary>
/// Rectangle filled (background) or border.
/// </summary>
public sealed class RectPrimitive : RenderPrimitive
{
    public float X { get => _x; init => _x = value; }
    public float Y { get => _y; init => _y = value; }
    public float Width { get; init; }
    public float Height { get; init; }
    public CssColor Fill { get; init; }
    public bool HasFill => Fill.A > 0f;
    public float BorderRadius { get; init; } = 0f;
    public CssColor Stroke { get; init; } = CssColor.Transparent;
    public float StrokeWidth { get; init; } = 0f;

    private float _x;
    private float _y;

    private protected override void Translate(float dx, float dy)
    {
        _x += dx;
        _y += dy;
    }
}

/// <summary>
/// Border line of one side.
/// </summary>
public sealed class BorderLinePrimitive : RenderPrimitive
{
    public float X1 { get => _x1; init => _x1 = value; }
    public float Y1 { get => _y1; init => _y1 = value; }
    public float X2 { get => _x2; init => _x2 = value; }
    public float Y2 { get => _y2; init => _y2 = value; }
    public float Width { get; init; }
    public CssColor Color { get; init; }
    public BorderStyle Style { get; init; }

    private float _x1;
    private float _y1;
    private float _x2;
    private float _y2;

    private protected override void Translate(float dx, float dy)
    {
        _x1 += dx;
        _y1 += dy;
        _x2 += dx;
        _y2 += dy;
    }
}

/// <summary>
/// Fragment of text at a specific position.
/// </summary>
public sealed class TextPrimitive : RenderPrimitive
{
    /// <summary>
    /// Bottom-left corner of the baseline (PDF coordinates after inversion).
    /// </summary>
    public float X { get => _x; init => _x = value; }
    public float Y { get => _y; init => _y = value; }
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

    private float _x;
    private float _y;

    private protected override void Translate(float dx, float dy)
    {
        _x += dx;
        _y += dy;
    }
}

/// <summary>
/// Rasterized or embedded image.
/// </summary>
public sealed class ImagePrimitive : RenderPrimitive
{
    public float X { get => _x; init => _x = value; }
    public float Y { get => _y; init => _y = value; }
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

    private float _x;
    private float _y;

    private protected override void Translate(float dx, float dy)
    {
        _x += dx;
        _y += dy;
    }
}

/// <summary>
/// Explicit page break (separation mark between pages).
/// </summary>
public sealed class PageBreakPrimitive : RenderPrimitive { }

/// <summary>
/// A link annotation that makes a rectangular region of a page clickable,
/// navigating to the specified URI when activated.
/// </summary>
public sealed class LinkAnnotationPrimitive : RenderPrimitive
{
    /// <summary>Left edge of the clickable region (top-left origin).</summary>
    public float X { get => _x; init => _x = value; }

    /// <summary>Top edge of the clickable region (top-left origin).</summary>
    public float Y { get => _y; init => _y = value; }

    /// <summary>Width of the clickable region.</summary>
    public float Width { get; init; }

    /// <summary>Height of the clickable region.</summary>
    public float Height { get; init; }

    /// <summary>The URI that the link navigates to.</summary>
    public string Uri { get; init; } = "";

    private float _x;
    private float _y;

    private protected override void Translate(float dx, float dy)
    {
        _x += dx;
        _y += dy;
    }
}