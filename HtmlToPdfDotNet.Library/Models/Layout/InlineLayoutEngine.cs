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
    public static List<LineBox> Layout(IEnumerable<InlineRun> runs,
                                       float availableWidth,
                                       TextAlign textAlign = TextAlign.Left,
                                       float containerFontSize = 0f,
                                       float lineHeightMultiplier = 0f)
    {
        List<LineBox> lines = new();
        LineBox line = new();
        float curX = 0f;

        foreach (InlineRun run in runs)
        {
            // Measure the run (use embedded font hmtx if available)
            run.Width = Helpers.MeasureText(run.Text, run);

            // Split into words preserving spaces
            IEnumerable<string> words = Helpers.SplitWords(run.Text);

            foreach (string word in words)
            {
                float wordWidth = Helpers.MeasureText(word, run);

                // If the word does not fit and the line already has content -> new line
                if (curX + wordWidth > availableWidth && line.Items.Count > 0)
                {
                    ApplyCssLineHeight(line, containerFontSize, lineHeightMultiplier);
                    Helpers.FinalizeAndAlign(line, availableWidth, textAlign);
                    lines.Add(line);
                    line = new LineBox();
                    curX = 0f;

                    // Skip leading space
                    string trimmedWord = word.TrimStart();
                    if (string.IsNullOrEmpty(trimmedWord)) continue;
                    wordWidth = Helpers.MeasureText(trimmedWord, run);

                    line.Items.Add((new InlineRun
                    {
                        Text = trimmedWord,
                        FontName = run.FontName,
                        FontSize = run.FontSize,
                        Bold = run.Bold,
                        Italic = run.Italic,
                        Color = run.Color,
                        EmbeddedFont = run.EmbeddedFont,
                        Width = wordWidth,
                        TextDecoration = run.TextDecoration,
                        LinkUri = run.LinkUri,
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
                        EmbeddedFont = run.EmbeddedFont,
                        Width = wordWidth,
                        TextDecoration = run.TextDecoration,
                        LinkUri = run.LinkUri,
                    }, curX));
                    Helpers.UpdateMetrics(line, run);
                    curX += wordWidth;
                }
            }
        }

        // Last line
        if (line.Items.Count > 0)
        {
            ApplyCssLineHeight(line, containerFontSize, lineHeightMultiplier);
            // Last line is never justified (CSS behavior)
            TextAlign lastAlign = textAlign == TextAlign.Justify ? TextAlign.Left : textAlign;
            Helpers.FinalizeAndAlign(line, availableWidth, lastAlign);
            lines.Add(line);
        }

        return lines;
    }

    private static void ApplyCssLineHeight(LineBox line, float containerFontSize, float lineHeightMultiplier)
    {
        if (containerFontSize <= 0f || lineHeightMultiplier <= 0f) return;
        float cssLineHeight = containerFontSize * lineHeightMultiplier;
        if (cssLineHeight > line.Ascent - line.Descent)
            line.SetLineHeight(cssLineHeight);
    }
}