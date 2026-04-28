namespace HtmlToPdfDotNet.Library.Models.Layout;

/// <summary>
/// A line of text composed of several inline runs.
/// </summary>
public sealed class LineBox
{
    /// <summary>
    /// The runs that make up the line.
    /// </summary>
    public List<(InlineRun Run, float X)> Items { get; set; } = new();

    /// <summary>
    /// The total width of the line.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    /// The ascent of the line.
    /// </summary>
    public float Ascent { get; set; }

    /// <summary>
    /// The descent of the line.
    /// </summary>
    public float Descent { get; set; }

    /// <summary>
    /// The height of the line.
    /// </summary>
    public float LineHeight => Ascent - Descent;
}