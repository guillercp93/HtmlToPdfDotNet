using System.Text;

namespace HtmlToPdfDotNet.Library.Models.Writer;

/// <summary>
/// Represents a PDF object that contains a binary stream (e.g., page content, images).
/// </summary>
public sealed class RawStreamPdfObject : PdfObject
{
    private readonly string _dictHeader;
    private readonly byte[] _streamBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="RawStreamPdfObject"/> class.
    /// </summary>
    /// <param name="number">The unique PDF object number.</param>
    /// <param name="dictHeader">The dictionary header string for the stream object.</param>
    /// <param name="streamBytes">The raw binary data of the stream.</param>
    public RawStreamPdfObject(int number, string dictHeader, byte[] streamBytes)
        : base(number, "") // ignored body
    {
        _dictHeader = dictHeader;
        _streamBytes = streamBytes;
    }

    /// <summary>
    /// Serializes the stream object to the specified output stream.
    /// </summary>
    /// <param name="stream">The output stream to write to.</param>
    public override void WriteTo(Stream stream)
    {
        ByteOffset = stream.Position;

        // "N 0 obj\n<dict>\nstream\n"
        byte[] header = Encoding.Latin1.GetBytes($"{Number} 0 obj\n{_dictHeader}");
        stream.Write(header);

        // Binary data of the stream
        stream.Write(_streamBytes);

        // "\nendstream\nendobj\n"
        byte[] footer = Encoding.Latin1.GetBytes("\nendstream\nendobj\n");
        stream.Write(footer);
    }
}
