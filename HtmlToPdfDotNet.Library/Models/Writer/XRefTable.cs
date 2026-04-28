using System.Text;

namespace HtmlToPdfDotNet.Library.Models.Writer;

/// <summary>
/// Cross-reference table (xref).
/// Written at the end of the file and allows random access to objects.
/// </summary>
public sealed class XRefTable
{
    private readonly List<PdfObject> _objects = new();

    /// <summary>
    /// Registers a PDF object with the cross-reference table.
    /// </summary>
    /// <param name="obj">The <see cref="PdfObject"/> to register.</param>
    public void Add(PdfObject obj) => _objects.Add(obj);

    /// <summary>
    /// Writes the xref and trailer section.
    /// </summary>
    /// <param name="stream">The output stream to write the xref table to.</param>
    /// <param name="rootObjNumber">The object number of the Root dictionary.</param>
    /// <param name="infoObjNumber">The object number of the Info dictionary (optional).</param>
    /// <returns>The starting offset of the xref table in the stream.</returns>
    public long WriteTo(Stream stream, int rootObjNumber, int infoObjNumber = -1)
    {
        long xrefOffset = stream.Position;

        // Entry 0 is always the free object
        var sb = new StringBuilder();
        sb.AppendLine("xref");
        sb.AppendLine($"0 {_objects.Count + 1}");
        sb.AppendLine("0000000000 65535 f ");   // object 0 free (note: trailing space required)

        // Entries in numerical order
        foreach (var obj in _objects.OrderBy(o => o.Number))
            sb.AppendLine($"{obj.ByteOffset:D10} 00000 n ");

        // Trailer
        sb.AppendLine("trailer");
        sb.AppendLine($"<< /Size {_objects.Count + 1}");
        sb.AppendLine($"   /Root {rootObjNumber} 0 R");
        if (infoObjNumber > 0)
            sb.AppendLine($"   /Info {infoObjNumber} 0 R");
        sb.AppendLine(">>");
        sb.AppendLine("startxref");
        sb.AppendLine(xrefOffset.ToString());
        sb.Append("%%EOF");

        var bytes = Encoding.Latin1.GetBytes(sb.ToString());
        stream.Write(bytes);

        return xrefOffset;
    }
}
