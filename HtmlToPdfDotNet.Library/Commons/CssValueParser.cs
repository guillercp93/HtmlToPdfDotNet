using System.Globalization;

namespace HtmlToPdfDotNet.Library.Commons;

/// <summary>
/// Utility class for parsing CSS values.
/// </summary>
public static class CssValueParser
{
    #region Lengths
    /// <summary>
    /// Parser a CSS length value and convert it to PDF points.
    /// Supports: px, pt, em, rem, cm, mm, in and numeric values without unit (treated as px).
    /// Returns <see cref="CssLength.Zero"/> if the value is not recognized.
    /// </summary>
    /// <param name="value">The CSS length value.</param>
    /// <param name="parentFontSize">The font size of the parent node.</param>
    /// <returns>The parsed length.</returns>
    public static CssLength ParseLength(string? value, float parentFontSize = Constants.DefaultFontSize)
    {
        if (string.IsNullOrWhiteSpace(value)) return CssLength.Zero;

        value = value.Trim().ToLowerInvariant();

        if (value == "auto") return CssLength.Auto;
        if (value == "0" || value == "none") return CssLength.Zero;

        // Units with suffix
        (string suffix, float toPt)[] units =
        [
            ("px",   Constants.PointsPerPx),       // 1px = 0.75pt  (96dpi → 72dpi)
            ("pt",   1f),
            ("em",   parentFontSize),
            ("rem",  Constants.DefaultFontSize),   // rem = root font size (12pt por defecto)
            ("cm",   Constants.PointsPerCm),       // 1cm ≈ 28.35pt
            ("mm",   Constants.PointsPerMm),
            ("in",   Constants.PointsPerInch),
        ];

        foreach ((string suffix, float toPt) in units)
        {
            if (value.EndsWith(suffix))
            {
                string raw = value[..^suffix.Length];
                if (TryParseFloat(raw, out float num))
                    return new CssLength(num * toPt);
                return CssLength.Zero;
            }
        }

        // Without unit → treat as px
        if (TryParseFloat(value, out float plain))
            return new CssLength(plain * Constants.PointsPerPx);

        return CssLength.Zero;
    }
    #endregion

    #region Shorthand edges (margin / padding)
    /// <summary>
    /// Parser a shorthand of 1-4 values (top right bottom left).
    /// </summary>
    /// <param name="value">The CSS edges value.</param>
    /// <param name="parentFontSize">The font size of the parent node.</param>
    /// <returns>The parsed edges.</returns>
    public static CssEdges ParseEdges(string? value, float parentFontSize = Constants.DefaultFontSize)
    {
        if (string.IsNullOrWhiteSpace(value)) return CssEdges.Zero;

        string[] parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        CssLength[] lengths = Array.ConvertAll(parts, p => ParseLength(p, parentFontSize));

        return lengths.Length switch
        {
            1 => new CssEdges(lengths[0]),
            2 => new CssEdges(lengths[0], lengths[1]),
            3 => new CssEdges(lengths[0], lengths[1], lengths[2], lengths[1]),
            _ => new CssEdges(lengths[0], lengths[1], lengths[2], lengths[3]),
        };
    }
    #endregion

    #region Colors
    /// <summary>
    /// Dictionary of named colors.
    /// </summary>
    private static readonly Dictionary<string, CssColor> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = CssColor.Black,
        ["white"] = CssColor.White,
        ["transparent"] = CssColor.Transparent,
        ["red"] = CssColor.FromRgb(255, 0, 0),
        ["green"] = CssColor.FromRgb(0, 128, 0),
        ["blue"] = CssColor.FromRgb(0, 0, 255),
        ["yellow"] = CssColor.FromRgb(255, 255, 0),
        ["orange"] = CssColor.FromRgb(255, 165, 0),
        ["gray"] = CssColor.FromRgb(128, 128, 128),
        ["grey"] = CssColor.FromRgb(128, 128, 128),
        ["silver"] = CssColor.FromRgb(192, 192, 192),
        ["navy"] = CssColor.FromRgb(0, 0, 128),
        ["teal"] = CssColor.FromRgb(0, 128, 128),
        ["purple"] = CssColor.FromRgb(128, 0, 128),
        ["maroon"] = CssColor.FromRgb(128, 0, 0),
        ["fuchsia"] = CssColor.FromRgb(255, 0, 255),
        ["aqua"] = CssColor.FromRgb(0, 255, 255),
        ["lime"] = CssColor.FromRgb(0, 255, 0),
        ["olive"] = CssColor.FromRgb(128, 128, 0),
        ["lightgray"] = CssColor.FromRgb(211, 211, 211),
        ["lightgrey"] = CssColor.FromRgb(211, 211, 211),
        ["darkgray"] = CssColor.FromRgb(169, 169, 169),
        ["darkgrey"] = CssColor.FromRgb(169, 169, 169),
        ["whitesmoke"] = CssColor.FromRgb(245, 245, 245),
        ["gainsboro"] = CssColor.FromRgb(220, 220, 220),
        ["cornsilk"] = CssColor.FromRgb(255, 248, 220),
    };

    /// <summary>
    /// Parser a CSS color. Supports: names, #rrggbb, #rgb, #rrggbbaa, rgb(...), rgba(...).
    /// </summary>
    /// <param name="value">The CSS color value.</param>
    /// <returns>The parsed color.</returns>
    public static CssColor ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return CssColor.Black;

        value = value.Trim();

        if (NamedColors.TryGetValue(value, out CssColor named)) return named;

        // Hex
        if (value.StartsWith('#'))
        {
            string hex = value[1..];
            return hex.Length switch
            {
                3 => CssColor.FromRgb(
                    HexByte(hex[0], hex[0]),
                    HexByte(hex[1], hex[1]),
                    HexByte(hex[2], hex[2])),
                6 => CssColor.FromRgb(
                    HexByte(hex[0], hex[1]),
                    HexByte(hex[2], hex[3]),
                    HexByte(hex[4], hex[5])),
                8 => CssColor.FromRgb(
                    HexByte(hex[0], hex[1]),
                    HexByte(hex[2], hex[3]),
                    HexByte(hex[4], hex[5]),
                    HexByte(hex[6], hex[7])),
                _ => CssColor.Black,
            };
        }

        // rgb(...) / rgba(...)
        string lower = value.ToLowerInvariant();
        if (lower.StartsWith("rgb"))
        {
            int start = lower.IndexOf('(');
            int end = lower.IndexOf(')');
            if (start >= 0 && end > start)
            {
                string[] parts = lower[(start + 1)..end].Split(',');
                if (parts.Length >= 3 &&
                    TryParseFloat(parts[0].Trim(), out float r) &&
                    TryParseFloat(parts[1].Trim(), out float g) &&
                    TryParseFloat(parts[2].Trim(), out float b))
                {
                    float a = 1f;
                    if (parts.Length == 4) TryParseFloat(parts[3].Trim(), out a);
                    // rgb() usa 0-255, rgba() usa 0-255 + alpha 0-1
                    return new CssColor(r / 255f, g / 255f, b / 255f, a);
                }
            }
        }

        return CssColor.Black;
    }
    #endregion

    #region Font size
    /// <summary>
    /// Dictionary of font size keywords.
    /// </summary>
    private static readonly Dictionary<string, float> FontSizeKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["xx-small"] = 6f,
        ["x-small"] = 7.5f,
        ["small"] = 9f,
        ["medium"] = Constants.DefaultFontSize,
        ["large"] = 13.5f,
        ["x-large"] = 18f,
        ["xx-large"] = 24f,
        ["smaller"] = 0f,   // relative, resolved in StyleResolver
        ["larger"] = 0f,
    };

    /// <summary>
    /// Parser font-size. If it's a keyword, return its equivalent in points.
    /// If it's a value with unit, delegate to ParseLength.
    /// </summary>
    /// <param name="value">The CSS font-size value.</param>
    /// <param name="parentFontSize">The font size of the parent node.</param>
    /// <returns>The parsed font-size.</returns>
    public static float ParseFontSize(string? value,
                                      float parentFontSize = Constants.DefaultFontSize)
    {
        if (string.IsNullOrWhiteSpace(value)) return parentFontSize;

        value = value.Trim();

        if (FontSizeKeywords.TryGetValue(value, out float keyword))
        {
            if (keyword > 0f) return keyword;
            // "smaller" / "larger"
            return value.Equals("smaller", StringComparison.OrdinalIgnoreCase)
                ? parentFontSize * 0.83f
                : parentFontSize * 1.2f;
        }

        // Percentage: "120%" → parentFontSize * 1.2
        if (value.EndsWith('%') && TryParseFloat(value[..^1], out float pct))
            return parentFontSize * pct / 100f;

        CssLength len = ParseLength(value, parentFontSize);
        return len.Points > 0f ? len.Points : parentFontSize;
    }
    #endregion

    #region Border
    /// <summary>
    /// Parser a shorthand of border: "1px solid #333" o "2pt dashed red".
    /// </summary>
    /// <param name="value">The CSS border value.</param>
    /// <param name="parentFontSize">The font size of the parent node.</param>
    /// <returns>The parsed border.</returns>
    public static CssBorderSide ParseBorderSide(string? value,
                                                float parentFontSize = Constants.DefaultFontSize)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "none")
            return CssBorderSide.None;

        // Smart split: ignore spaces inside parentheses (for rgb/rgba)
        List<string> parts = new();
        int bracketLevel = 0;
        int lastPos = 0;
        string vTrim = value.Trim();
        for (int i = 0; i < vTrim.Length; i++)
        {
            if (vTrim[i] == '(') bracketLevel++;
            else if (vTrim[i] == ')') bracketLevel--;
            else if (vTrim[i] == ' ' && bracketLevel == 0)
            {
                string p = vTrim[lastPos..i].Trim();
                if (!string.IsNullOrEmpty(p)) parts.Add(p);
                lastPos = i + 1;
            }
        }
        string lastPart = vTrim[lastPos..].Trim();
        if (!string.IsNullOrEmpty(lastPart)) parts.Add(lastPart);

        CssLength width = new(1f);           // default 1pt
        BorderStyle style = BorderStyle.Solid;
        CssColor color = CssColor.Black;

        foreach (string part in parts)
        {
            string p = part.ToLowerInvariant();

            if (p == "none") { style = BorderStyle.None; continue; }
            if (p == "solid") { style = BorderStyle.Solid; continue; }
            if (p == "dashed") { style = BorderStyle.Dashed; continue; }
            if (p == "dotted") { style = BorderStyle.Dotted; continue; }
            if (p == "double") { style = BorderStyle.Double; continue; }

            if (p.StartsWith('#') || p.StartsWith("rgb") || NamedColors.ContainsKey(p))
            {
                color = ParseColor(p);
                continue;
            }

            CssLength len = ParseLength(p, parentFontSize);
            if (!len.IsZero) width = len;
        }

        return new CssBorderSide(width, style, color);
    }
    #endregion

    #region Display / enums
    /// <summary>
    /// Parse a CSS display value.
    /// </summary>
    /// <param name="value">The CSS display value.</param>
    /// <returns>The parsed display.</returns>
    public static DisplayType ParseDisplay(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "block" => DisplayType.Block,
        "inline" => DisplayType.Inline,
        "inline-block" => DisplayType.InlineBlock,
        "none" => DisplayType.None,
        "flex" => DisplayType.Flex,
        "table" => DisplayType.Table,
        "list-item" => DisplayType.ListItem,
        _ => DisplayType.Block,
    };

    /// <summary>
    /// Parse a CSS list-style-type value.
    /// </summary>
    /// <param name="value">The CSS list-style-type value.</param>
    /// <returns>The parsed list-style-type.</returns>
    public static ListStyleType ParseListStyleType(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "disc" => ListStyleType.Disc,
        "circle" => ListStyleType.Circle,
        "square" => ListStyleType.Square,
        "decimal" => ListStyleType.Decimal,
        "lower-alpha" => ListStyleType.LowerAlpha,
        "upper-alpha" => ListStyleType.UpperAlpha,
        "lower-roman" => ListStyleType.LowerRoman,
        "upper-roman" => ListStyleType.UpperRoman,
        "none" => ListStyleType.None,
        _ => ListStyleType.Disc,
    };

    /// <summary>
    /// Parse a CSS text-align value.
    /// </summary>
    /// <param name="value">The CSS text-align value.</param>
    /// <returns>The parsed text-align.</returns>
    public static TextAlign ParseTextAlign(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "left" => TextAlign.Left,
        "right" => TextAlign.Right,
        "center" => TextAlign.Center,
        "justify" => TextAlign.Justify,
        _ => TextAlign.Left,
    };

    /// <summary>
    /// Parse a CSS text-transform value.
    /// </summary>
    /// <param name="value">The CSS text-transform value.</param>
    /// <returns>The parsed text-transform.</returns>
    public static TextTransForm ParseTextTransform(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "capitalize" => TextTransForm.Capitalize,
        "uppercase" => TextTransForm.Uppercase,
        "lowercase" => TextTransForm.Lowercase,
        "full-width" => TextTransForm.FullWidth,
        _ => TextTransForm.None,
    };

    /// <summary>
    /// Parse a CSS text-decoration value.
    /// </summary>
    /// <param name="value">The CSS text-decoration value.</param>
    /// <returns>The parsed text-decoration.</returns>
    public static TextDecoration ParseTextDecoration(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "underline" => TextDecoration.Underline,
        "overline" => TextDecoration.Overline,
        "line-through" => TextDecoration.LineThrough,
        _ => TextDecoration.None,
    };

    /// <summary>
    /// Parse a CSS font-weight value.
    /// </summary>
    /// <param name="value">The CSS font-weight value.</param>
    /// <returns>The parsed font-weight.</returns>
    public static FontWeight ParseFontWeight(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return FontWeight.Normal;
        string v = value.Trim().ToLowerInvariant();
        if (v == "bold" || v == "bolder") return FontWeight.Bold;
        if (int.TryParse(v, out int n) && n >= 600) return FontWeight.Bold;
        return FontWeight.Normal;
    }

    /// <summary>
    /// Parse a CSS font-style value.
    /// </summary>
    /// <param name="value">The CSS font-style value.</param>
    /// <returns>The parsed font-style.</returns>
    public static FontStyle ParseFontStyle(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "italic" => FontStyle.Italic,
        "oblique" => FontStyle.Oblique,
        _ => FontStyle.Normal,
    };
    #endregion

    #region Helpers
    /// <summary>
    /// Tries to parse a float from a string.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">The parsed float.</param>
    /// <returns>True if the string was parsed successfully, false otherwise.</returns>
    private static bool TryParseFloat(string s, out float result)
        => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    /// <summary>
    /// Converts a pair of hexadecimal characters to a byte.
    /// </summary>
    /// <param name="hi">The high nibble (first character).</param>
    /// <param name="lo">The low nibble (second character).</param>
    /// <returns>The byte value.</returns>
    private static byte HexByte(char hi, char lo)
        => Convert.ToByte($"{hi}{lo}", 16);
    #endregion
}