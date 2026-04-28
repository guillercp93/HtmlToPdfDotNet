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

    public IEnumerable<RenderPrimitive> ForPage(int pageIndex)
        => Primitives.Where(p => p.PageIndex == pageIndex);
}