using System.IO.Compression;
using System.Text;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;

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
///   Tj             → show text
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

    // Active font in the stream (avoid redundant Tf)
    private string _currentFont = "";
    private float _currentFontSize = 0f;

    // Active text color
    private CssColor _currentTextColor = new(-1, -1, -1); // invalid → force emit

    // Active fill color
    private CssColor _currentFillColor = new(-1, -1, -1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentStreamBuilder"/> class.
    /// </summary>
    /// <param name="pageHeight">The height of the page, used for coordinate inversion (Y-axis).</param>
    public ContentStreamBuilder(float pageHeight) => _pageH = pageHeight;

    #region Drawing primitives

    /// <summary>
    /// Emits PDF operators to draw a filled rectangle.
    /// </summary>
    /// <param name="r">The rectangle primitive containing position, size, and fill color.</param>
    public void DrawRect(RectPrimitive r)
    {
        if (!r.HasFill) return;

        SetFillColor(r.Fill);

        // PDF re: x y width height re  →  then f to fill
        // Y inverted: bottom-left corner of the rect in PDF
        float pdfY = _pageH - r.Y - r.Height;
        _sb.AppendLine($"q");
        _sb.AppendLine($"{Helpers.F(r.X)} {Helpers.F(pdfY)} {Helpers.F(r.Width)} {Helpers.F(r.Height)} re f");
        _sb.AppendLine($"Q");
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
    /// </summary>
    /// <param name="t">The text primitive containing position, text, font, and color.</param>
    public void DrawText(TextPrimitive t)
    {
        if (string.IsNullOrEmpty(t.Text)) return;

        float pdfY = _pageH - t.Y;

        _sb.AppendLine("BT");
        SetFont(t.FontName, t.FontSize);
        SetFillColor(t.Color);
        _sb.AppendLine($"{Helpers.F(t.X)} {Helpers.F(pdfY)} Td");
        _sb.AppendLine($"({EscapePdfString(t.Text)}) Tj");
        _sb.AppendLine("ET");

        // Reset text cursor (each BT starts fresh)
    }

    #endregion
    #region Graphic state helpers

    /// <summary>
    /// Sets the fill color in the graphic state if it has changed.
    /// </summary>
    private void SetFillColor(CssColor c)
    {
        if (ColorsEqual(c, _currentFillColor)) return;
        _currentFillColor = c;
        _sb.AppendLine($"{Helpers.F(c.R)} {Helpers.F(c.G)} {Helpers.F(c.B)} rg");
    }

    /// <summary>
    /// Sets the stroke color in the graphic state.
    /// </summary>
    private void SetStrokeColor(CssColor c)
        => _sb.AppendLine($"{Helpers.F(c.R)} {Helpers.F(c.G)} {Helpers.F(c.B)} RG");

    /// <summary>
    /// Sets the current font and size in the graphic state if they have changed.
    /// </summary>
    private void SetFont(string name, float size)
    {
        if (name == _currentFont && Math.Abs(size - _currentFontSize) < 0.01f) return;
        _currentFont = name;
        _currentFontSize = size;
        // /F1 is the font alias in the page resources dictionary
        // We use the alias based on the font name
        var alias = FontAlias(name);
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
        var raw = Encoding.Latin1.GetBytes(_sb.ToString());
        if (!compress) return raw;
        return Deflate(raw);
    }

    /// <summary>
    /// Gets the raw, uncompressed content stream as a string.
    /// </summary>
    public string RawContent => _sb.ToString();
    #endregion

    #region Utils

    /// <summary>Escapes special characters in a PDF string.</summary>
    private static string EscapePdfString(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\\': sb.Append("\\\\"); break;
                default:
                    // Characters outside basic Latin-1 → replace with '?'
                    if (ch > 255)
                        sb.Append('?');
                    else
                        sb.Append(ch);
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
    /// Maps a PDF standard font name to its internal resource alias (F1..F12).
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

    /// <summary>
    /// Compresses the input data using the Deflate algorithm with a ZLib header.
    /// </summary>
    private static byte[] Deflate(byte[] data)
    {
        using var ms = new MemoryStream();
        // PDF FlateDecode uses zlib: 2 bytes header + deflate + 4 bytes Adler32
        // .NET DeflateStream does not add zlib header → we use ZLibStream (.NET 6+)
        using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(data, 0, data.Length);
        return ms.ToArray();
    }
    #endregion
}
