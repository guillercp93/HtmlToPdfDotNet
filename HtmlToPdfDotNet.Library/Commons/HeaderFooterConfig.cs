namespace HtmlToPdfDotNet.Library.Commons;

/// <summary>
/// Configuration for running headers and footers on PDF pages.
/// Header and footer content can include <c>{page}</c> and <c>{total}</c>
/// placeholders which are substituted with the current page number
/// and total page count when <see cref="ShowPageNumbers"/> is enabled.
/// </summary>
public sealed class HeaderFooterConfig
{
    /// <summary>
    /// HTML template for the running header displayed on every page.
    /// When <see cref="FirstPageHeaderHtml"/> is non-null, this is used
    /// for pages 2+ and the first page uses <see cref="FirstPageHeaderHtml"/> instead.
    /// </summary>
    public string HeaderHtml { get; set; } = string.Empty;

    /// <summary>
    /// HTML template for the running footer displayed on every page.
    /// </summary>
    public string FooterHtml { get; set; } = string.Empty;

    /// <summary>
    /// Optional distinct header for the first page only.
    /// When null, the regular <see cref="HeaderHtml"/> is used on the first page as well.
    /// </summary>
    public string? FirstPageHeaderHtml { get; set; }

    /// <summary>
    /// Reserved height at the top of each page for the header, in points.
    /// Content will avoid this region.
    /// </summary>
    public float HeaderHeight { get; set; }

    /// <summary>
    /// Reserved height at the bottom of each page for the footer, in points.
    /// Content will avoid this region.
    /// </summary>
    public float FooterHeight { get; set; }

    /// <summary>
    /// When true, <c>{page}</c> and <c>{total}</c> placeholders in header/footer
    /// HTML are substituted with the actual page number and total page count.
    /// Defaults to true.
    /// </summary>
    public bool ShowPageNumbers { get; set; } = true;
}
