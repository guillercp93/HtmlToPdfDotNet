using System.Buffers.Binary;
using System.Text;

namespace HtmlToPdfDotNet.Library.Models.Fonts;

/// <summary>
/// Binary parser for OpenType/TrueType font files (.ttf / .otf).
///
/// Tables read:
///   head  → unitsPerEm, indexToLocFormat, fontBBox
///   hhea  → ascender, descender, lineGap, numOfHMetrics
///   OS/2  → sTypoAscender, sTypoDescender, sCapHeight, fsType, fsSelection
///   hmtx  → advance widths and lsb per glyph
///   cmap  → Unicode → GlyphID (prefers Platform 3 Format 4, falls back to Platform 0 Format 4)
///   post  → italicAngle
///   name  → family name (nameId 1 or 4)
///
/// The parser is purely read-only; it does not modify the font file.
/// </summary>
public sealed class TrueTypeFontParser
{
    /// <summary>
    /// Parses the font at <paramref name="fontPath"/> and returns an
    /// <see cref="EmbeddedFontInfo"/> populated with all relevant metrics.
    /// </summary>
    /// <param name="fontPath">Path to the font file.</param>
    /// <param name="familyName">The family name of the font.</param>
    /// <param name="bold">Indicates if the font is bold.</param>
    /// <param name="italic">Indicates if the font is italic.</param>
    /// <returns>An <see cref="EmbeddedFontInfo"/> object populated with all relevant metrics.</returns>  
    public static EmbeddedFontInfo Parse(string fontPath, string familyName, bool bold, bool italic)
    {
        byte[] fontBytes = File.ReadAllBytes(fontPath);
        return Parse(fontBytes, familyName, bold, italic);
    }

    /// <summary>
    /// Parses the font from raw <paramref name="fontBytes"/> and returns an
    /// <see cref="EmbeddedFontInfo"/> populated with all relevant metrics.
    /// </summary>
    /// <param name="fontBytes">Byte array containing the font data.</param>
    /// <param name="familyName">The family name of the font.</param>
    /// <param name="bold">Indicates if the font is bold.</param>
    /// <param name="italic">Indicates if the font is italic.</param>
    /// <returns>An <see cref="EmbeddedFontInfo"/> object populated with all relevant metrics.</returns>
    /// <exception cref="InvalidDataException">When the font data is invalid.</exception>
    public static EmbeddedFontInfo Parse(byte[] fontBytes, string familyName, bool bold, bool italic)
    {
        Span<byte> data = fontBytes.AsSpan();

        // ── Sfnt header ───────────────────────────────────────────────────────
        // Offset 0: sfVersion (uint32)
        uint sfVersion = BinaryPrimitives.ReadUInt32BigEndian(data[0..4]);
        bool isCff = sfVersion == 0x4F54544Fu; // 'OTTO'

        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(data[4..6]);

        // Build table directory: tag → (offset, length)
        Dictionary<string, (uint Offset, uint Length)> tables = new(numTables);
        int dirBase = 12; // sizeof(sfnt header)
        for (int i = 0; i < numTables; i++)
        {
            int entry = dirBase + i * 16;
            string tag = Encoding.ASCII.GetString(data[entry..(entry + 4)]);
            uint offset = BinaryPrimitives.ReadUInt32BigEndian(data[(entry + 8)..(entry + 12)]);
            uint length = BinaryPrimitives.ReadUInt32BigEndian(data[(entry + 12)..(entry + 16)]);
            tables[tag.TrimEnd()] = (offset, length);
        }

        // ── head ──────────────────────────────────────────────────────────────
        int unitsPerEm = 1000;
        int[] fontBBox = [0, -200, 1000, 800];
        if (tables.TryGetValue("head", out var headT))
        {
            Span<byte> h = data[(int)headT.Offset..];
            unitsPerEm = BinaryPrimitives.ReadUInt16BigEndian(h[18..20]);
            short xMin = BinaryPrimitives.ReadInt16BigEndian(h[36..38]);
            short yMin = BinaryPrimitives.ReadInt16BigEndian(h[38..40]);
            short xMax = BinaryPrimitives.ReadInt16BigEndian(h[40..42]);
            short yMax = BinaryPrimitives.ReadInt16BigEndian(h[42..44]);
            fontBBox = [xMin, yMin, xMax, yMax];
        }

        // ── hhea ──────────────────────────────────────────────────────────────
        int ascender = (int)(unitsPerEm * 0.8);
        int descender = -(int)(unitsPerEm * 0.2);
        int lineGap = 0;
        int numOfHMetrics = 0;
        if (tables.TryGetValue("hhea", out var hheaT))
        {
            Span<byte> h = data[(int)hheaT.Offset..];
            ascender = BinaryPrimitives.ReadInt16BigEndian(h[4..6]);
            descender = BinaryPrimitives.ReadInt16BigEndian(h[6..8]);
            lineGap = BinaryPrimitives.ReadInt16BigEndian(h[8..10]);
            numOfHMetrics = BinaryPrimitives.ReadUInt16BigEndian(h[34..36]);
        }

        // ── OS/2 ──────────────────────────────────────────────────────────────
        int capHeight = (int)(unitsPerEm * 0.72);
        int pdfFlags = 32; // Nonsymbolic
        if (tables.TryGetValue("OS/2", out var os2T))
        {
            Span<byte> h = data[(int)os2T.Offset..];
            ushort os2Version = BinaryPrimitives.ReadUInt16BigEndian(h[0..2]);

            short typoAsc = BinaryPrimitives.ReadInt16BigEndian(h[68..70]);
            short typoDes = BinaryPrimitives.ReadInt16BigEndian(h[70..72]);
            if (typoAsc != 0) ascender = typoAsc;
            if (typoDes != 0) descender = typoDes;

            if (os2Version >= 2 && h.Length > 90)
                capHeight = BinaryPrimitives.ReadInt16BigEndian(h[88..90]);

            // fsSelection bits
            ushort fsSelection = BinaryPrimitives.ReadUInt16BigEndian(h[62..64]);
            bool isItalicFlag = (fsSelection & 0x01) != 0;
            bool isBoldFlag = (fsSelection & 0x20) != 0;

            // Build PDF Flags
            pdfFlags = 32; // Nonsymbolic
            if (isItalicFlag) pdfFlags |= 64; // Italic
            if (isBoldFlag) pdfFlags |= 0;  // no special bit for bold in Flags
        }

        // ── post ──────────────────────────────────────────────────────────────
        int italicAngle = 0;
        if (tables.TryGetValue("post", out var postT))
        {
            Span<byte> h = data[(int)postT.Offset..];
            // italicAngle is a Fixed (16.16) at offset 4
            int raw = BinaryPrimitives.ReadInt32BigEndian(h[4..8]);
            italicAngle = raw >> 16; // integer part
        }

        // ── maxp ──────────────────────────────────────────────────────────────
        int numGlyphs = 0;
        if (tables.TryGetValue("maxp", out var maxpT))
        {
            Span<byte> h = data[(int)maxpT.Offset..];
            numGlyphs = BinaryPrimitives.ReadUInt16BigEndian(h[4..6]);
        }

        // ── hmtx ──────────────────────────────────────────────────────────────
        int[] advanceWidths = [];
        int lastAdvWidth = unitsPerEm;
        if (tables.TryGetValue("hmtx", out var hmtxT) && numOfHMetrics > 0)
        {
            Span<byte> h = data[(int)hmtxT.Offset..];
            advanceWidths = new int[numGlyphs > 0 ? numGlyphs : numOfHMetrics];
            int count = Math.Min(numOfHMetrics, advanceWidths.Length);
            for (int i = 0; i < count; i++)
            {
                int entryOff = i * 4;
                if (entryOff + 2 > h.Length) break;
                advanceWidths[i] = BinaryPrimitives.ReadUInt16BigEndian(h[entryOff..(entryOff + 2)]);
            }
            lastAdvWidth = advanceWidths[count - 1];
            // Glyphs beyond numOfHMetrics share the last advance width
            for (int i = count; i < advanceWidths.Length; i++)
                advanceWidths[i] = lastAdvWidth;
        }

        // ── cmap ──────────────────────────────────────────────────────────────
        Dictionary<int, int> cmapUnicodeToGid = new(256);
        if (tables.TryGetValue("cmap", out var cmapT))
        {
            cmapUnicodeToGid = ParseCmap(data, (int)cmapT.Offset);
        }

        // ── StemV estimate ────────────────────────────────────────────────────
        // Heuristic: bold ≈ 120, regular ≈ 80
        int stemV = bold ? 120 : 80;

        return new EmbeddedFontInfo
        {
            FamilyName = familyName,
            IsBold = bold,
            IsItalic = italic,
            IsCff = isCff,
            UnitsPerEm = unitsPerEm,
            Ascender = ascender,
            Descender = descender,
            CapHeight = capHeight > 0 ? capHeight : (int)(unitsPerEm * 0.72),
            LineGap = lineGap,
            ItalicAngle = italicAngle,
            FontBBox = fontBBox,
            StemV = stemV,
            PdfFlags = pdfFlags,
            CmapUnicodeToGid = cmapUnicodeToGid,
            AdvanceWidths = advanceWidths,
            LastAdvanceWidth = lastAdvWidth,
            FontBytes = fontBytes,
        };
    }

    #region cmap parser
    /// <summary>
    /// Parses the cmap table and returns a Unicode → GID dictionary.
    /// Preference order:
    ///   1. Platform 3 (Windows), Encoding 1 (Unicode BMP), Format 4
    ///   2. Platform 0 (Unicode), any encoding, Format 4
    ///   3. Platform 3, Encoding 10, Format 12 (full Unicode)
    /// </summary>
    /// <param name="data">The byte array containing the font data.</param>
    /// <param name="cmapOffset">The offset to the cmap table.</param>
    /// <returns>A dictionary mapping Unicode code points to glyph IDs.</returns>
    private static Dictionary<int, int> ParseCmap(ReadOnlySpan<byte> data, int cmapOffset)
    {
        ReadOnlySpan<byte> table = data[cmapOffset..];

        ushort numSubtables = BinaryPrimitives.ReadUInt16BigEndian(table[2..4]);

        // Collect candidate subtables
        List<(int Platform, int Encoding, int Format, int SubtableOffset)> candidates = new();
        for (int i = 0; i < numSubtables; i++)
        {
            int rec = 4 + i * 8;
            if (rec + 8 > table.Length) break;
            ushort platform = BinaryPrimitives.ReadUInt16BigEndian(table[rec..(rec + 2)]);
            ushort encoding = BinaryPrimitives.ReadUInt16BigEndian(table[(rec + 2)..(rec + 4)]);
            int offset = (int)BinaryPrimitives.ReadUInt32BigEndian(table[(rec + 4)..(rec + 8)]);

            if (offset + 2 > table.Length) continue;
            ushort format = BinaryPrimitives.ReadUInt16BigEndian(table[offset..(offset + 2)]);
            candidates.Add((platform, encoding, format, offset));
        }

        // Priority 1: Platform 3 Encoding 1 Format 4 (Windows BMP Unicode)
        var best = candidates.FirstOrDefault(c => c.Platform == 3 && c.Encoding == 1 && c.Format == 4);
        if (best == default)
        {
            // Priority 2: Platform 0 (Unicode) Format 4
            best = candidates.FirstOrDefault(c => c.Platform == 0 && c.Format == 4);
        }
        if (best != default)
            return ParseFormat4(table[best.SubtableOffset..]);

        // Priority 3: Platform 3 Encoding 10 Format 12 (full Unicode)
        var fmt12 = candidates.FirstOrDefault(c => c.Platform == 3 && c.Encoding == 10 && c.Format == 12);
        if (fmt12 != default)
            return ParseFormat12(table[fmt12.SubtableOffset..]);

        return new Dictionary<int, int>();
    }

    /// <summary>Parses a cmap Format 4 subtable (BMP, segmented coverage).</summary>
    /// <param name="sub">The subtable to parse.</param>
    /// <returns>A dictionary mapping Unicode code points to glyph IDs.</returns>
    private static Dictionary<int, int> ParseFormat4(ReadOnlySpan<byte> sub)
    {
        // Format 4 layout:
        //  0: format (2)
        //  2: length (2)
        //  4: language (2)
        //  6: segCountX2 (2)
        // ... search/range/shift fields (6 bytes)
        // 14: endCode[segCount]
        // 14+2*segCount: reservedPad (2)
        // 16+2*segCount: startCode[segCount]
        // 16+4*segCount: idDelta[segCount]
        // 16+6*segCount: idRangeOffset[segCount]
        // 16+8*segCount: glyphIdArray[]

        if (sub.Length < 14) return new();

        int segCount = BinaryPrimitives.ReadUInt16BigEndian(sub[6..8]) / 2;
        if (segCount <= 0) return new();

        Dictionary<int, int> result = new(segCount * 32);

        int endCodesBase = 14;
        int startCodesBase = 16 + 2 * segCount;
        int idDeltaBase = 16 + 4 * segCount;
        int idRangeOffsetBase = 16 + 6 * segCount;
        int glyphIdArrayBase = 16 + 8 * segCount;

        for (int seg = 0; seg < segCount; seg++)
        {
            int endCode = BinaryPrimitives.ReadUInt16BigEndian(sub[(endCodesBase + seg * 2)..]);
            int startCode = BinaryPrimitives.ReadUInt16BigEndian(sub[(startCodesBase + seg * 2)..]);
            int idDelta = BinaryPrimitives.ReadInt16BigEndian(sub[(idDeltaBase + seg * 2)..]);
            int idRangeOff = BinaryPrimitives.ReadUInt16BigEndian(sub[(idRangeOffsetBase + seg * 2)..]);

            if (startCode == 0xFFFF) break; // terminal segment

            for (int c = startCode; c <= endCode; c++)
            {
                int gid;
                if (idRangeOff == 0)
                {
                    gid = (c + idDelta) & 0xFFFF;
                }
                else
                {
                    // The idRangeOffset is relative to *its own position* in the array
                    int idRangeOffPos = idRangeOffsetBase + seg * 2;
                    int glyphIdIndex = idRangeOff + (c - startCode) * 2 + idRangeOffPos - idRangeOffsetBase;
                    int glyphArrayPos = glyphIdArrayBase + glyphIdIndex - (idRangeOffsetBase - idRangeOffsetBase);

                    // Correct relative addressing:
                    // position of this idRangeOffset entry (in bytes from subtable start)
                    int fieldPos = idRangeOffsetBase + seg * 2;
                    // The glyph array index address = fieldPos + idRangeOff + (c - startCode)*2
                    int addr = fieldPos + idRangeOff + (c - startCode) * 2;
                    if (addr + 2 > sub.Length) continue;
                    gid = BinaryPrimitives.ReadUInt16BigEndian(sub[addr..(addr + 2)]);
                    if (gid != 0) gid = (gid + idDelta) & 0xFFFF;
                }

                if (gid != 0)
                    result[c] = gid;
            }
        }

        return result;
    }

    /// <summary>Parses a cmap Format 12 subtable (full Unicode coverage, 32-bit).</summary>
    /// <param name="sub">The subtable to parse.</param>
    /// <returns>A dictionary mapping Unicode code points to glyph IDs.</returns>
    private static Dictionary<int, int> ParseFormat12(ReadOnlySpan<byte> sub)
    {
        if (sub.Length < 16) return new();

        uint numGroups = BinaryPrimitives.ReadUInt32BigEndian(sub[12..16]);
        Dictionary<int, int> result = new((int)numGroups * 8);

        for (int g = 0; g < (int)numGroups; g++)
        {
            int groupOff = 16 + g * 12;
            if (groupOff + 12 > sub.Length) break;

            uint startCharCode = BinaryPrimitives.ReadUInt32BigEndian(sub[groupOff..(groupOff + 4)]);
            uint endCharCode = BinaryPrimitives.ReadUInt32BigEndian(sub[(groupOff + 4)..(groupOff + 8)]);
            uint startGlyphId = BinaryPrimitives.ReadUInt32BigEndian(sub[(groupOff + 8)..(groupOff + 12)]);

            for (uint cp = startCharCode; cp <= endCharCode; cp++)
            {
                int gid = (int)(startGlyphId + (cp - startCharCode));
                result[(int)cp] = gid;
            }
        }

        return result;
    }
    #endregion
}
