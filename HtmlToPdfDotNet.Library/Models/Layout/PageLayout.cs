namespace HtmlToPdfDotNet.Library.Models.Layout;

/// <summary>
/// Defines the dimensions of a page along with its margins.
/// </summary>
public sealed class PageLayout
{
    /// <summary>
    /// Page width in points (1/72 inch).
    /// Default is 595.28 (A4).
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// Page height in points (1/72 inch).
    /// Default is 841.89 (A4).
    /// </summary>
    public float Height { get; }

    /// <summary>
    /// Margins (top, bottom, left, right) in points.
    /// Default is 10mm (28.35 points) for all sides.
    /// </summary>
    public PageMargins Margins { get; }

    /// <summary>
    /// The effective width of the content area (page width minus left/right margins).
    /// </summary>
    public float ContentWidth => Width - Margins.Left - Margins.Right;

    /// <summary>
    /// The effective height of the content area (page height minus top/bottom margins).
    /// </summary>
    public float ContentHeight => Height - Margins.Top - Margins.Bottom;

    /// <summary>
    /// Space reserved at the top and bottom of the content area for
    /// running headers and footers, in points. This value is subtracted
    /// from the effective content height by the layout engine so that
    /// page content stays within the non-reserved region.
    ///
    /// Default is 0 (no reservation).
    /// </summary>
    public float ReservedHeaderFooterHeight { get; set; }

    public PageLayout(
        float width = 595.28f,
        float height = 841.89f,
        PageMargins? margins = null)
    {
        Width = width;
        Height = height;
        Margins = margins ?? PageMargins.Default;
    }

    public static PageLayout A4 => new(595.28f, 841.89f);
    public static PageLayout Letter => new(612f, 792f);
    public static PageLayout Legal => new(612f, 1008f);
}
