namespace HtmlToPdfDotNet.Library.Models.Layout;

/// <summary>
/// Contains the result of the layout process.
/// </summary>
public sealed class LayoutResult
{
    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int PageCount { get; set; } = 1;

    /// <summary>
    /// Gets the list of render primitives.
    /// </summary>
    public List<RenderPrimitive> Primitives { get; } = new();

    /// <summary>
    /// Gets or sets the total height of the content.
    /// </summary>
    public float TotalHeight { get; set; }

    /// <summary>
    /// Per-page header primitives, indexed by page number.
    /// Each entry contains the primitives for the header of that page.
    /// Null when no header is configured.
    /// </summary>
    public List<List<RenderPrimitive>>? PageHeaders { get; set; }

    /// <summary>
    /// Per-page footer primitives, indexed by page number.
    /// Each entry contains the primitives for the footer of that page.
    /// Null when no footer is configured.
    /// </summary>
    public List<List<RenderPrimitive>>? PageFooters { get; set; }

    /// <summary>
    /// Gets the list of link annotations for the document.
    /// Each annotation defines a clickable region on a specific page.
    /// </summary>
    public List<LinkAnnotationPrimitive> Annotations { get; } = new();

    /// <summary>
    /// Returns an enumerable collection of render primitives for the specified page.
    /// </summary>
    /// <param name="pageIndex">The 0-based index of the page.</param>
    /// <returns>An enumerable collection of render primitives for the specified page.</returns>
    public IEnumerable<RenderPrimitive> ForPage(int pageIndex)
        => Primitives.Where(p => p.PageIndex == pageIndex);
}
