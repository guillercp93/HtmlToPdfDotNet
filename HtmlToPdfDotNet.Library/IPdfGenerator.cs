using System.IO;

namespace HtmlToPdfDotNet.Library;

/// <summary>
/// Defines the contract for the PDF generator.
/// </summary>
public interface IPdfGenerator
{
    /// <summary>
    /// Converts an HTML string into a PDF byte array.
    /// </summary>
    /// <param name="html">The raw HTML content to convert.</param>
    /// <returns>A byte array representing the generated PDF.</returns>
    byte[] Convert(string html);

    /// <summary>
    /// Converts an HTML string and writes the resulting PDF to the specified output stream.
    /// </summary>
    /// <param name="html">The raw HTML content to convert.</param>
    /// <param name="output">The destination stream for the PDF data.</param>
    void Convert(string html, Stream output);

    /// <summary>
    /// Tries to write the converted PDF to the specified path, returning true if successful, false otherwise.
    /// </summary>
    /// <param name="html">The raw HTML content to convert.</param>
    /// <param name="pdfPath">The destination path for the generated PDF file.</param>
    /// <returns>True if the PDF was successfully written, false otherwise.</returns>
    bool WritePdfFile(string html, string pdfPath);
}
