using System.Text;

namespace HtmlToPdfDotNet.Library.Models.Writer;

/// <summary>
/// Builds PDF font objects and font dictionaries.
/// </summary>
public sealed class FontResourceBuilder
{
    // Map each alias (F1..F12) → PDF object number
    private readonly Dictionary<string, int> _fontObjectNumbers = new();
    private readonly ObjectCounter _counter;
    private readonly XRefTable _xref;

    // All possible aliases in order
    private static readonly (string Alias, string PdfName)[] AllFonts =
    [
        ("F1",  "Helvetica"),
        ("F2",  "Helvetica-Bold"),
        ("F3",  "Helvetica-Oblique"),
        ("F4",  "Helvetica-BoldOblique"),
        ("F5",  "Times-Roman"),
        ("F6",  "Times-Bold"),
        ("F7",  "Times-Italic"),
        ("F8",  "Times-BoldItalic"),
        ("F9",  "Courier"),
        ("F10", "Courier-Bold"),
        ("F11", "Courier-Oblique"),
        ("F12", "Courier-BoldOblique"),
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="FontResourceBuilder"/> class.
    /// </summary>
    /// <param name="counter">The object counter used to assign unique PDF object numbers.</param>
    /// <param name="xref">The cross-reference table where new font objects will be registered.</param>
    public FontResourceBuilder(ObjectCounter counter, XRefTable xref)
    {
        _counter = counter;
        _xref = xref;
    }

    /// <summary>
    /// Creates the PDF font objects for all standard fonts and registers them in the cross-reference table.
    /// </summary>
    /// <returns>A list of <see cref="PdfObject"/> representing the standard fonts.</returns>
    public List<PdfObject> CreateFontObjects()
    {
        var objects = new List<PdfObject>();

        foreach (var (alias, pdfName) in AllFonts)
        {
            var num = _counter.Next();
            _fontObjectNumbers[alias] = num;

            var body = $"<< /Type /Font\n   /Subtype /Type1\n   /BaseFont /{pdfName}\n   /Encoding /WinAnsiEncoding\n>>";
            var obj = new PdfObject(num, body);
            objects.Add(obj);
            _xref.Add(obj);
        }

        return objects;
    }

    /// <summary>
    /// Generates the font dictionary string for the /Resources section of a PDF page.
    /// This dictionary maps font aliases (e.g., /F1) to their respective object references.
    /// </summary>
    /// <returns>A string representing the PDF font dictionary.</returns>
    public string BuildFontDict()
    {
        var sb = new StringBuilder();
        sb.Append("<< ");
        foreach (var (alias, _) in AllFonts)
        {
            if (_fontObjectNumbers.TryGetValue(alias, out var num))
                sb.Append($"/{alias} {num} 0 R ");
        }
        sb.Append(">>");
        return sb.ToString();
    }

    /// <summary>
    /// Identifies the font aliases used in a collection of text primitives.
    /// </summary>
    /// <param name="texts">The collection of text primitives to analyze.</param>
    /// <returns>An enumerable of unique font aliases used by the primitives.</returns>
    public static IEnumerable<string> UsedAliases(IEnumerable<Layout.TextPrimitive> texts)
        => texts.Select(t => ContentStreamBuilder.FontAlias(t.FontName)).Distinct();
}
