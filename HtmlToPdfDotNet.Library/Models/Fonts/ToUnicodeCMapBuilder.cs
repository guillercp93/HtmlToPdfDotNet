using System.Text;

namespace HtmlToPdfDotNet.Library.Models.Fonts;

/// <summary>
/// Builds the <c>ToUnicode</c> CMap stream required for embedded fonts in PDF.
///
/// The ToUnicode CMap maps CIDs (= GIDs, since we use Identity-H encoding with
/// CIDToGIDMap /Identity) back to Unicode code points so that text extraction
/// and copy-paste work correctly in PDF viewers.
///
/// Output format: a valid PDF CMap stream using <c>beginbfchar</c> / <c>endbfchar</c>
/// blocks (100 entries per block as per the PDF spec recommendation).
/// </summary>
public static class ToUnicodeCMapBuilder
{
    /// <summary>
    /// Builds the ToUnicode CMap byte stream for the given GID → Unicode mapping.
    /// </summary>
    /// <param name="gidToUnicode">
    /// Dictionary from Glyph ID to Unicode code point.
    /// Typically the inverse of <see cref="EmbeddedFontInfo.CmapUnicodeToGid"/>.
    /// </param>
    /// <param name="fontName">The PostScript font name, used in the CMap stream header.</param>
    /// <returns>A byte array containing the ToUnicode CMap stream.</returns>
    public static byte[] Build(IReadOnlyDictionary<int, int> gidToUnicode, string fontName)
    {
        // Sort by GID for deterministic output
        List<KeyValuePair<int, int>> pairs = gidToUnicode.OrderBy(kv => kv.Key).ToList();

        StringBuilder sb = new(pairs.Count * 20 + 512);

        // ── CMap stream header ────────────────────────────────────────────────
        sb.AppendLine("/CIDInit /ProcSet findresource begin");
        sb.AppendLine("12 dict begin");
        sb.AppendLine("begincmap");
        sb.AppendLine("/CIDSystemInfo");
        sb.AppendLine("<< /Registry (Adobe)");
        sb.AppendLine("   /Ordering (UCS)");
        sb.AppendLine("   /Supplement 0");
        sb.AppendLine(">> def");
        sb.AppendLine($"/CMapName /{fontName}-UTF16 def");
        sb.AppendLine("/CMapType 2 def");
        sb.AppendLine("1 begincodespacerange");
        sb.AppendLine("<0000> <FFFF>");
        sb.AppendLine("endcodespacerange");

        // ── bfchar blocks (max 100 per block) ────────────────────────────────
        const int blockSize = 100;
        for (int i = 0; i < pairs.Count; i += blockSize)
        {
            int count = Math.Min(blockSize, pairs.Count - i);
            sb.AppendLine($"{count} beginbfchar");
            for (int j = 0; j < count; j++)
            {
                (int gid, int unicode) = pairs[i + j];
                // <GGGG> <UUUU>
                sb.AppendLine($"<{gid:X4}> <{unicode:X4}>");
            }
            sb.AppendLine("endbfchar");
        }

        // ── CMap stream footer ────────────────────────────────────────────────
        sb.AppendLine("endcmap");
        sb.AppendLine("CMapName currentdict /CMap defineresource pop");
        sb.AppendLine("end");
        sb.Append("end");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Inverts the Unicode→GID map to produce a GID→Unicode map.
    /// When multiple Unicode code points map to the same GID, the lowest is kept.
    /// </summary>
    /// <param name="unicodeToGid">The Unicode→GID map to invert.</param>
    /// <returns>A new dictionary mapping GID→Unicode.</returns>
    public static Dictionary<int, int> InvertCmap(IReadOnlyDictionary<int, int> unicodeToGid)
    {
        Dictionary<int, int> result = new(unicodeToGid.Count);
        foreach ((int unicode, int gid) in unicodeToGid)
        {
            if (!result.ContainsKey(gid) || unicode < result[gid])
                result[gid] = unicode;
        }
        return result;
    }
}
