using System.Text;

namespace HtmlToPdfDotNet.Library.Models.Writer;

// ─────────────────────────────────────────────────────────────────────────────
// PDF object model
// Each object has an integer number (1-based) and a generation number (0).
// It is serialized as:  N 0 obj\n...\nendobj
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// PDF object with assigned number.  Saves the byte offset once written.
/// </summary>
public class PdfObject
{
    public int Number { get; }
    public long ByteOffset { get; set; } = -1;

    // Raw object content (without "N 0 obj / endobj" wrapper)
    private readonly string _body;

    public PdfObject(int number, string body)
    {
        Number = number;
        _body = body;
    }

    /// <summary>Serializes "N 0 obj\nBODY\nendobj\n".</summary>
    public virtual void WriteTo(Stream stream)
    {
        ByteOffset = stream.Position;
        var bytes = Encoding.Latin1.GetBytes($"{Number} 0 obj\n{_body}\nendobj\n");
        stream.Write(bytes);
    }
}
