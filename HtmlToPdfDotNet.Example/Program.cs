using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHtmlToPdfDotNet();

ConversionOptions options = new() { CompressStreams = false };
PdfGenerator generator = new(options);

string html = "<h1>Hello World</h1><p>This is a test paragraph.</p><div style='font-size: 24px; color: blue;'>Blue styled text</div>";

bool result = generator.WritePdfFile(html, "./test.pdf");

if (result)
    Console.WriteLine("PDF generated successfully.");
else
    Console.WriteLine("PDF generation failed.");

