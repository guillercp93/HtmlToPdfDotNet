namespace HtmlToPdfDotNet.Library.Models.Writer;

/// <summary>
/// Helper class to manage object numbers in PDF.
/// </summary>
public sealed class ObjectCounter
{
    private int _next = 1;

    /// <summary>
    /// Gets the next object number.
    /// </summary>
    public int Next() => _next++;

    /// <summary>
    /// Gets the next object number without incrementing the counter.
    /// </summary>
    public int Peek => _next;
}