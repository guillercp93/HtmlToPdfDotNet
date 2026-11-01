using HtmlToPdfDotNet.Example.Models;
using HtmlToPdfDotNet.Example.Services;
using HtmlToPdfDotNet.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HtmlToPdfDotNet.Example.Controllers;

[ApiController]
public class ReportController(IRazorViewRenderer renderer, IPdfGenerator pdfGenerator) : ControllerBase
{
    /// <summary>Rate limiting policy applied to PDF generation (see Program.cs).</summary>
    public const string PdfRateLimitPolicy = "pdf";

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
    [EnableRateLimiting(PdfRateLimitPolicy)]
    public async Task<FileContentResult> GetReportPdf()
    {
        string html = await renderer.RenderToStringAsync("Report/SalesReport", BuildModel());
        byte[] pdfBytes = pdfGenerator.Convert(html);
        return File(pdfBytes, "application/pdf", "SalesReport.pdf");
    }

    private static SalesReportModel BuildModel() => new();
}
