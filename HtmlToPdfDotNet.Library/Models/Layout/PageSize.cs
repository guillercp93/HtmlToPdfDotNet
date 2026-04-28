namespace HtmlToPdfDotNet.Library.Models.Layout;

/// <summary>
/// Defines the dimensions of a page in points.
/// </summary>
public static class PageSize
{
    public static readonly (float Width, float Height) A4 = (595.28f, 841.89f);
    public static readonly (float Width, float Height) Letter = (612f, 792f);
    public static readonly (float Width, float Height) Legal = (612f, 1008f);
}