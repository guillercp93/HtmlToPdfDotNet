namespace HtmlToPdfDotNet.Library.Models.Layout;

/// <summary>
/// Defines the margins of a page in points.
/// </summary>
public sealed class PageMargins
{
    public static readonly PageMargins Default = new(top: 56.7f, right: 56.7f, bottom: 56.7f, left: 56.7f); // ~2 cm

    public float Top { get; }
    public float Right { get; }
    public float Bottom { get; }
    public float Left { get; }

    public PageMargins(float top, float right, float bottom, float left)
    {
        Top = top; Right = right; Bottom = bottom; Left = left;
    }

    public PageMargins(float all) : this(all, all, all, all) { }
}