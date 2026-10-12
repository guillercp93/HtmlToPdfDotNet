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

    private float _cssLineHeight;
    /// <summary>
    /// The height of the line — uses CSS line-height when set, otherwise font-metric height.
    /// </summary>
    public float LineHeight => _cssLineHeight > 0f ? _cssLineHeight : Ascent - Descent;

    /// <summary>
    /// Sets the CSS line-height for this line.
    /// </summary>
    public void SetLineHeight(float value) => _cssLineHeight = value;
}