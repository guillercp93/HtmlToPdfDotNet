using HtmlToPdfDotNet.Example.Models;
using HtmlToPdfDotNet.Example.Services;
using HtmlToPdfDotNet.Library;
using Microsoft.AspNetCore.Mvc;

namespace HtmlToPdfDotNet.Example.Controllers;

[ApiController]
public class ReportController(IRazorViewRenderer renderer, IPdfGenerator pdfGenerator) : ControllerBase
{
    /// <summary>
    /// Returns the sales report rendered as an HTML page.
    /// </summary>
    [HttpGet("report")]
    [Produces("text/html")]
    public async Task<ContentResult> GetReportHtml()
    {
        string html = await renderer.RenderToStringAsync("Report/SalesReport", BuildModel());
        return Content(html, "text/html");
    }

    /// <summary>
    /// Returns the sales report rendered as a downloadable PDF file.
    /// </summary>
    [HttpGet("report/pdf")]
    [Produces("application/pdf")]
    public async Task<FileContentResult> GetReportPdf()
    {
        string html = await renderer.RenderToStringAsync("Report/SalesReport", BuildModel());
        byte[] pdfBytes = pdfGenerator.Convert(html);
        return File(pdfBytes, "application/pdf", "SalesReport.pdf");
    }

    private static SalesReportModel BuildModel() => new();
}
