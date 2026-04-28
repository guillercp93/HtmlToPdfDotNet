using HtmlToPdfDotNet.Library.Commons;

namespace HtmlToPdfDotNet.Library.Models.Layout;

/// <summary>
/// A run of text with the same style.
/// </summary>
public sealed class InlineRun
{
    public string Text { get; init; } = "";
    public string FontName { get; init; } = "Helvetica";
    public float FontSize { get; init; } = 12f;
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public CssColor Color { get; init; }
    public float Width { get; set; }   // measured
}