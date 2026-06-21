using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;
using HtmlToPdfDotNet.Library.Models.Fonts;

namespace HtmlToPdfDotNet.Library.Commons;

public static class Helpers
{
    /// <summary>
    /// Calculates the total width of the line and applies alignment.
    /// </summary>
    /// <param name="line">The line to align.</param>
    /// <param name="availableWidth">The available width.</param>
    /// <param name="align">The alignment to apply.</param>
    public static void FinalizeAndAlign(LineBox line, float availableWidth, TextAlign align)
    {
        if (line.Items.Count == 0) return;

        float totalWidth = line.Items.Sum(i => i.Run.Width);
        line.Width = totalWidth;

        switch (align)
        {
            case TextAlign.Right:
                {
                    float offset = availableWidth - totalWidth;
                    ShiftAll(line, offset);
                    break;
                }
            case TextAlign.Center:
                {
                    float offset = (availableWidth - totalWidth) / 2f;
                    ShiftAll(line, offset);
                    break;
                }
            case TextAlign.Justify when line.Items.Count > 1:
                {
                    // Distribuir espacio extra entre los gaps entre palabras
                    int spaceCount = line.Items.Count - 1;
                    float extra = (availableWidth - totalWidth) / spaceCount;
                    float accumulated = 0f;
                    List<(InlineRun Run, float X)> updated = new();
                    for (int i = 0; i < line.Items.Count; i++)
                    {
                        (InlineRun run, float x) = line.Items[i];
                        updated.Add((run, x + accumulated));
                        if (i < spaceCount) accumulated += extra;
                    }
                    line.Items.Clear();
                    line.Items.AddRange(updated);
                    line.Width = availableWidth;
                    break;
                }
        }
    }

    /// <summary>
    /// Checks if the node has only inline content.
    /// </summary>
    /// <param name="node">The node to check.</param>
    /// <param name="parentStyle">The computed styles of the parent nodes.</param>
    /// <returns>True if the node has only inline content, false otherwise.</returns>
    public static bool HasOnlyInlineContent(HtmlNode node, Dictionary<HtmlNode, ComputedStyle> parentStyle)
    {
        foreach (HtmlNode child in node.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Text) continue;
            if (child.NodeType != HtmlNodeType.Element) continue;
            if (!parentStyle.TryGetValue(child, out ComputedStyle? style)) continue;
            if (style.Display is DisplayType.Block or DisplayType.Table or DisplayType.ListItem)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Creates an inline run from a text and a style, optionally using an embedded
    /// font from <paramref name="registry"/> for accurate per-glyph metrics.
    /// </summary>
    /// <param name="text">The text to create the inline run from.</param>
    /// <param name="style">The computed style to apply to the inline run.</param>
    /// <param name="registry">
    ///   Optional font registry.  When the requested family is found here, the run
    ///   carries an <see cref="EmbeddedFontInfo"/> and the layout engine uses its
    ///   hmtx widths instead of the AFM approximations.
    /// </param>
    /// <returns>
    ///   An <see cref="InlineRun"/> representing the text with the given style.
    /// </returns>
    public static InlineRun MakeRun(string text, ComputedStyle style, FontRegistry? registry = null, string? linkUri = null)
    {
        bool bold = style.FontWeight == FontWeight.Bold;
        bool italic = style.FontStyle == FontStyle.Italic || style.FontStyle == FontStyle.Oblique;
        string fontName;
        string transformedText = style.TextTransForm switch
        {
            TextTransForm.Capitalize => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text),
            TextTransForm.Lowercase => text.ToLowerInvariant(),
            TextTransForm.Uppercase => text.ToUpperInvariant(),
            TextTransForm.FullWidth => ToFullWidth(text),
            _ => text,
        };


        EmbeddedFontInfo? embeddedFont;
        if (registry != null && registry.TryResolve(style.FontFamily, bold, italic, out embeddedFont))
        {
            // Use the registered TTF/OTF font – keep family name in FontName for
            // identification; EmbeddedFont carries all metric / encoding data.
            fontName = embeddedFont!.FamilyName;
        }
        else
        {
            embeddedFont = null;
            fontName = StandardFontMetrics.Resolve(style.FontFamily, bold, italic);
        }

        return new InlineRun
        {
            Text = transformedText,
            FontName = fontName,
            FontSize = style.FontSize,
            Bold = bold,
            Italic = italic,
            Color = style.Color,
            TextDecoration = style.TextDecoration,
            LinkUri = linkUri,
            EmbeddedFont = embeddedFont,
        };
    }

    /// <summary>
    /// Converts standard ASCII printable characters and spaces to their Unicode full-width counterparts.
    /// </summary>
    /// <param name="input">The text to convert.</param>
    /// <returns>The text with full-width characters.</returns>
    private static string ToFullWidth(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        char[] chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] == ' ')
            {
                chars[i] = '\u3000'; // Ideographic Space
            }
            else if (chars[i] >= '!' && chars[i] <= '~')
            {
                chars[i] = (char)(chars[i] + 0xFEE0);
            }
        }
        return new string(chars);
    }

    /// <summary>
    /// Normalizes the text by replacing line breaks and tabs with spaces.
    /// </summary>
    /// <param name="raw">The raw text to normalize.</param>
    /// <returns>The normalized text.</returns>
    public static string NormalizeText(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        // Replace line breaks and tabs with space
        ReadOnlySpan<char> span = raw.Trim('\r', '\n', '\t').AsSpan();
        StringBuilder buffer = new(raw.Length);
        bool lastWasSpace = false;

        foreach (char ch in span)
        {
            if (ch is '\r' or '\n' or '\t' or ' ')
            {
                if (!lastWasSpace) buffer.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                buffer.Append(ch);
                lastWasSpace = false;
            }
        }
        return buffer.ToString();
    }

    /// <summary>
    /// Cleans the HTML string by removing null characters, BOM, and other problematic non-printable control characters.
    /// </summary>
    /// <param name="html">The raw HTML string.</param>
    /// <returns>The cleaned HTML string.</returns>
    public static string CleanHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        // Remove NULL characters, BOM, and other non-printable ASCII control characters
        string satinizeString = new(html.Where(c => c >= ' ' && c != '\0').ToArray());

        // Decode HTML entities (like &#x2B;)
        satinizeString = System.Net.WebUtility.HtmlDecode(satinizeString);
        return satinizeString.Trim('\uFEFF', '\u200B'); // Trim Byte Order Mark and Zero Width Space
    }

    /// <summary>
    /// Shifts all runs in the line by the given offset.
    /// </summary>
    /// <param name="line">The line to shift.</param>
    /// <param name="offset">The offset to shift by.</param>
    public static void ShiftAll(LineBox line, float offset)
    {
        if (offset <= 0f) return;
        List<(InlineRun Run, float)> updated = line.Items.Select(i => (i.Run, i.X + offset)).ToList();
        line.Items.Clear();
        line.Items.AddRange(updated);
    }

    /// <summary>
    /// Splits the given text into words, preserving spaces.
    /// </summary>
    /// <param name="text">The raw text.</param>
    /// <returns>The sequence of words.</returns>
    public static IEnumerable<string> SplitWords(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == ' ')
            {
                // Incluir el espacio en el token anterior
                yield return text[start..(i + 1)];
                start = i + 1;
            }
        }
        if (start < text.Length)
            yield return text[start..];
    }

    /// <summary>
    /// Updates the ascent and descent of the line with the metrics of the given run.
    /// </summary>
    /// <param name="line">The line to update.</param>
    /// <param name="run">The run to update the metrics with.</param>
    public static void UpdateMetrics(LineBox line, InlineRun run)
    {
        float ascent = run.FontSize * 0.8f;
        float descent = -run.FontSize * 0.2f;
        if (ascent > line.Ascent) line.Ascent = ascent;
        if (descent < line.Descent) line.Descent = descent;
    }

    /// <summary>
    /// Writes raw text to a stream.
    /// </summary>
    /// <param name="s">The stream to write to.</param>
    /// <param name="text">The text to write.</param>
    public static void WriteRaw(Stream s, string text)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(text);
        s.Write(bytes);
    }

    /// <summary>
    /// Formats a float with 3 decimal places.
    /// </summary>
    /// <param name="v">The float to format.</param>
    /// <returns>The formatted float.</returns>
    public static string F(float v)
        => v.ToString("F3", CultureInfo.InvariantCulture);

    /// <summary>
    /// Concatenates an array of byte arrays into a single byte array.
    /// </summary>
    /// <param name="arrays">The array of byte arrays to concatenate.</param>
    /// <returns>The concatenated byte array.</returns>
    public static byte[] ConcatArrays(IEnumerable<byte[]> arrays)
    {
        int total = arrays.Sum(a => a.Length);
        byte[] result = new byte[total];
        int offset = 0;
        foreach (byte[] arr in arrays)
        {
            arr.CopyTo(result, offset);
            offset += arr.Length;
        }
        return result;
    }

    /// <summary>
    /// Decompresses zlib/Deflate data using System.IO.Compression.ZLibStream.
    /// </summary>
    /// <param name="data">The compressed data.</param>
    /// <returns>The decompressed data.</returns>
    public static byte[] ZlibDecompress(byte[] data)
    {
        using MemoryStream input = new(data);
        using MemoryStream output = new();
        using ZLibStream zlib = new(input, CompressionMode.Decompress);
        zlib.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// Builds padded glyf data and returns the offsets.
    /// </summary>
    /// <param name="parts">The parts of the glyf data.</param>
    /// <param name="offsets">The offsets of the glyf data.</param>
    /// <returns>The padded glyf data.</returns>
    public static byte[] BuildPaddedGlyf(byte[][] parts, out int[] offsets)
    {
        offsets = new int[parts.Length + 1];
        int total = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            offsets[i] = total;
            int len = parts[i].Length;
            total += len;
            // 4-byte padding
            if (len % 4 != 0) total += 4 - (len % 4);
        }
        offsets[parts.Length] = total;

        byte[] result = new byte[total];
        int pos = 0;
        foreach (byte[] part in parts)
        {
            part.CopyTo(result, pos);
            int len = part.Length;
            pos += len;
            if (len % 4 != 0) pos += 4 - (len % 4); // skip padding (already zeroed)
        }
        return result;
    }

    /// <summary>
    /// Rebuilds the hmtx table.
    /// </summary>
    /// <param name="src">The source font bytes.</param>
    /// <param name="tableDir">The table directory.</param>
    /// <returns>The rebuilt hmtx table.</returns>
    public static byte[] RebuildHmtx(ReadOnlySpan<byte> src,
                                     Dictionary<string, (int Offset, int Length)> tableDir)
    {
        if (!tableDir.TryGetValue("hmtx", out var hmtxE))
            return [];

        // Copy original hmtx verbatim – widths are per original GID and we keep all of them.
        return CopyTable(src, hmtxE);
    }

    /// <summary>
    /// Copies a table from the source font bytes.
    /// </summary>
    /// <param name="src">The source font bytes.</param>
    /// <param name="t">The table to copy.</param>
    /// <returns>The copied table.</returns>
    public static byte[] CopyTable(ReadOnlySpan<byte> src, (int Offset, int Length) t)
        => src[t.Offset..(t.Offset + t.Length)].ToArray();

    /// <summary>
    /// Assembles a new sfnt font file from a dictionary of table tag → bytes.
    /// Computes the proper sfnt header and table directory.
    /// </summary>
    /// <param name="tables">The tables to assemble.</param>
    /// <returns>The assembled font bytes.</returns>
    public static byte[] AssembleSfnt(Dictionary<string, byte[]> tables)
    {
        int n = tables.Count;
        // searchRange = (2 ** floor(log2(n))) * 16
        int sr = 1;
        while (sr * 2 <= n) sr *= 2;
        ushort searchRange = (ushort)(sr * 16);
        ushort entrySelector = (ushort)(Math.Log2(sr));
        ushort rangeShift = (ushort)((n - sr) * 16);

        // Sort table tags
        List<string> sorted = tables.Keys.OrderBy(t => t).ToList();

        // Compute table data offsets (starting after sfnt header + table dir)
        int headerSize = 12 + n * 16;
        int dataOffset = headerSize;
        // Each table is 4-byte aligned
        Dictionary<string, int> tableOffsets = new Dictionary<string, int>();
        foreach (string tag in sorted)
        {
            tableOffsets[tag] = dataOffset;
            int len = tables[tag].Length;
            dataOffset += len;
            if (len % 4 != 0) dataOffset += 4 - (len % 4);
        }
        int totalSize = dataOffset;

        byte[] buf = new byte[totalSize];
        Span<byte> span = buf.AsSpan();

        // sfnt header (TrueType: sfVersion = 0x00010000)
        BinaryPrimitives.WriteUInt32BigEndian(span[0..], 0x00010000u);
        BinaryPrimitives.WriteUInt16BigEndian(span[4..], (ushort)n);
        BinaryPrimitives.WriteUInt16BigEndian(span[6..], searchRange);
        BinaryPrimitives.WriteUInt16BigEndian(span[8..], entrySelector);
        BinaryPrimitives.WriteUInt16BigEndian(span[10..], rangeShift);

        // Table directory
        int dirPos = 12;
        foreach (string tag in sorted)
        {
            byte[] tagBytes = Encoding.ASCII.GetBytes(tag.PadRight(4)[..4]);
            tagBytes.CopyTo(span[dirPos..]);
            int off = tableOffsets[tag];
            byte[] tdata = tables[tag];
            uint checksum = CalcChecksum(tdata);
            BinaryPrimitives.WriteUInt32BigEndian(span[(dirPos + 4)..], checksum);
            BinaryPrimitives.WriteUInt32BigEndian(span[(dirPos + 8)..], (uint)off);
            BinaryPrimitives.WriteUInt32BigEndian(span[(dirPos + 12)..], (uint)tdata.Length);
            dirPos += 16;
        }

        // Table data
        foreach (string tag in sorted)
        {
            int off = tableOffsets[tag];
            tables[tag].CopyTo(span[off..]);
        }

        return buf;
    }

    /// <summary>
    /// Calculates the checksum of the given data.
    /// </summary>
    /// <param name="data">The data to calculate the checksum of.</param>
    /// <returns>The checksum.</returns>
    public static uint CalcChecksum(byte[] data)
    {
        uint sum = 0;
        int i = 0;
        while (i + 3 < data.Length)
        {
            sum += BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i));
            i += 4;
        }
        // Remaining bytes (pad with zeros)
        if (i < data.Length)
        {
            uint last = 0;
            int shift = 24;
            while (i < data.Length) { last |= (uint)data[i++] << shift; shift -= 8; }
            sum += last;
        }
        return sum;
    }

    /// <summary>
    /// Compresses the given byte array using Zlib (FlateDecode).
    /// </summary>
    public static byte[] Deflate(byte[] data)
    {
        using MemoryStream ms = new();
        using (ZLibStream zlib = new(ms, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(data, 0, data.Length);
        return ms.ToArray();
    }

    /// <summary>
    /// Generates a deterministic 6-character uppercase subset prefix from a string.
    /// </summary>
    public static string MakeSubsetTag(string input)
    {
        uint hash = 2166136261u;
        foreach (char c in input) { hash ^= (byte)c; hash *= 16777619u; }
        StringBuilder sb = new(6);
        for (int i = 0; i < 6; i++) { sb.Append((char)('A' + hash % 26)); hash /= 26; }
        return sb.ToString();
    }

    /// <summary>
    /// Sanitizes a string for use as a PDF/PostScript name token.
    /// </summary>
    public static string SanitizePsName(string name)
        => new string(name.Replace(" ", "")
            .Where(c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')
                          or (>= '0' and <= '9') or '-' or '_')
            .ToArray());


    /// <summary>
    /// Measures <paramref name="text"/> using the embedded font's hmtx table when
    /// available, otherwise falls back to the AFM-based <see cref="StandardFontMetrics"/>.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="run">The inline run containing the font information.</param>
    /// <returns>The width of the text in points.</returns>
    public static float MeasureText(string text, InlineRun run)
        => run.EmbeddedFont != null
            ? run.EmbeddedFont.MeasureWidth(text, run.FontSize)
            : StandardFontMetrics.MeasureWidth(text, run.FontName, run.FontSize);

    /// <summary>
    /// Creates a CssEdges with the given points for top and bottom margins and zero for left and right margins.
    /// </summary>
    /// <param name="points">The top and bottom margin points.</param>
    /// <returns>The CssEdges with the given points for top and bottom margins and zero for left and right margins.</returns>
    public static CssEdges DefaultMargin(float points) => new(new CssLength(points), CssLength.Zero);

    /// <summary>
    /// Creates a CssEdges with the given points for left and right padding and zero for top and bottom padding.
    /// </summary>
    /// <param name="points">The left and right padding points.</param>
    /// <returns>The CssEdges with the given points for left and right padding and zero for top and bottom padding.</returns>
    public static CssEdges DefaultPadding(float points) => new(CssLength.Zero, new CssLength(points), CssLength.Zero, new CssLength(points));

}