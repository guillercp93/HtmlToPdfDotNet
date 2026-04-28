namespace HtmlToPdfDotNet.Library.Models.Layout;

/// <summary>
/// Defines the dimensions of a page along with its margins.
/// </summary>
public sealed class PageLayout
{
    public float Width { get; }
    public float Height { get; }
    public PageMargins Margins { get; }

    public float ContentWidth => Width - Margins.Left - Margins.Right;

    public float ContentHeight => Height - Margins.Top - Margins.Bottom;

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