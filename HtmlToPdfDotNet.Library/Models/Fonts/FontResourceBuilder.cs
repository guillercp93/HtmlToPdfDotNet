using System.IO.Compression;
using System.Text;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Fonts;

namespace HtmlToPdfDotNet.Library.Models.Writer;

/// <summary>
/// Builds all PDF font objects needed for a document:
///
///   Standard (Type1) path:
///     One /Type /Font /Subtype /Type1 object per built-in family (F1..F12).
///
///   Embedded (Type0 / CIDFontType2) path — Phase 4:
///     For every distinct <see cref="EmbeddedFontInfo"/> referenced by the layout:
///     ① FontFile stream (compressed TTF subset or full CFF)  → /FontFile2 or /FontFile3
///     ② FontDescriptor
///     ③ ToUnicode CMap stream
///     ④ CIDFontType2 descendant (with /W glyph-width array)
///     ⑤ Type0 composite font   (alias /FE0, /FE1, … referenced by content streams)
/// </summary>
public sealed class FontResourceBuilder
{
    #region Standard fonts
    private readonly Dictionary<string, int> _standardFontNums = new();
    private static readonly (string Alias, string PdfName)[] AllFonts =
    [
        ("F1",  "Helvetica"),
        ("F2",  "Helvetica-Bold"),
        ("F3",  "Helvetica-Oblique"),
        ("F4",  "Helvetica-BoldOblique"),
        ("F5",  "Times-Roman"),
        ("F6",  "Times-Bold"),
        ("F7",  "Times-Italic"),
        ("F8",  "Times-BoldItalic"),
        ("F9",  "Courier"),
        ("F10", "Courier-Bold"),
        ("F11", "Courier-Oblique"),
        ("F12", "Courier-BoldOblique"),
    ];
    #endregion

    #region Embedded fonts
    private readonly Dictionary<EmbeddedFontInfo, string> _embeddedAliases = new();
    private readonly Dictionary<EmbeddedFontInfo, SortedSet<int>> _usedGids = new();
    private readonly Dictionary<string, int> _type0Nums = new();
    #endregion

    #region Infrastructure
    private readonly ObjectCounter _counter;
    private readonly XRefTable _xref;
    /// <summary>Alias map used by <see cref="ContentStreamBuilder"/> to encode GID hex strings.</summary>
    public IReadOnlyDictionary<EmbeddedFontInfo, string> EmbeddedAliases => _embeddedAliases;
    /// <summary>
    /// Initializes a new instance of the <see cref="FontResourceBuilder"/> class.
    /// </summary>
    /// <param name="counter">Object counter for unique PDF object numbers.</param>
    /// <param name="xref">Cross-reference table for registering objects.</param>
    public FontResourceBuilder(ObjectCounter counter, XRefTable xref)
    {
        _counter = counter;
        _xref = xref;
    }
    #endregion

    #region Phase A: analyze
    /// <summary>
    /// Scans all <see cref="TextPrimitive"/> objects from the complete layout to
    /// discover which embedded fonts are needed and which glyph IDs are used.
    /// Must be called before <see cref="CreateFontObjects"/>.
    /// </summary>
    /// <param name="allPrimitives">All text primitives in the document.</param>
    public void Analyze(IEnumerable<TextPrimitive> allPrimitives)
    {
        int index = 0;
        foreach (TextPrimitive prim in allPrimitives)
        {
            if (prim.EmbeddedFont == null) continue;
            EmbeddedFontInfo font = prim.EmbeddedFont;

            if (!_embeddedAliases.ContainsKey(font))
            {
                _embeddedAliases[font] = $"FE{index++}";
                _usedGids[font] = new SortedSet<int> { 0 }; // always include .notdef
            }

            foreach (char ch in prim.Text)
                _usedGids[font].Add(font.GetGlyphId(ch));
        }
    }
    #endregion

    #region Phase B: create PDF objects
    /// <summary>
    /// Creates all PDF font objects (standard + embedded) and registers them in
    /// the cross-reference table.
    /// Returns them in the order they should be written to the PDF stream.
    /// </summary>
    /// <returns>A list of <see cref="PdfObject"/> representing the standard fonts.</returns>
    public List<PdfObject> CreateFontObjects()
    {
        List<PdfObject> objects = new();

        // Standard Type1 fonts (always emitted for fallback)
        foreach ((string alias, string pdfName) in AllFonts)
        {
            int num = _counter.Next();
            _standardFontNums[alias] = num;
            string body = $"<< /Type /Font\n   /Subtype /Type1\n   /BaseFont /{pdfName}\n   /Encoding /WinAnsiEncoding\n>>";
            PdfObject obj = new(num, body);
            objects.Add(obj);
            _xref.Add(obj);
        }

        // Embedded fonts
        foreach ((EmbeddedFontInfo font, string alias) in _embeddedAliases)
            objects.AddRange(CreateEmbeddedFontChain(font, alias, _usedGids[font]));

        return objects;
    }
    #endregion

    #region Font dictionary (for page /Resources)
    /// <summary>
    /// Builds the PDF font dictionary string for the /Resources /Font entry,
    /// including both standard (/F1…/F12) and embedded (/FE0…) font aliases.
    /// </summary>
    /// <returns>A string representing the PDF font dictionary.</returns>
    public string BuildFontDict()
    {
        StringBuilder sb = new();
        sb.Append("<< ");

        foreach ((string alias, _) in AllFonts)
            if (_standardFontNums.TryGetValue(alias, out int num))
                sb.Append($"/{alias} {num} 0 R ");

        foreach ((_, string alias) in _embeddedAliases)
            if (_type0Nums.TryGetValue(alias, out int num))
                sb.Append($"/{alias} {num} 0 R ");

        sb.Append(">>");
        return sb.ToString();
    }

    #endregion

    #region Embedded font object chain
    /// <summary>
    /// Creates the complete chain of PDF objects for one embedded font:
    /// FontFile(2/3) stream → FontDescriptor → ToUnicode CMap → CIDFontType2 → Type0.
    /// </summary>
    /// <param name="font">The <see cref="EmbeddedFontInfo"/> describing the font.</param>
    /// <param name="alias">The PDF font alias (e.g. "FE0").</param>
    /// <param name="usedGids">The set of glyph IDs to include in the subset.</param>
    /// <returns>A list of <see cref="PdfObject"/> representing the font chain.</returns>
    private List<PdfObject> CreateEmbeddedFontChain(EmbeddedFontInfo font,
                                                    string alias,
                                                    SortedSet<int> usedGids)
    {
        List<PdfObject> objects = new();

        string subsetTag = Helpers.MakeSubsetTag(font.FamilyName);
        string psBase = Helpers.SanitizePsName(font.FamilyName)
                         + (font.IsBold ? "-Bold" : "")
                         + (font.IsItalic ? "-Italic" : "");
        string psName = $"{subsetTag}+{psBase}";

        // ── ① Font file stream ─────────────────────────────────────────────
        byte[] subsetBytes = FontSubsetBuilder.Build(font, usedGids);
        byte[] compressed = Helpers.Deflate(subsetBytes);

        int fontFileNum = _counter.Next();
        string fontFileDictKey = font.IsCff ? "/FontFile3" : "/FontFile2";
        string fontFileSubtype = font.IsCff ? "\n   /Subtype /OpenType" : "";
        string fontFileHeader =
            $"<< /Length {compressed.Length}\n" +
            $"   /Filter /FlateDecode\n" +
            $"   /Length1 {subsetBytes.Length}{fontFileSubtype}\n" +
            ">>\nstream\n";

        RawStreamPdfObject fontFileObj = new(fontFileNum, fontFileHeader, compressed);
        objects.Add(fontFileObj);
        _xref.Add(fontFileObj);

        // ── ② FontDescriptor ──────────────────────────────────────────────
        int fdNum = _counter.Next();
        int em = font.UnitsPerEm > 0 ? font.UnitsPerEm : 1000;
        double scale = 1000.0 / em;

        int ascent = (int)Math.Round(font.Ascender * scale);
        int descent = (int)Math.Round(font.Descender * scale);
        int capHeight = (int)Math.Round(font.CapHeight * scale);
        int bx0 = (int)Math.Round(font.FontBBox[0] * scale);
        int by0 = (int)Math.Round(font.FontBBox[1] * scale);
        int bx1 = (int)Math.Round(font.FontBBox[2] * scale);
        int by1 = (int)Math.Round(font.FontBBox[3] * scale);

        string fdBody =
            $"<< /Type /FontDescriptor\n" +
            $"   /FontName /{psName}\n" +
            $"   /Flags {font.PdfFlags}\n" +
            $"   /FontBBox [{bx0} {by0} {bx1} {by1}]\n" +
            $"   /ItalicAngle {font.ItalicAngle}\n" +
            $"   /Ascent {ascent}\n" +
            $"   /Descent {descent}\n" +
            $"   /CapHeight {capHeight}\n" +
            $"   /StemV {font.StemV}\n" +
            $"   {fontFileDictKey} {fontFileNum} 0 R\n" +
            $">>";
        PdfObject fdObj = new(fdNum, fdBody);
        objects.Add(fdObj);
        _xref.Add(fdObj);

        // ── ③ ToUnicode CMap stream ───────────────────────────────────────
        IReadOnlyDictionary<int, int> gidToUnicode = BuildGidToUnicode(font, usedGids);
        byte[] cmapBytes = ToUnicodeCMapBuilder.Build(gidToUnicode, psName);

        int cmapNum = _counter.Next();
        string cmapHeader = $"<< /Length {cmapBytes.Length} >>\nstream\n";
        RawStreamPdfObject cmapObj = new(cmapNum, cmapHeader, cmapBytes);
        objects.Add(cmapObj);
        _xref.Add(cmapObj);

        // ── ④ CIDFontType2 descendant ─────────────────────────────────────
        int cidNum = _counter.Next();
        int dw = DefaultWidth(font);
        string wArray = BuildWidthArray(font, usedGids, dw);

        string cidBody =
            $"<< /Type /Font\n" +
            $"   /Subtype /CIDFontType2\n" +
            $"   /BaseFont /{psName}\n" +
            $"   /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >>\n" +
            $"   /FontDescriptor {fdNum} 0 R\n" +
            $"   /DW {dw}\n" +
            $"   /W {wArray}\n" +
            $"   /CIDToGIDMap /Identity\n" +
            $">>";
        PdfObject cidObj = new(cidNum, cidBody);
        objects.Add(cidObj);
        _xref.Add(cidObj);

        // ── ⑤ Type0 composite font ────────────────────────────────────────
        int type0Num = _counter.Next();
        _type0Nums[alias] = type0Num;

        string type0Body =
            $"<< /Type /Font\n" +
            $"   /Subtype /Type0\n" +
            $"   /BaseFont /{psName}\n" +
            $"   /Encoding /Identity-H\n" +
            $"   /DescendantFonts [{cidNum} 0 R]\n" +
            $"   /ToUnicode {cmapNum} 0 R\n" +
            $">>";
        PdfObject type0Obj = new(type0Num, type0Body);
        objects.Add(type0Obj);
        _xref.Add(type0Obj);

        return objects;
    }
    #endregion

    #region Width helpers
    /// <summary>
    /// Calculates the default width for a glyph based on the font's UnitsPerEm.
    /// </summary>
    /// <param name="font">The <see cref="EmbeddedFontInfo"/> describing the font.</param>
    /// <returns>The default width.</returns>
    private static int DefaultWidth(EmbeddedFontInfo font)
    {
        if (font.UnitsPerEm == 0) return 1000;
        int aw = font.LastAdvanceWidth > 0 ? font.LastAdvanceWidth : font.UnitsPerEm;
        return (int)Math.Round(aw * 1000.0 / font.UnitsPerEm);
    }

    /// <summary>
    /// Builds the PDF /W array for the CIDFont dictionary.
    /// Format: [gid [width] gid [width] …]
    /// Only GIDs whose width differs from the default width are emitted.
    /// </summary>
    /// <param name="font">The <see cref="EmbeddedFontInfo"/> describing the font.</param>
    /// <param name="usedGids">The set of glyph IDs to include in the subset.</param>
    /// <param name="defaultWidth">The default width for glyphs.</param>
    /// <returns>The PDF /W array as a string.</returns>
    private static string BuildWidthArray(EmbeddedFontInfo font,
                                          SortedSet<int> usedGids,
                                          int defaultWidth)
    {
        StringBuilder sb = new("[");
        foreach (var gid in usedGids)
        {
            int w = font.GetPdfWidth(gid);
            if (w == defaultWidth) continue;
            sb.Append($" {gid} [{w}]");
        }
        sb.Append(" ]");
        return sb.ToString();
    }
    #endregion

    #region ToUnicode helpers
    /// <summary>
    /// Builds a mapping from glyph IDs to Unicode code points for the used glyphs.
    /// </summary>
    /// <param name="font">The <see cref="EmbeddedFontInfo"/> describing the font.</param>
    /// <param name="usedGids">The set of glyph IDs to include in the subset.</param>
    /// <returns>The mapping from glyph ID to Unicode code point.</returns>
    private static IReadOnlyDictionary<int, int> BuildGidToUnicode(EmbeddedFontInfo font,
                                                                   SortedSet<int> usedGids)
    {
        Dictionary<int, int> full = ToUnicodeCMapBuilder.InvertCmap(font.CmapUnicodeToGid);
        Dictionary<int, int> result = new(usedGids.Count);
        foreach (int gid in usedGids)
            if (full.TryGetValue(gid, out int unicode))
                result[gid] = unicode;
        return result;
    }
    #endregion

}
