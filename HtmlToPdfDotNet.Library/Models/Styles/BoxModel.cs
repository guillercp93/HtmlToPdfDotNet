using HtmlAgilityPack;

namespace HtmlToPdfDotNet.Library.Models.Styles;

/// <summary>
/// Represents the CSS box model of a DOM node with absolute coordinates
/// already calculated during the layout pass.
/// The coordinates follow the PDF system: origin (0,0) at the bottom-left corner
/// of the page, Y grows upwards.
/// During layout, Y is worked with from the top (cursor), and inverted at the end.
/// </summary>
public sealed class BoxModel
{
    #region Source node
    public HtmlNode Node { get; }
    public ComputedStyle Style { get; }
    #endregion

    #region Content box dimensions (PDF points)
    /// <summary>Area's content width (without padding or border).</summary>
    public float ContentWidth { get; set; }

    /// <summary>Area's content height (resolved after layout of children).</summary>
    public float ContentHeight { get; set; }
    #endregion

    #region Margin box position (top-left corner)
    /// <summary>X of the outer border of the margin box, relative to the parent container.</summary>
    public float X { get; set; }

    /// <summary>Y of the outer border of the margin box (layout cursor, grows downwards).</summary>
    public float Y { get; set; }
    #endregion

    #region Derived properties
    public float MarginLeft => Style.Margin.Left.Points;
    public float MarginRight => Style.Margin.Right.Points;
    public float MarginTop => Style.Margin.Top.Points;
    public float MarginBottom => Style.Margin.Bottom.Points;

    public float PaddingLeft => Style.Padding.Left.Points;
    public float PaddingRight => Style.Padding.Right.Points;
    public float PaddingTop => Style.Padding.Top.Points;
    public float PaddingBottom => Style.Padding.Bottom.Points;

    public float BorderLeftWidth => Style.BorderLeft.Width.Points;
    public float BorderRightWidth => Style.BorderRight.Width.Points;
    public float BorderTopWidth => Style.BorderTop.Width.Points;
    public float BorderBottomWidth => Style.BorderBottom.Width.Points;
    #endregion

    #region Derived boxes
    /// <summary>X of the start of the border box.</summary>
    public float BorderBoxX => X + MarginLeft;

    /// <summary>Y of the start of the border box.</summary>
    public float BorderBoxY => Y + MarginTop;

    /// <summary>Width of the border box (content + padding + border).</summary>
    public float BorderBoxWidth =>
        ContentWidth + PaddingLeft + PaddingRight + BorderLeftWidth + BorderRightWidth;

    /// <summary>Height of the border box.</summary>
    public float BorderBoxHeight =>
        ContentHeight + PaddingTop + PaddingBottom + BorderTopWidth + BorderBottomWidth;

    /// <summary>X of the start of the padding box.</summary>
    public float PaddingBoxX => BorderBoxX + BorderLeftWidth;

    /// <summary>Y of the start of the padding box.</summary>
    public float PaddingBoxY => BorderBoxY + BorderTopWidth;

    /// <summary>X of the start of the content box.</summary>
    public float ContentX => PaddingBoxX + PaddingLeft;

    /// <summary>Y of the start of the content box.</summary>
    public float ContentY => PaddingBoxY + PaddingTop;

    /// <summary>Total height including margins (height it occupies in the flow).</summary>
    public float MarginBoxHeight => MarginTop + BorderBoxHeight + MarginBottom;

    /// <summary>Total width including margins.</summary>
    public float MarginBoxWidth => MarginLeft + BorderBoxWidth + MarginRight;
    #endregion

    public BoxModel(HtmlNode node, ComputedStyle style)
    {
        Node = node;
        Style = style;
    }

    /// <summary>
    /// Calculates the width of the content box given the available width of the parent container.
    /// Respects <see cref="ComputedStyle.Width"/> if specified, or uses the available width
    /// subtracting margins, borders and padding (block element behavior).
    /// </summary>
    public void ResolveWidth(float availableWidth)
    {
        if (!Style.Width.IsAuto && Style.Width.Points > 0f)
        {
            ContentWidth = Style.Width.Points;
        }
        else
        {
            // Block by default: occupies all available width minus margins/borders/padding
            float used = MarginLeft + MarginRight
                         + BorderLeftWidth + BorderRightWidth
                         + PaddingLeft + PaddingRight;
            ContentWidth = Math.Max(0f, availableWidth - used);
        }
    }

    public override string ToString()
        => $"[{Node.Name}] X={X:F1} Y={Y:F1} {ContentWidth:F1}×{ContentHeight:F1}";
}