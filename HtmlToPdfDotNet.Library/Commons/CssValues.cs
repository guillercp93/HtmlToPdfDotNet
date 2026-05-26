using System.Globalization;

namespace HtmlToPdfDotNet.Library.Commons;

/// <summary>
/// Value of display CSS property
/// </summary>
public enum DisplayType
{
    Block,
    Inline,
    InlineBlock,
    None,
    Flex,
    Table
}

/// <summary>
/// Value of text-align CSS property
/// </summary>
public enum TextAlign
{
    Left,
    Right,
    Center,
    Justify
}

/// <summary>
/// Value of text-transform CSS property
/// </summary>
public enum TextTransForm
{
    Capitalize,
    Uppercase,
    Lowercase,
    None,
    FullWidth
}

/// <summary>
/// Value of text-decoration CSS property
/// </summary>
public enum TextDecoration
{
    None,
    Underline,
    Overline,
    LineThrough
}

/// <summary>
/// Value of font-weight CSS property
/// </summary>
public enum FontWeight
{
    Normal = 400,
    Bold = 700
}

/// <summary>
/// Value of font-style CSS property
/// </summary>
/// <see cref="https://developer.mozilla.org/en-US/docs/Web/CSS/font-style"/>
public enum FontStyle
{
    Normal,
    Italic,
    Oblique
}

/// <summary>
/// Represents a value of CSS length already resolved to PDF points (1pt = 1/72 inch).
/// All values are used as points for simplicity.
/// </summary>
public readonly struct CssLength
{
    public static readonly CssLength Zero = new(0f);
    public static readonly CssLength Auto = new(0f, isAuto: true);

    public float Points { get; }
    public bool IsAuto { get; }
    public bool IsZero => !IsAuto && Points == 0f;

    public CssLength(float points, bool isAuto = false)
    {
        Points = points;
        IsAuto = isAuto;
    }

    public override string ToString() => IsAuto ? "auto" : $"{Points:F2}pt";
}

/// <summary>
/// Color RGBA with components in range [0, 1]
/// </summary>
public readonly struct CssColor
{
    public static readonly CssColor Black = new(0f, 0f, 0f);
    public static readonly CssColor White = new(1f, 1f, 1f);
    public static readonly CssColor Transparent = new(0f, 0f, 0f, 0f);

    public float R { get; }
    public float G { get; }
    public float B { get; }
    public float A { get; }

    public CssColor(float r, float g, float b, float a = 1f)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static CssColor FromRgb(byte r, byte g, byte b, byte a = 255)
    {
        return new(r / 255f, g / 255f, b / 255f, a / 255f);
    }

    public static CssColor FromHex(string hex)
    {
        if (hex.StartsWith("#"))
        {
            hex = hex.Substring(1);
        }

        if (hex.Length == 3)
        {
            return new(byte.Parse(hex[0].ToString(), NumberStyles.HexNumber) / 255f,
                byte.Parse(hex[1].ToString(), NumberStyles.HexNumber) / 255f,
                byte.Parse(hex[2].ToString(), NumberStyles.HexNumber) / 255f);
        }

        if (hex.Length == 6)
        {
            return new(byte.Parse(hex[0..2], NumberStyles.HexNumber) / 255f,
                byte.Parse(hex[2..4], NumberStyles.HexNumber) / 255f,
                byte.Parse(hex[4..6], NumberStyles.HexNumber) / 255f);
        }

        throw new FormatException("Invalid hex color format");
    }

    public override string ToString() => $"rgba({R:F2},{G:F2},{B:F2},{A:F2})";
}

/// <summary>
/// Represents the four sides of margin, padding, border-width.
/// </summary>
public readonly struct CssEdges
{
    public static readonly CssEdges Zero = new(CssLength.Zero, CssLength.Zero, CssLength.Zero, CssLength.Zero);

    public CssLength Top { get; }
    public CssLength Right { get; }
    public CssLength Bottom { get; }
    public CssLength Left { get; }

    public CssLength Horizontal => new(Left.Points + Right.Points);
    public CssLength Vertical => new(Top.Points + Bottom.Points);

    public CssEdges(CssLength top, CssLength right, CssLength bottom, CssLength left)
    {
        Top = top;
        Right = right;
        Bottom = bottom;
        Left = left;
    }

    /// <summary>
    /// Creates a new CssEdges with all edges set to the same value.
    /// </summary>
    /// <param name="all">A CssLength value to set all edges to.</param>
    public CssEdges(CssLength all) : this(all, all, all, all)
    {
    }

    /// <summary>
    /// Creates a new CssEdges with top and bottom set to vertical, and left and right set to horizontal.
    /// </summary>
    /// <param name="vertical">A CssLength value to set top and bottom edges to.</param>
    /// <param name="horizontal">A CssLength value to set left and right edges to.</param>
    public CssEdges(CssLength vertical, CssLength horizontal) : this(vertical, horizontal, vertical, horizontal)
    {
    }
}

/// <summary>
/// Represents a CSS border style.
/// </summary>
public enum BorderStyle
{
    None,
    Solid,
    Dashed,
    Dotted,
    Double,
    Hidden,
    Groove,
    Ridge,
    Inset,
    Outset
}

/// <summary>
/// Represents a single side of a CSS border with color, thickness and style.
/// </summary>
public readonly struct CssBorderSide
{
    public static readonly CssBorderSide None = new(CssLength.Zero, BorderStyle.None, CssColor.Black);

    public CssLength Width { get; }
    public BorderStyle Style { get; }
    public CssColor Color { get; }

    public bool IsVisible => Style != BorderStyle.None && Width.Points > 0f;

    public CssBorderSide(CssLength width, BorderStyle style, CssColor color)
    {
        Width = width;
        Style = style;
        Color = color;
    }
}