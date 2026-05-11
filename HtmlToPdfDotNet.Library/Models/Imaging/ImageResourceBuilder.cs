using System.Text;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Writer;

namespace HtmlToPdfDotNet.Library.Models.Imaging;

/// <summary>
/// Builder for image resources.
/// </summary>
public sealed class ImageResourceBuilder
{
    private readonly ObjectCounter _counter;
    private readonly XRefTable _xref;
    private readonly bool _compress;

    // Map alias -> object number XObject
    private readonly Dictionary<string, int> _imageObjects = new();

    // XObject objects to be writter
    private readonly List<PdfObject> _objects = new();

    public ImageResourceBuilder(ObjectCounter counter, XRefTable xref, bool compress)
    {
        _counter = counter;
        _xref = xref;
        _compress = compress;
    }

    /// <summary>
    /// Register an image and create its XObject if it doesn't exist.
    /// </summary>
    /// <param name="alias">The alias of the image.</param>
    /// <param name="imageData">The image data.</param>
    /// <returns>The object number of the main XObject (to reference in /Resources).</returns>
    public int RegisterImage(string alias, ImageData imageData)
    {
        if (_imageObjects.TryGetValue(alias, out int existingNum))
        {
            return existingNum;
        }

        int mainObjNum = _counter.Next();
        _imageObjects[alias] = mainObjNum;

        if (imageData.Format == ImageFormat.Jpeg)
        {
            var obj = CreateJpegXObject(mainObjNum, imageData);
            _objects.Add(obj);
            _xref.Add(obj);
        }
        else if (imageData.Format == ImageFormat.Png)
        {
            // if It has alpha, create Smask first
            int? smaskNum = null;
            if (imageData.HasAlpha)
            {
                smaskNum = _counter.Next();
                var smask = CreateAlphaMask(smaskNum.Value, imageData);
                _objects.Add(smask);
                _xref.Add(smask);
            }

            var obj = CreatePngXObject(mainObjNum, imageData, smaskNum);

            _objects.Add(obj);
            _xref.Add(obj);
        }

        return mainObjNum;
    }

    /// <summary>
    /// Return all XObjects objects created by this builder.
    /// </summary>
    /// <returns>A list of <see cref="PdfObject"/> objects.</returns>
    public List<PdfObject> GetObjects() => _objects;

    /// <summary>
    /// Build the XObject dictionary for the resources.
    /// </summary>
    /// <returns>The XObject dictionary as a string.</returns>
    public string BuildImageDict()
    {
        if (_imageObjects.Count == 0) return string.Empty;

        StringBuilder sb = new();
        sb.AppendLine("<< ");

        foreach ((string alias, int objNum) in _imageObjects.OrderBy(kv => kv.Key))
        {
            sb.AppendLine($"/{alias} {objNum} 0 R ");
        }

        sb.AppendLine(">>");

        return sb.ToString();
    }

    /// <summary>
    /// Create a PDF object for a JPEG image.
    /// </summary>
    /// <param name="objNum">The object number.</param>
    /// <param name="img">The image data.</param>
    /// <returns>The PDF object.</returns>
    private PdfObject CreateJpegXObject(int objNum, ImageData img)
    {
        // JPEG is embedded with /DCTDecode
        var header =
            "<< /Type /XObject\n" +
            "   /Subtype /Image\n" +
            $"   /Width {img.PixelWidth}\n" +
            $"   /Height {img.PixelHeight}\n" +
            $"   /ColorSpace {img.ColorSpace}\n" +
            $"   /BitsPerComponent {img.BitsPerComponent}\n" +
            "   /Filter /DCTDecode\n" +
            $"   /Length {img.RawBytes.Length}\n" +
            ">>\nstream\n";

        return new RawStreamPdfObject(objNum, header, img.RawBytes);
    }

    /// <summary>
    /// Create a PDF object for a PNG image.
    /// </summary>
    /// <param name="objNum">The object number.</param>
    /// <param name="img">The image data.</param>
    /// <param name="smaskNum">The optional soft mask object number.</param>
    /// <returns>The PDF object.</returns>
    private PdfObject CreatePngXObject(int objNum, ImageData img, int? smaskNum)
    {
        // Compress pixels RGB/Gray
        byte[] streamBytes = _compress ? Helpers.Deflate(img.RawBytes) : img.RawBytes;
        string filterDecl = _compress ? "\n   /Filter /FlateDecode" : string.Empty;
        string smaskDecl = smaskNum.HasValue ? $"\n   /SMask {smaskNum.Value} 0 R" : string.Empty;

        int components = img.ColorSpace == "/DeviceGray" ? 1 : 3;

        var header =
            "<< /Type /XObject\n" +
            "   /Subtype /Image\n" +
            $"   /Width {img.PixelWidth}\n" +
            $"   /Height {img.PixelHeight}\n" +
            $"   /ColorSpace {img.ColorSpace}\n" +
            $"   /BitsPerComponent {img.BitsPerComponent}{filterDecl}{smaskDecl}\n" +
            $"   /Length {streamBytes.Length}\n" +
            ">>\nstream\n";

        return new RawStreamPdfObject(objNum, header, streamBytes);
    }

    /// <summary>
    /// Create a /SMask (soft mask) object for the alpha channel of a PNG.
    /// </summary>
    private RawStreamPdfObject CreateAlphaMask(int objNum, ImageData img)
    {
        if (img.AlphaBytes == null)
            throw new ArgumentException("ImageData has no alpha channel.", nameof(img));

        byte[] streamBytes = _compress ? Helpers.Deflate(img.AlphaBytes) : img.AlphaBytes;
        string filterDecl = _compress ? "\n   /Filter /FlateDecode" : "";

        var header =
            $"<< /Type /XObject\n" +
            $"   /Subtype /Image\n" +
            $"   /Width {img.PixelWidth}\n" +
            $"   /Height {img.PixelHeight}\n" +
            $"   /ColorSpace /DeviceGray\n" +
            $"   /BitsPerComponent 8{filterDecl}\n" +
            $"   /Length {streamBytes.Length}\n" +
            $">>\nstream\n";

        return new RawStreamPdfObject(objNum, header, streamBytes);
    }
}