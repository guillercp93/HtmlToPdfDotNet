using System.Text;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Fonts;

namespace HtmlToPdfDotNet.Library.Models.Writer;

/// <summary>
/// Builds the content stream of a PDF page.
///
/// PDF operators used:
///   q / Q          → save/restore graphic state
///   rg / RG        → fill/stroke color (RGB)
///   re f           → fill rectangle
///   w              → line width
///   m l S          → moveto, lineto, stroke  (for borders and lines)
///   BT … ET        → text block
///   Tf             → select font and size
///   Td             → move text cursor
///   Tj             → show text (Latin-1 string for Type1 fonts)
///   TJ via hex     → show GID hex string for Type0/CIDFont embedded fonts
///
/// Text encoding:
///   Standard (Type1) fonts  → Latin-1 string literal  (text) Tj
///   Embedded (Type0) fonts  → 2-byte-per-char GID hex  &lt;GGGGGGGG…&gt; Tj
///
/// Coordinates: PDF has origin in the bottom-left corner of the page.
/// The layout engine works with Y from top, so we apply:
///   pdfY = pageHeight - layoutY
/// for each primitive before emitting it.
/// </summary>
public sealed class ContentStreamBuilder
{
    private readonly StringBuilder _sb = new();
    private readonly float _pageH;

    /// <summary>
    /// Alias map provided by <see cref="FontResourceBuilder.EmbeddedAliases"/>.
    /// Maps each EmbeddedFontInfo reference to its /FEn alias in the page resources.
    /// Null when no embedded fonts are used.
    /// </summary>
    private readonly IReadOnlyDictionary<EmbeddedFontInfo, string>? _embeddedAliases;

    // Active graphic state (cached to suppress redundant operators)
    private string _currentFont = "";
    private float _currentFontSize = 0f;

    // Active text color
    private CssColor _currentTextColor = new(-1, -1, -1); // invalid → force emit

    // Active fill color
    private CssColor _currentFillColor = new(-1, -1, -1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentStreamBuilder"/> class.
    /// </summary>
    /// <param name="pageHeight">The height of the page for Y-axis inversion.</param>
    /// <param name="embeddedAliases">
    ///   Optional map of embedded-font aliases (from <see cref="FontResourceBuilder"/>).
    ///   Pass <c>null</c> when only standard fonts are used.
    /// </param>
    public ContentStreamBuilder(float pageHeight,
                                IReadOnlyDictionary<EmbeddedFontInfo, string>? embeddedAliases = null)
    {
        _pageH = pageHeight;
        _embeddedAliases = embeddedAliases;
    }

    #region Drawing primitives
    /// <summary>Emits PDF operators to draw a filled rectangle.</summary>
    /// <param name="r">The rectangle primitive to draw, containing position, size, and fill color information.</param>
    public void DrawRect(RectPrimitive r)
    {
        if (!r.HasFill) return;

        SetFillColor(r.Fill);

        // PDF re: x y width height re  →  then f to fill
        // Y inverted: bottom-left corner of the rect in PDF
        float pdfY = _pageH - r.Y - r.Height;
        _sb.AppendLine("q");
        _sb.AppendLine($"{Helpers.F(r.X)} {Helpers.F(pdfY)} {Helpers.F(r.Width)} {Helpers.F(r.Height)} re f");
        _sb.AppendLine("Q");
    }

    /// <summary>
    /// Emits PDF operators to draw a border line.
    /// </summary>
    /// <param name="b">The border line primitive containing coordinates, width, color, and style.</param>
    public void DrawBorderLine(BorderLinePrimitive b)
    {
        float x1 = b.X1;
        float y1 = _pageH - b.Y1;
        float x2 = b.X2;
        float y2 = _pageH - b.Y2;

        _sb.AppendLine("q");
        SetStrokeColor(b.Color);
        _sb.AppendLine($"{Helpers.F(b.Width)} w");

        if (b.Style == BorderStyle.Dashed)
            _sb.AppendLine("[3 2] 0 d");   // dash pattern
        else if (b.Style == BorderStyle.Dotted)
            _sb.AppendLine("[1 2] 0 d");

        _sb.AppendLine($"{Helpers.F(x1)} {Helpers.F(y1)} m {Helpers.F(x2)} {Helpers.F(y2)} l S");
        _sb.AppendLine("Q");
    }

    /// <summary>
    /// Emits PDF operators to draw a text fragment.
    /// Automatically switches between Type1 (Latin-1) and Type0 (GID-hex) encoding.
    /// </summary>
    /// <param name="t">The text primitive containing position, text, font, and color.</param>
    public void DrawText(TextPrimitive t)
    {
        if (string.IsNullOrEmpty(t.Text)) return;

        float pdfY = _pageH - t.Y;

        _sb.AppendLine("BT");
        SetFillColor(t.Color);

        if (t.EmbeddedFont != null
            && _embeddedAliases != null
            && _embeddedAliases.TryGetValue(t.EmbeddedFont, out var embAlias))
        {
            // ── Embedded Type0 path ────────────────────────────────────────
            SetFont(embAlias, t.FontSize);
            _sb.AppendLine($"{Helpers.F(t.X)} {Helpers.F(pdfY)} Td");
            _sb.AppendLine($"<{BuildGidHexString(t.Text, t.EmbeddedFont)}> Tj");
        }
        else
        {
            // ── Standard Type1 path ────────────────────────────────────────
            SetFont(FontAlias(t.FontName), t.FontSize);
            _sb.AppendLine($"{Helpers.F(t.X)} {Helpers.F(pdfY)} Td");
            _sb.AppendLine($"({EscapePdfString(t.Text)}) Tj");
        }

        _sb.AppendLine("ET");
    }

    #endregion
    #region Graphic state helpers

    /// <summary>
    /// Sets the fill color in the graphic state if it has changed.
    /// </summary>
    /// <param name="c">The fill color to set.</param>
    private void SetFillColor(CssColor c)
    {
        if (ColorsEqual(c, _currentFillColor)) return;
        _currentFillColor = c;
        _sb.AppendLine($"{Helpers.F(c.R)} {Helpers.F(c.G)} {Helpers.F(c.B)} rg");
    }

    /// <summary>
    /// Sets the stroke color in the graphic state.
    /// </summary>
    /// <param name="c">The stroke color to set.</param>
    private void SetStrokeColor(CssColor c)
        => _sb.AppendLine($"{Helpers.F(c.R)} {Helpers.F(c.G)} {Helpers.F(c.B)} RG");

    /// <summary>
    /// Sets the current font and size in the graphic state if they have changed.
    /// </summary>
    /// <param name="name">The name of the font to set.</param>
    /// <param name="size">The size of the font to set.</param>
    private void SetFont(string alias, float size)
    {
        if (alias == _currentFont && Math.Abs(size - _currentFontSize) < 0.01f) return;
        _currentFont = alias;
        _currentFontSize = size;
        _sb.AppendLine($"/{alias} {Helpers.F(size)} Tf");
    }
    #endregion

    #region Build

    /// <summary>
    /// Builds the final content stream, optionally compressing it with FlateDecode.
    /// </summary>
    /// <param name="compress">True to compress using ZLib; otherwise, false.</param>
    /// <returns>A byte array representing the content stream.</returns>
    public byte[] Build(bool compress = true)
    {
        byte[] raw = Encoding.Latin1.GetBytes(_sb.ToString());
        return compress ? Helpers.Deflate(raw) : raw;
    }

    /// <summary>
    /// Gets the raw, uncompressed content stream as a string.
    /// </summary>
    public string RawContent => _sb.ToString();

    #endregion
    #region Utils

    /// <summary>
    /// Builds the GID hex string for an embedded (Type0/Identity-H) font.
    /// Each Unicode character is mapped to its 2-byte GID and encoded as 4 hex digits.
    /// Result: "GGGGGGGG…" (no spaces, no angle brackets — caller adds those).
    /// </summary>
    private static string BuildGidHexString(string text, EmbeddedFontInfo font)
    {
        StringBuilder sb = new(text.Length * 4);
        foreach (char ch in text)
        {
            int gid = font.GetGlyphId(ch);
            sb.Append(gid.ToString("X4"));
        }
        return sb.ToString();
    }

    /// <summary>Escapes special characters in a PDF Latin-1 string literal.</summary>
    private static string EscapePdfString(string s)
    {
        StringBuilder sb = new(s.Length);
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\\': sb.Append("\\\\"); break;
                default:
                    sb.Append(ch > 255 ? '?' : ch);
                    break;
            }
        }
        return sb.ToString();
    }

    // Internal overload for direct calls with interpolation
    // (floats are formatted with InvariantCulture via FormattableString)
    private static bool ColorsEqual(CssColor a, CssColor b)
        => Math.Abs(a.R - b.R) < 0.001f
        && Math.Abs(a.G - b.G) < 0.001f
        && Math.Abs(a.B - b.B) < 0.001f;

    /// <summary>
    /// Maps a PDF standard font name to its resource alias (F1..F12).
    /// </summary>
    /// <param name="pdfFontName">The standard PDF name of the font.</param>
    /// <returns>The font alias used in the page resources dictionary.</returns>
    internal static string FontAlias(string pdfFontName) => pdfFontName switch
    {
        "Helvetica" => "F1",
        "Helvetica-Bold" => "F2",
        "Helvetica-Oblique" => "F3",
        "Helvetica-BoldOblique" => "F4",
        "Times-Roman" => "F5",
        "Times-Bold" => "F6",
        "Times-Italic" => "F7",
        "Times-BoldItalic" => "F8",
        "Courier" => "F9",
        "Courier-Bold" => "F10",
        "Courier-Oblique" => "F11",
        "Courier-BoldOblique" => "F12",
        _ => "F1",
    };
    #endregion
}
