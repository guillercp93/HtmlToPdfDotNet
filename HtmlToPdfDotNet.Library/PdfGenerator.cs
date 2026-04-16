using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Models;

namespace HtmlToPdfDotNet.Library;

public class PdfGenerator
{
    public string GeneratePdf(string htmlContent)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);
        new StyleResolver().Resolve(doc.DocumentNode);
        // This is a placeholder for actual PDF generation logic
        return $"PDF generated for: {htmlContent}";
    }
}
