using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Fonts;
using HtmlToPdfDotNet.Library.Models.Imaging;
using HtmlToPdfDotNet.Library.Models.Layout;

namespace HtmlToPdfDotNet.Library.Models.Writer;

/// ─────────────────────────────────────────────────────────────────────
/// Structure of the generated PDF:
/// ┌────────────────────────────────────────────────────┐
/// │  %PDF-1.7                                          │
/// │  %âãÏÓ  (binary hint)                              │
/// │                                                    │
/// │  1 0 obj  Catalog                                  │
/// │  2 0 obj  Info                                     │
/// │  3 0 obj  Pages                                    │
/// │  4..4+N   Font objects                             │
/// │           ├─ Standard Type1  (F1..F12)             │
/// │           └─ Embedded Type0  (FE0, FE1, …)         │
/// │               ├─ FontFile2/3 stream (subset)       │
/// │               ├─ FontDescriptor                    │
/// │               ├─ ToUnicode CMap                    │
/// │               ├─ CIDFontType2                      │
/// │               └─ Type0 composite font              │
/// │  For each page:                                    │
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
///
/// Phase 4 additions:
///   • Performs a pre-pass over all <see cref="TextPrimitive"/> objects to
///     discover which embedded TTF/OTF fonts and which glyph IDs are used.
///   • Passes the resulting <see cref="EmbeddedFontInfo"/> alias map to each
///     <see cref="ContentStreamBuilder"/> so it can emit GID hex strings for
///     embedded fonts and Latin-1 strings for standard fonts.
///   • Emits per-page header and footer primitives (when present in the
///     layout result) before and after page content respectively.
/// </summary>
public sealed class PdfDocumentWriter
{
    /// <summary>
    /// Page layout configuration.
    /// </summary>
    private readonly PageLayout _page;

    /// <summary>
    /// Compression flag.
    /// </summary>
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
    /// <param name="layout">The layout result containing pages and render primitives.</param>
    /// <param name="output">The output stream where the PDF will be written.</param>
    public void Write(LayoutResult layout, Stream output)
    {
        ObjectCounter counter = new();
        XRefTable xref = new();

        // ── Reserve fixed object numbers ───────────────────────────────────
        int catalogNum = counter.Next(); // 1
        int infoNum = counter.Next(); // 2
        int pagesNum = counter.Next(); // 3

        // ── Font resource builder ────────────────────────────────────────
        FontResourceBuilder fontBuilder = new(counter, xref);
        List<TextPrimitive> allText = layout.Primitives.OfType<TextPrimitive>().ToList();

        // Include header/footer text primitives in font analysis
        if (layout.PageHeaders != null)
            foreach (List<RenderPrimitive> headerPage in layout.PageHeaders)
                allText.AddRange(headerPage.OfType<TextPrimitive>());
        if (layout.PageFooters != null)
            foreach (List<RenderPrimitive> footerPage in layout.PageFooters)
                allText.AddRange(footerPage.OfType<TextPrimitive>());

        fontBuilder.Analyze(allText);

        // ── Create all font PDF objects ────────────────────────────────────
        List<PdfObject> fontObjects = fontBuilder.CreateFontObjects();

        // ── Image resource builder ────────────────────────────────────────
        ImageResourceBuilder imageBuilder = new(counter, xref, _compress);
        List<PdfObject> imagesObjects = imageBuilder.GetObjects();

        // Alias map: passed to each ContentStreamBuilder so it knows
        // which TextPrimitives need GID-hex encoding.
        IReadOnlyDictionary<EmbeddedFontInfo, string> embeddedAliases = fontBuilder.EmbeddedAliases;

        // ── Build page object pairs ────────────────────────────────────────
        List<int> pageObjectNums = new();
        List<(PdfObject PageObj, PdfObject ContentObj)> pageContentPairs = new();

        for (int i = 0; i < layout.PageCount; i++)
        {
            int contentNum = counter.Next();
            int pageNum = counter.Next();
            pageObjectNums.Add(pageNum);

            ContentStreamBuilder csBuilder = new(_page.Height, embeddedAliases);

            // 1. Emit header primitives (if any) for this page
            if (layout.PageHeaders != null && i < layout.PageHeaders.Count)
            {
                foreach (RenderPrimitive prim in layout.PageHeaders[i])
                    EmitPrimitive(csBuilder, prim, imageBuilder);
            }

            // 2. Emit page content primitives
            foreach (RenderPrimitive prim in layout.ForPage(i))
            {
                EmitPrimitive(csBuilder, prim, imageBuilder);
            }

            // 3. Emit footer primitives (if any) for this page
            if (layout.PageFooters != null && i < layout.PageFooters.Count)
            {
                foreach (RenderPrimitive prim in layout.PageFooters[i])
                    EmitPrimitive(csBuilder, prim, imageBuilder);
            }

            byte[] streamBytes = csBuilder.Build(_compress);
            string filterDecl = _compress ? "\n   /Filter /FlateDecode" : "";

            string contentBody = $"<< /Length {streamBytes.Length}{filterDecl} >>\nstream\n";
            RawStreamPdfObject contentObj = new(contentNum, contentBody, streamBytes);
            xref.Add(contentObj);

            // Page dictionary
            string fontDict = fontBuilder.BuildFontDict();
            string imgDict = imageBuilder.BuildImageDict();
            string xobjRes = string.IsNullOrEmpty(imgDict) ? "" : $"/XObject {imgDict}";

            string pageBody =
                $"<< /Type /Page\n" +
                $"   /Parent {pagesNum} 0 R\n" +
                $"   /MediaBox [0 0 {Helpers.F(_page.Width)} {Helpers.F(_page.Height)}]\n" +
                $"   /Resources << /Font {fontDict} {xobjRes} >>\n" +
                $"   /Contents {contentNum} 0 R\n" +
                $">>";
            PdfObject pageObj = new(pageNum, pageBody);
            xref.Add(pageObj);

            pageContentPairs.Add((pageObj, contentObj));
        }

        // ── Pages object ───────────────────────────────────────────────────
        string kidsArray = string.Join(" ", pageObjectNums.Select(n => $"{n} 0 R"));
        PdfObject pagesObj = new(pagesNum,
            $"<< /Type /Pages\n" +
            $"   /Kids [{kidsArray}]\n" +
            $"   /Count {layout.PageCount}\n" +
            $">>");
        xref.Add(pagesObj);

        // ── Info object ────────────────────────────────────────────────────
        DateTime now = DateTime.UtcNow;
        string date = $"D:{now:yyyyMMddHHmmss}Z";
        PdfObject infoObj = new(infoNum,
            $"<< /Producer (HtmlToPdf .NET)\n" +
            $"   /CreationDate ({date})\n" +
            $">>");
        xref.Add(infoObj);

        // ── Catalog object ─────────────────────────────────────────────────
        PdfObject catalogObj = new(catalogNum,
            $"<< /Type /Catalog\n" +
            $"   /Pages {pagesNum} 0 R\n" +
            $">>");
        xref.Add(catalogObj);

        // ── Write to stream ────────────────────────────────────────────────
        Helpers.WriteRaw(output, "%PDF-1.7\n%\xE2\xE3\xCF\xD3\n\n");

        foreach (PdfObject fo in fontObjects) fo.WriteTo(output);
        foreach (PdfObject io in imagesObjects) io.WriteTo(output);

        foreach ((PdfObject pageObj, PdfObject contentObj) in pageContentPairs)
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
    /// <param name="layout">The layout result containing pages and render primitives.</param>
    /// <returns>A byte array containing the full PDF document.</returns>
    public byte[] ToBytes(LayoutResult layout)
    {
        using MemoryStream ms = new();
        Write(layout, ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Dispatches a render primitive to the appropriate <see cref="ContentStreamBuilder"/> method.
    /// </summary>
    /// <param name="csBuilder">The content stream builder.</param>
    /// <param name="prim">The render primitive to emit.</param>
    /// <param name="imageBuilder">The image resource builder.</param>
    private static void EmitPrimitive(ContentStreamBuilder csBuilder,
                                      RenderPrimitive prim,
                                      ImageResourceBuilder imageBuilder)
    {
        switch (prim)
        {
            case RectPrimitive r:
                csBuilder.DrawRect(r);
                break;
            case BorderLinePrimitive b:
                csBuilder.DrawBorderLine(b);
                break;
            case TextPrimitive t:
                csBuilder.DrawText(t);
                break;
            case ImagePrimitive img:
                csBuilder.DrawImage(img);
                if (img.ImageData != null)
                    imageBuilder.RegisterImage(img.XObjectAlias, img.ImageData);
                break;
        }
    }
}
