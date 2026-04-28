using System.Globalization;
using System.Text;
using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;

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

        var totalWidth = line.Items.Sum(i => i.Run.Width);
        line.Width = totalWidth;

        switch (align)
        {
            case TextAlign.Right:
                {
                    var offset = availableWidth - totalWidth;
                    ShiftAll(line, offset);
                    break;
                }
            case TextAlign.Center:
                {
                    var offset = (availableWidth - totalWidth) / 2f;
                    ShiftAll(line, offset);
                    break;
                }
            case TextAlign.Justify when line.Items.Count > 1:
                {
                    // Distribuir espacio extra entre los gaps entre palabras
                    var spaceCount = line.Items.Count - 1;
                    var extra = (availableWidth - totalWidth) / spaceCount;
                    var accumulated = 0f;
                    var updated = new List<(InlineRun Run, float X)>();
                    for (int i = 0; i < line.Items.Count; i++)
                    {
                        var (run, x) = line.Items[i];
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
        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Text) continue;
            if (child.NodeType != HtmlNodeType.Element) continue;
            if (!parentStyle.TryGetValue(child, out var style)) continue;
            if (style.Display == DisplayType.Block || style.Display == DisplayType.Table)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Creates an inline run from a text and a style.
    /// </summary>
    /// <param name="text">The text to create the inline run from.</param>
    /// <param name="style">The computed style to apply to the inline run.</param>
    /// <returns>The inline run.</returns>
    public static InlineRun MakeRun(string text, ComputedStyle style)
    {
        bool bold = style.FontWeight == FontWeight.Bold;
        bool italic = style.FontStyle == FontStyle.Italic || style.FontStyle == FontStyle.Oblique;
        var font = StandardFontMetrics.Resolve(style.FontFamily, bold, italic);
        return new InlineRun
        {
            Text = text,
            FontName = font,
            FontSize = style.FontSize,
            Bold = bold,
            Italic = italic,
            Color = style.Color,
        };
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
        var span = raw.AsSpan();
        var buffer = new System.Text.StringBuilder(raw.Length);
        bool lastWasSpace = false;

        foreach (var ch in span)
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
    /// Shifts all runs in the line by the given offset.
    /// </summary>
    /// <param name="line">The line to shift.</param>
    /// <param name="offset">The offset to shift by.</param>
    public static void ShiftAll(LineBox line, float offset)
    {
        if (offset <= 0f) return;
        var updated = line.Items.Select(i => (i.Run, i.X + offset)).ToList();
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
        var ascent = run.FontSize * 0.8f;
        var descent = -run.FontSize * 0.2f;
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
        var bytes = Encoding.Latin1.GetBytes(text);
        s.Write(bytes);
    }

    /// <summary>
    /// Formats a float with 3 decimal places.
    /// </summary>
    /// <param name="v">The float to format.</param>
    /// <returns>The formatted float.</returns>
    public static string F(float v)
        => v.ToString("F3", CultureInfo.InvariantCulture);
}