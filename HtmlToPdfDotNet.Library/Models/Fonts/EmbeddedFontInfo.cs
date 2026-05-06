namespace HtmlToPdfDotNet.Library.Models.Fonts;

/// <summary>
/// Holds all data extracted from a parsed TTF/OTF font file:
/// metrics (unitsPerEm, ascender, descender, capHeight, bounding box),
/// the Unicode → GlyphID mapping (cmap), the per-glyph advance widths (hmtx),
/// and the raw font bytes used for embedding.
/// </summary>
public sealed class EmbeddedFontInfo
{
    #region Identity
    /// <summary>CSS family name as specified by the caller (e.g. "Roboto").</summary>
    public string FamilyName { get; init; } = "";

    /// <summary>Whether this face is bold.</summary>
    public bool IsBold { get; init; }

    /// <summary>Whether this face is italic/oblique.</summary>
    public bool IsItalic { get; init; }

    #endregion

    #region Font-wide metrics (in design units)
    /// <summary>
    /// Font design units per em (head.unitsPerEm).
    /// All advance widths and coordinates are expressed in these units.
    /// </summary>
    public int UnitsPerEm { get; init; } = 1000;

    /// <summary>Maximum ascender above baseline (OS/2.sTypoAscender or hhea.ascender).</summary>
    public int Ascender { get; init; }

    /// <summary>Maximum descender below baseline (negative, OS/2.sTypoDescender or hhea.descender).</summary>
    public int Descender { get; init; }

    /// <summary>Cap-height in design units (OS/2.sCapHeight, or estimated).</summary>
    public int CapHeight { get; init; }

    /// <summary>Line gap (OS/2.sTypoLineGap or hhea.lineGap).</summary>
    public int LineGap { get; init; }

    /// <summary>Italic angle in degrees (from post table).</summary>
    public int ItalicAngle { get; init; }

    /// <summary>Font bounding box [xMin, yMin, xMax, yMax] in design units (head table).</summary>
    public int[] FontBBox { get; init; } = [0, -200, 1000, 800];

    /// <summary>StemV value for the FontDescriptor (estimated from weight).</summary>
    public int StemV { get; init; } = 80;

    /// <summary>PDF FontDescriptor /Flags bitfield.</summary>
    public int PdfFlags { get; init; } = 32; // Nonsymbolic

    #endregion

    #region Glyph maps
    /// <summary>
    /// cmap table: Unicode code point → Glyph ID.
    /// Built from the best available cmap subtable (Platform 3 format 4 preferred).
    /// </summary>
    public IReadOnlyDictionary<int, int> CmapUnicodeToGid { get; init; }
        = new Dictionary<int, int>();

    /// <summary>
    /// hmtx table: Glyph ID → advance width in design units.
    /// Index is GID; values beyond <c>numOfHMetrics</c> share the last entry.
    /// </summary>
    public IReadOnlyList<int> AdvanceWidths { get; init; } = [];

    /// <summary>
    /// Advance width used for GIDs that have no explicit entry
    /// (i.e. GID >= numOfHMetrics, they share the last advance width from hmtx).
    /// </summary>
    public int LastAdvanceWidth { get; init; } = 0;

    #endregion

    #region Raw font data
    /// <summary>
    /// True when the font uses CFF/CFF2 outlines (OTF).
    /// False for TrueType outlines (.ttf).
    /// Determines which FontFile key to use in the FontDescriptor.
    /// </summary>
    public bool IsCff { get; init; }

    /// <summary>
    /// The raw font file bytes.
    /// For TrueType → embedded as /FontFile2 stream.
    /// For CFF → embedded as /FontFile3 with /Subtype /OpenType.
    /// </summary>
    public byte[] FontBytes { get; init; } = [];

    #endregion

    #region Convenience helpers
    /// <summary>
    /// Returns the GID for the given Unicode code point,
    /// or 0 (the .notdef glyph) if the character is not present in the cmap.
    /// </summary>
    public int GetGlyphId(int unicodeCodepoint)
        => CmapUnicodeToGid.TryGetValue(unicodeCodepoint, out var gid) ? gid : 0;

    /// <summary>
    /// Returns the advance width of glyph <paramref name="gid"/> in design units.
    /// </summary>
    public int GetAdvanceWidth(int gid)
    {
        if (AdvanceWidths.Count == 0) return LastAdvanceWidth;
        if (gid < AdvanceWidths.Count) return AdvanceWidths[gid];
        return LastAdvanceWidth;
    }

    /// <summary>
    /// Returns the advance width of glyph <paramref name="gid"/> scaled to 1000 units/em
    /// (the unit expected by PDF /Widths arrays and <see cref="MeasureWidth"/>).
    /// </summary>
    public int GetPdfWidth(int gid)
    {
        if (UnitsPerEm == 0) return 500;
        return (int)Math.Round(GetAdvanceWidth(gid) * 1000.0 / UnitsPerEm);
    }

    /// <summary>
    /// Returns the advance width in points for the string <paramref name="text"/>
    /// at the given font size, using the embedded hmtx metrics.
    /// </summary>
    public float MeasureWidth(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text) || UnitsPerEm == 0) return 0f;
        double total = 0;
        foreach (var ch in text)
        {
            var gid = GetGlyphId(ch);
            total += GetAdvanceWidth(gid);
        }
        return (float)(total / UnitsPerEm * fontSize);
    }
    #endregion
}
