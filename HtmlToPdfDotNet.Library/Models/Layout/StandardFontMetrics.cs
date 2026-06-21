namespace HtmlToPdfDotNet.Library.Models.Layout;

/// <summary>
/// Approximate metrics for the 14 standard PDF fonts (Type1).
/// These fonts are guaranteed to be available in all PDF viewers without embedding.
///
/// Glyph widths are normalized to a base unit of 1000 (AFM standard).
/// To obtain the actual width: (glyphWidth / 1000f) * fontSize
///
/// Data source: Adobe Font Metrics (AFM) files, public domain.
/// </summary>
public static class StandardFontMetrics
{
    /// <summary>
    /// Returns the PDF font name given the family and style.
    /// </summary>
    /// <param name="family">The font family name.</param>
    /// <param name="bold">Whether the font should be bold.</param>
    /// <param name="italic">Whether the font should be italic.</param>
    /// <returns>The corresponding PDF font name.</returns>
    public static string Resolve(string family, bool bold, bool italic)
    {
        string f = family.ToLowerInvariant().Trim();

        return f switch
        {
            "helvetica" or "arial" or "sans-serif" => (bold, italic) switch
            {
                (true, true) => "Helvetica-BoldOblique",
                (true, false) => "Helvetica-Bold",
                (false, true) => "Helvetica-Oblique",
                _ => "Helvetica",
            },
            "times-roman" or "times" or "times new roman" or "serif" => (bold, italic) switch
            {
                (true, true) => "Times-BoldItalic",
                (true, false) => "Times-Bold",
                (false, true) => "Times-Italic",
                _ => "Times-Roman",
            },
            "courier" or "courier new" or "monospace" => (bold, italic) switch
            {
                (true, true) => "Courier-BoldOblique",
                (true, false) => "Courier-Bold",
                (false, true) => "Courier-Oblique",
                _ => "Courier",
            },
            _ => bold ? "Helvetica-Bold" : "Helvetica",
        };
    }

    /// <summary>
    /// Returns the width in points of the string <paramref name="text"/>
    /// with the specified font and size.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="pdfFontName">The PDF font name.</param>
    /// <param name="fontSize">The font size.</param>
    /// <returns>The width of the text in points.</returns>
    public static float MeasureWidth(string text, string pdfFontName, float fontSize)
    {
        if (string.IsNullOrEmpty(text)) return 0f;

        Dictionary<char, int> widths = GetWidthTable(pdfFontName);
        float total = 0f;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];

            // Skip surrogate pairs (emoji etc.) — count as space width for standard fonts
            if (char.IsHighSurrogate(ch))
            {
                if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    i++;
                total += widths.GetValueOrDefault(' ', 278);
                continue;
            }

            if (widths.TryGetValue(ch, out int w))
            {
                total += w;
            }
            else
            {
                total += widths.GetValueOrDefault(' ', 278);
            }
        }

        return total / 1000f * fontSize;
    }

    /// <summary>Cap-height (capital letter line) in points.</summary>
    /// <param name="pdfFontName">The PDF font name.</param>
    /// <param name="fontSize">The font size.</param>
    /// <returns>The cap-height in points.</returns>
    public static float CapHeight(string pdfFontName, float fontSize)
        => IsMonospace(pdfFontName) ? fontSize * 0.562f : fontSize * 0.718f;

    /// <summary>Descender in points (negative value).</summary>
    /// <param name="pdfFontName">The PDF font name.</param>
    /// <param name="fontSize">The font size.</param>
    /// <returns>The descender in points.</returns>
    public static float Descender(string pdfFontName, float fontSize)
        => fontSize * -0.207f;

    /// <summary>
    /// Determines whether the specified font is monospace.
    /// </summary>
    /// <param name="name">The font name.</param>
    /// <returns>True if the font is monospace, false otherwise.</returns>
    private static bool IsMonospace(string name)
        => name.StartsWith("Courier", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the width table for the specified font.
    /// </summary>
    /// <param name="fontName">The font name.</param>
    /// <returns>The width table for the font.</returns>
    private static Dictionary<char, int> GetWidthTable(string fontName)
    {
        // Normalize to family group
        if (fontName.StartsWith("Helvetica", StringComparison.OrdinalIgnoreCase))
        {
            bool bold = fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase);
            return bold ? HelveticaBoldWidths : HelveticaWidths;
        }
        if (fontName.StartsWith("Times", StringComparison.OrdinalIgnoreCase))
        {
            bool bold = fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase);
            bool italic = fontName.Contains("Italic", StringComparison.OrdinalIgnoreCase);
            if (bold && italic) return TimesRomanBoldItalicWidths;
            if (bold) return TimesRomanBoldWidths;
            if (italic) return TimesRomanItalicWidths;
            return TimesRomanWidths;
        }
        if (fontName.StartsWith("Courier", StringComparison.OrdinalIgnoreCase))
            return CourierWidths; // monospace: todos 600

        return HelveticaWidths;
    }

    #region Helvetica widths types
    // Helvetica (Regular) — AFM widths
    private static readonly Dictionary<char, int> HelveticaWidths = new()
    {
        [' '] = 278,
        ['!'] = 278,
        ['"'] = 355,
        ['#'] = 556,
        ['$'] = 556,
        ['%'] = 889,
        ['&'] = 667,
        ['\''] = 222,
        ['('] = 333,
        [')'] = 333,
        ['*'] = 389,
        ['+'] = 584,
        [','] = 278,
        ['-'] = 333,
        ['.'] = 278,
        ['/'] = 278,
        ['0'] = 556,
        ['1'] = 556,
        ['2'] = 556,
        ['3'] = 556,
        ['4'] = 556,
        ['5'] = 556,
        ['6'] = 556,
        ['7'] = 556,
        ['8'] = 556,
        ['9'] = 556,
        [':'] = 278,
        [';'] = 278,
        ['<'] = 584,
        ['='] = 584,
        ['>'] = 584,
        ['?'] = 556,
        ['@'] = 1015,
        ['A'] = 667,
        ['B'] = 667,
        ['C'] = 722,
        ['D'] = 722,
        ['E'] = 667,
        ['F'] = 611,
        ['G'] = 778,
        ['H'] = 722,
        ['I'] = 278,
        ['J'] = 500,
        ['K'] = 667,
        ['L'] = 556,
        ['M'] = 833,
        ['N'] = 722,
        ['O'] = 778,
        ['P'] = 667,
        ['Q'] = 778,
        ['R'] = 722,
        ['S'] = 667,
        ['T'] = 611,
        ['U'] = 722,
        ['V'] = 667,
        ['W'] = 944,
        ['X'] = 667,
        ['Y'] = 667,
        ['Z'] = 611,
        ['['] = 278,
        ['\\'] = 278,
        [']'] = 278,
        ['^'] = 469,
        ['_'] = 556,
        ['`'] = 222,
        ['a'] = 556,
        ['b'] = 556,
        ['c'] = 500,
        ['d'] = 556,
        ['e'] = 556,
        ['f'] = 278,
        ['g'] = 556,
        ['h'] = 556,
        ['i'] = 222,
        ['j'] = 222,
        ['k'] = 500,
        ['l'] = 222,
        ['m'] = 833,
        ['n'] = 556,
        ['o'] = 556,
        ['p'] = 556,
        ['q'] = 556,
        ['r'] = 333,
        ['s'] = 500,
        ['t'] = 278,
        ['u'] = 556,
        ['v'] = 500,
        ['w'] = 722,
        ['x'] = 500,
        ['y'] = 500,
        ['z'] = 500,
        ['{'] = 334,
        ['|'] = 260,
        ['}'] = 334,
        ['~'] = 584,
    };

    // Helvetica-Bold
    private static readonly Dictionary<char, int> HelveticaBoldWidths = new()
    {
        [' '] = 278,
        ['!'] = 333,
        ['"'] = 474,
        ['#'] = 556,
        ['$'] = 556,
        ['%'] = 889,
        ['&'] = 722,
        ['\''] = 278,
        ['('] = 333,
        [')'] = 333,
        ['*'] = 389,
        ['+'] = 584,
        [','] = 278,
        ['-'] = 333,
        ['.'] = 278,
        ['/'] = 278,
        ['0'] = 556,
        ['1'] = 556,
        ['2'] = 556,
        ['3'] = 556,
        ['4'] = 556,
        ['5'] = 556,
        ['6'] = 556,
        ['7'] = 556,
        ['8'] = 556,
        ['9'] = 556,
        [':'] = 333,
        [';'] = 333,
        ['<'] = 584,
        ['='] = 584,
        ['>'] = 584,
        ['?'] = 611,
        ['@'] = 975,
        ['A'] = 722,
        ['B'] = 722,
        ['C'] = 722,
        ['D'] = 722,
        ['E'] = 667,
        ['F'] = 611,
        ['G'] = 778,
        ['H'] = 722,
        ['I'] = 278,
        ['J'] = 556,
        ['K'] = 722,
        ['L'] = 611,
        ['M'] = 833,
        ['N'] = 722,
        ['O'] = 778,
        ['P'] = 667,
        ['Q'] = 778,
        ['R'] = 722,
        ['S'] = 667,
        ['T'] = 611,
        ['U'] = 722,
        ['V'] = 667,
        ['W'] = 944,
        ['X'] = 667,
        ['Y'] = 667,
        ['Z'] = 611,
        ['['] = 333,
        ['\\'] = 278,
        [']'] = 333,
        ['^'] = 584,
        ['_'] = 556,
        ['`'] = 278,
        ['a'] = 556,
        ['b'] = 611,
        ['c'] = 556,
        ['d'] = 611,
        ['e'] = 556,
        ['f'] = 333,
        ['g'] = 611,
        ['h'] = 611,
        ['i'] = 278,
        ['j'] = 278,
        ['k'] = 556,
        ['l'] = 278,
        ['m'] = 889,
        ['n'] = 611,
        ['o'] = 611,
        ['p'] = 611,
        ['q'] = 611,
        ['r'] = 389,
        ['s'] = 556,
        ['t'] = 333,
        ['u'] = 611,
        ['v'] = 556,
        ['w'] = 778,
        ['x'] = 556,
        ['y'] = 556,
        ['z'] = 500,
        ['{'] = 389,
        ['|'] = 280,
        ['}'] = 389,
        ['~'] = 584,
    };
    #endregion

    #region Times-Roman widths types
    // Times-Roman
    private static readonly Dictionary<char, int> TimesRomanWidths = new()
    {
        [' '] = 250,
        ['!'] = 333,
        ['"'] = 408,
        ['#'] = 500,
        ['$'] = 500,
        ['%'] = 833,
        ['&'] = 778,
        ['\''] = 180,
        ['('] = 333,
        [')'] = 333,
        ['*'] = 500,
        ['+'] = 564,
        [','] = 250,
        ['-'] = 333,
        ['.'] = 250,
        ['/'] = 278,
        ['0'] = 500,
        ['1'] = 500,
        ['2'] = 500,
        ['3'] = 500,
        ['4'] = 500,
        ['5'] = 500,
        ['6'] = 500,
        ['7'] = 500,
        ['8'] = 500,
        ['9'] = 500,
        [':'] = 278,
        [';'] = 278,
        ['<'] = 564,
        ['='] = 564,
        ['>'] = 564,
        ['?'] = 444,
        ['@'] = 921,
        ['A'] = 722,
        ['B'] = 667,
        ['C'] = 667,
        ['D'] = 722,
        ['E'] = 611,
        ['F'] = 556,
        ['G'] = 722,
        ['H'] = 722,
        ['I'] = 333,
        ['J'] = 389,
        ['K'] = 722,
        ['L'] = 611,
        ['M'] = 889,
        ['N'] = 722,
        ['O'] = 722,
        ['P'] = 556,
        ['Q'] = 722,
        ['R'] = 667,
        ['S'] = 556,
        ['T'] = 611,
        ['U'] = 722,
        ['V'] = 722,
        ['W'] = 944,
        ['X'] = 722,
        ['Y'] = 722,
        ['Z'] = 611,
        ['['] = 333,
        ['\\'] = 278,
        [']'] = 333,
        ['^'] = 469,
        ['_'] = 500,
        ['`'] = 333,
        ['a'] = 444,
        ['b'] = 500,
        ['c'] = 444,
        ['d'] = 500,
        ['e'] = 444,
        ['f'] = 333,
        ['g'] = 500,
        ['h'] = 500,
        ['i'] = 278,
        ['j'] = 278,
        ['k'] = 500,
        ['l'] = 278,
        ['m'] = 778,
        ['n'] = 500,
        ['o'] = 500,
        ['p'] = 500,
        ['q'] = 500,
        ['r'] = 333,
        ['s'] = 389,
        ['t'] = 278,
        ['u'] = 500,
        ['v'] = 500,
        ['w'] = 722,
        ['x'] = 500,
        ['y'] = 500,
        ['z'] = 444,
        ['{'] = 480,
        ['|'] = 200,
        ['}'] = 480,
        ['~'] = 541,
    };

    // Times-Bold (subset)
    private static readonly Dictionary<char, int> TimesRomanBoldWidths = new()
    {
        [' '] = 250,
        ['!'] = 333,
        ['"'] = 555,
        ['#'] = 500,
        ['$'] = 500,
        ['%'] = 1000,
        ['&'] = 833,
        ['\''] = 278,
        ['('] = 333,
        [')'] = 333,
        ['*'] = 500,
        ['+'] = 570,
        [','] = 250,
        ['-'] = 333,
        ['.'] = 250,
        ['/'] = 278,
        ['0'] = 500,
        ['1'] = 500,
        ['2'] = 500,
        ['3'] = 500,
        ['4'] = 500,
        ['5'] = 500,
        ['6'] = 500,
        ['7'] = 500,
        ['8'] = 500,
        ['9'] = 500,
        ['A'] = 722,
        ['B'] = 667,
        ['C'] = 722,
        ['D'] = 722,
        ['E'] = 667,
        ['F'] = 611,
        ['G'] = 778,
        ['H'] = 778,
        ['I'] = 389,
        ['J'] = 500,
        ['K'] = 778,
        ['L'] = 667,
        ['M'] = 944,
        ['N'] = 722,
        ['O'] = 778,
        ['P'] = 611,
        ['Q'] = 778,
        ['R'] = 722,
        ['S'] = 556,
        ['T'] = 667,
        ['U'] = 722,
        ['V'] = 722,
        ['W'] = 1000,
        ['X'] = 722,
        ['Y'] = 722,
        ['Z'] = 667,
        ['a'] = 500,
        ['b'] = 556,
        ['c'] = 444,
        ['d'] = 556,
        ['e'] = 444,
        ['f'] = 333,
        ['g'] = 500,
        ['h'] = 556,
        ['i'] = 278,
        ['j'] = 333,
        ['k'] = 556,
        ['l'] = 278,
        ['m'] = 833,
        ['n'] = 556,
        ['o'] = 500,
        ['p'] = 556,
        ['q'] = 556,
        ['r'] = 444,
        ['s'] = 389,
        ['t'] = 333,
        ['u'] = 556,
        ['v'] = 500,
        ['w'] = 722,
        ['x'] = 500,
        ['y'] = 500,
        ['z'] = 444,
    };

    // Times-Italic
    private static readonly Dictionary<char, int> TimesRomanItalicWidths = new()
    {
        [' '] = 250,
        ['A'] = 611,
        ['B'] = 611,
        ['C'] = 667,
        ['D'] = 722,
        ['E'] = 611,
        ['F'] = 556,
        ['G'] = 722,
        ['H'] = 722,
        ['I'] = 333,
        ['J'] = 389,
        ['K'] = 722,
        ['L'] = 611,
        ['M'] = 889,
        ['N'] = 722,
        ['O'] = 722,
        ['P'] = 556,
        ['Q'] = 722,
        ['R'] = 667,
        ['S'] = 556,
        ['T'] = 611,
        ['U'] = 722,
        ['V'] = 722,
        ['W'] = 944,
        ['X'] = 722,
        ['Y'] = 722,
        ['Z'] = 611,
        ['a'] = 500,
        ['b'] = 500,
        ['c'] = 444,
        ['d'] = 500,
        ['e'] = 444,
        ['f'] = 278,
        ['g'] = 500,
        ['h'] = 500,
        ['i'] = 278,
        ['j'] = 278,
        ['k'] = 444,
        ['l'] = 278,
        ['m'] = 722,
        ['n'] = 500,
        ['o'] = 500,
        ['p'] = 500,
        ['q'] = 500,
        ['r'] = 389,
        ['s'] = 389,
        ['t'] = 278,
        ['u'] = 500,
        ['v'] = 444,
        ['w'] = 667,
        ['x'] = 444,
        ['y'] = 444,
        ['z'] = 389,
    };

    // Times-BoldItalic
    private static readonly Dictionary<char, int> TimesRomanBoldItalicWidths = new()
    {
        [' '] = 250,
        ['A'] = 667,
        ['B'] = 667,
        ['C'] = 667,
        ['D'] = 722,
        ['E'] = 667,
        ['F'] = 667,
        ['G'] = 722,
        ['H'] = 778,
        ['I'] = 389,
        ['J'] = 500,
        ['K'] = 667,
        ['L'] = 611,
        ['M'] = 889,
        ['N'] = 722,
        ['O'] = 722,
        ['P'] = 611,
        ['Q'] = 722,
        ['R'] = 667,
        ['S'] = 556,
        ['T'] = 611,
        ['U'] = 722,
        ['V'] = 722,
        ['W'] = 944,
        ['X'] = 722,
        ['Y'] = 722,
        ['Z'] = 611,
        ['a'] = 500,
        ['b'] = 556,
        ['c'] = 444,
        ['d'] = 556,
        ['e'] = 444,
        ['f'] = 333,
        ['g'] = 500,
        ['h'] = 556,
        ['i'] = 278,
        ['j'] = 278,
        ['k'] = 556,
        ['l'] = 278,
        ['m'] = 833,
        ['n'] = 556,
        ['o'] = 500,
        ['p'] = 556,
        ['q'] = 556,
        ['r'] = 444,
        ['s'] = 389,
        ['t'] = 333,
        ['u'] = 556,
        ['v'] = 500,
        ['w'] = 722,
        ['x'] = 500,
        ['y'] = 500,
        ['z'] = 444,
    };
    #endregion

    #region Courier widths types
    // Courier (monospace: all glyphs = 600)
    private static readonly Dictionary<char, int> CourierWidths;

    static StandardFontMetrics()
    {
        CourierWidths = new Dictionary<char, int>();
        for (int i = 32; i < 127; i++)
            CourierWidths[(char)i] = 600;
    }
    #endregion
}