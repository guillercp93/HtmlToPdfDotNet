using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;

namespace HtmlToPdfDotNet.Library.Models.Writer;

/// ─────────────────────────────────────────────────────────────────────
/// Structure of the generated PDF:
/// ┌────────────────────────────────────────────────────┐
/// │  %PDF-1.7                                          │
/// │  %âãÏÓ  (binario hint)                             │
/// │                                                    │
/// │  1 0 obj  Catalog                                  │
/// │  2 0 obj  Info                                     │
/// │  3 0 obj  Pages                                    │
/// │  4..4+N   Font objects (12 standard fonts)        │
/// │  For each page:                                   │
/// │    N 0 obj  Page                                   │
/// │    N+1 0 obj  Content stream                       │
/// │                                                    │
/// │  xref                                              │
/// │  trailer                                           │
/// │  %%EOF                                             │
/// └────────────────────────────────────────────────────┘
/// ──────────────────────────────────────────────────────────────────────

/// <summary>
/// Serializes a <see cref="LayoutResult"/> into a valid PDF 1.7 file.
/// </summary>
public sealed class PdfDocumentWriter
{
    private readonly PageLayout _page;
    private readonly bool _compress;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDocumentWriter"/> class.
    /// </summary>
    /// <param name="page">The page layout configuration (size, margins, etc.).</param>
    /// <param name="compressStreams">Whether to compress page content streams using FlateDecode.</param>
    public PdfDocumentWriter(PageLayout page, bool compressStreams = true)
    {
        _page = page;
        _compress = compressStreams;
    }

    /// <summary>
    /// Writes the layout result as a PDF document to the specified output stream.
    /// </summary>
    /// <param name="layout">The layout result containing pages and primitives.</param>
    /// <param name="output">The output stream where the PDF will be written.</param>
    public void Write(LayoutResult layout, Stream output)
    {
        var counter = new ObjectCounter();
        var xref = new XRefTable();

        // Reserve object numbers for fixed objects
        int catalogNum = counter.Next(); // 1
        int infoNum = counter.Next(); // 2
        int pagesNum = counter.Next(); // 3

        // Create fonts
        var fontBuilder = new FontResourceBuilder(counter, xref);
        var fontObjects = fontBuilder.CreateFontObjects();

        // Prepare page objects
        var pageObjectNums = new List<int>();
        var pageContentPairs = new List<(PdfObject PageObj, PdfObject ContentObj)>();

        for (int i = 0; i < layout.PageCount; i++)
        {
            int contentNum = counter.Next();
            int pageNum = counter.Next();
            pageObjectNums.Add(pageNum);

            var primitives = layout.ForPage(i).ToList();

            // Content stream
            var csBuilder = new ContentStreamBuilder(_page.Height);
            foreach (var prim in primitives)
            {
                switch (prim)
                {
                    case RectPrimitive r: csBuilder.DrawRect(r); break;
                    case BorderLinePrimitive b: csBuilder.DrawBorderLine(b); break;
                    case TextPrimitive t: csBuilder.DrawText(t); break;
                }
            }

            byte[] streamBytes = csBuilder.Build(_compress);
            string filterDecl = _compress ? "\n   /Filter /FlateDecode" : "";

            var contentBody = $"<< /Length {streamBytes.Length}{filterDecl} >>\nstream\n";
            var contentObj = new RawStreamPdfObject(contentNum, contentBody, streamBytes);
            xref.Add(contentObj);

            // Page dictionary
            string fontDict = fontBuilder.BuildFontDict();
            string pageBody =
                $"<< /Type /Page\n" +
                $"   /Parent {pagesNum} 0 R\n" +
                $"   /MediaBox [0 0 {Helpers.F(_page.Width)} {Helpers.F(_page.Height)}]\n" +
                $"   /Resources << /Font {fontDict} >>\n" +
                $"   /Contents {contentNum} 0 R\n" +
                $">>";
            var pageObj = new PdfObject(pageNum, pageBody);
            xref.Add(pageObj);

            pageContentPairs.Add((pageObj, contentObj));
        }

        // Pages object
        string kidsArray = string.Join(" ", pageObjectNums.Select(n => $"{n} 0 R"));
        var pagesObj = new PdfObject(pagesNum,
            $"<< /Type /Pages\n" +
            $"   /Kids [{kidsArray}]\n" +
            $"   /Count {layout.PageCount}\n" +
            $">>");
        xref.Add(pagesObj);

        // Info object
        var now = DateTime.UtcNow;
        string date = $"D:{now:yyyyMMddHHmmss}Z";
        var infoObj = new PdfObject(infoNum,
            $"<< /Producer (HtmlToPdf .NET 10)\n" +
            $"   /CreationDate ({date})\n" +
            $">>");
        xref.Add(infoObj);

        // Catalog object
        var catalogObj = new PdfObject(catalogNum,
            $"<< /Type /Catalog\n" +
            $"   /Pages {pagesNum} 0 R\n" +
            $">>");
        xref.Add(catalogObj);

        // Header
        Helpers.WriteRaw(output, "%PDF-1.7\n%\xE2\xE3\xCF\xD3\n\n");

        // Fonts
        foreach (var fo in fontObjects) fo.WriteTo(output);

        // Content/Page pairs
        foreach (var (pageObj, contentObj) in pageContentPairs)
        {
            contentObj.WriteTo(output);
            pageObj.WriteTo(output);
        }

        // Pages, Info, Catalog
        pagesObj.WriteTo(output);
        infoObj.WriteTo(output);
        catalogObj.WriteTo(output);

        // XRef + Trailer
        xref.WriteTo(output, rootObjNumber: catalogNum, infoObjNumber: infoNum);
    }

    /// <summary>
    /// Generates the PDF document and returns it as a byte array.
    /// </summary>
    /// <param name="layout">The layout result containing pages and primitives.</param>
    /// <returns>A byte array containing the full PDF document.</returns>
    public byte[] ToBytes(LayoutResult layout)
    {
        using var ms = new MemoryStream();
        Write(layout, ms);
        return ms.ToArray();
    }
}
