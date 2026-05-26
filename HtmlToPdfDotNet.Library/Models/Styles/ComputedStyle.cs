using HtmlToPdfDotNet.Library.Commons;

namespace HtmlToPdfDotNet.Library.Models.Styles;

/// <summary>
/// Represents the computed style of an HTML element.
/// </summary>
public sealed class ComputedStyle
{
    #region Display
    public DisplayType Display { get; set; } = DisplayType.Block;
    #endregion

    #region Box model
    public CssLength Width { get; set; } = CssLength.Auto;
    public CssLength Height { get; set; } = CssLength.Auto;

    public CssEdges Margin { get; set; } = CssEdges.Zero;
    public CssEdges Padding { get; set; } = CssEdges.Zero;

    public CssBorderSide BorderTop { get; set; } = CssBorderSide.None;
    public CssBorderSide BorderRight { get; set; } = CssBorderSide.None;
    public CssBorderSide BorderBottom { get; set; } = CssBorderSide.None;
    public CssBorderSide BorderLeft { get; set; } = CssBorderSide.None;
    public CssLength BorderRadius { get; set; } = CssLength.Zero;
    #endregion

    #region Colors
    public CssColor Color { get; set; } = CssColor.Black;
    public CssColor BackgroundColor { get; set; } = CssColor.Transparent;
    #endregion

    #region Typography
    public string FontFamily { get; set; } = "Helvetica";
    public float FontSize { get; set; } = Constants.DefaultFontSize;  // points
    public FontWeight FontWeight { get; set; } = FontWeight.Normal;
    public FontStyle FontStyle { get; set; } = FontStyle.Normal;
    public float LineHeight { get; set; } = 1.2f;         // multiplier
    #endregion

    #region Text
    public TextAlign TextAlign { get; set; } = TextAlign.Left;
    public TextTransForm TextTransForm { get; set; } = TextTransForm.None;
    #endregion

    #region Page breaks
    public bool PageBreakBefore { get; set; } = false;
    public bool PageBreakAfter { get; set; } = false;
    #endregion

    /// <summary>
    /// Creates a shallow copy of the current style.
    /// </summary>
    /// <returns>A shallow copy of the current style.</returns>
    public ComputedStyle Clone() => (ComputedStyle)MemberwiseClone();
}