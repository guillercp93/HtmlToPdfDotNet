using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Library.Models.Layout;

/// <summary>
/// Builds lines of text for the given inline content (word-wrap: break-word).
/// </summary>
public static class InlineLayoutEngine
{
    /// <summary>
    /// Build lines of text for the given inline content.
    /// </summary>
    /// <param name="runs">Sequence of runs with inline text styling.</param>
    /// <param name="availableWidth">Available width in points.</param>
    /// <param name="textAlign">Horizontal alignment of text.</param>
    /// <returns>Sequence of lines ready to render.</returns>
    public static List<LineBox> Layout(
        IEnumerable<InlineRun> runs,
        float availableWidth,
        TextAlign textAlign = TextAlign.Left)
    {
        var lines = new List<LineBox>();
        var line = new LineBox();
        float curX = 0f;

        foreach (var run in runs)
        {
            // Measure the run
            run.Width = StandardFontMetrics.MeasureWidth(run.Text, run.FontName, run.FontSize);

            // Split into words preserving spaces
            var words = Helpers.SplitWords(run.Text);

            foreach (var word in words)
            {
                var wordWidth = StandardFontMetrics.MeasureWidth(word, run.FontName, run.FontSize);

                // If the word does not fit and the line already has content -> new line
                if (curX + wordWidth > availableWidth && line.Items.Count > 0)
                {
                    Helpers.FinalizeAndAlign(line, availableWidth, textAlign);
                    lines.Add(line);
                    line = new LineBox();
                    curX = 0f;

                    // Skip leading space
                    var trimmedWord = word.TrimStart();
                    if (string.IsNullOrEmpty(trimmedWord)) continue;
                    wordWidth = StandardFontMetrics.MeasureWidth(trimmedWord, run.FontName, run.FontSize);

                    line.Items.Add((new InlineRun
                    {
                        Text = trimmedWord,
                        FontName = run.FontName,
                        FontSize = run.FontSize,
                        Bold = run.Bold,
                        Italic = run.Italic,
                        Color = run.Color,
                        Width = wordWidth,
                    }, curX));
                    Helpers.UpdateMetrics(line, run);
                    curX += wordWidth;
                }
                else
                {
                    // Word fits in the current line
                    line.Items.Add((new InlineRun
                    {
                        Text = word,
                        FontName = run.FontName,
                        FontSize = run.FontSize,
                        Bold = run.Bold,
                        Italic = run.Italic,
                        Color = run.Color,
                        Width = wordWidth,
                    }, curX));
                    Helpers.UpdateMetrics(line, run);
                    curX += wordWidth;
                }
            }
        }

        // Last line
        if (line.Items.Count > 0)
        {
            // Last line is never justified (CSS behavior)
            var lastAlign = textAlign == TextAlign.Justify ? TextAlign.Left : textAlign;
            Helpers.FinalizeAndAlign(line, availableWidth, lastAlign);
            lines.Add(line);
        }

        return lines;
    }
}