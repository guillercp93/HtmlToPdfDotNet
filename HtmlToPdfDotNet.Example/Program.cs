using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHtmlToPdfDotNet();

ConversionOptions options = new() { CompressStreams = false };

// ── Phase 4: Register custom fonts ──────────────────────────────────
string fontPath = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
string fontPath2 = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";
options.Fonts.RegisterFont(fontPath, "DejaVu Sans");
options.Fonts.RegisterFont(fontPath2, "DejaVu Sans", bold: true);
Console.WriteLine($"Registered font: {fontPath}, {fontPath2}");

PdfGenerator generator = new(options);

string html = @"
<h1 style='font-family: ""DejaVu Sans""'>Hello World (Embedded Font)</h1>
<p style='font-family: ""DejaVu Sans""'>This paragraph uses the subsetted DejaVu Sans font.</p>
<div style='border: 1pt solid red; color: blue; font-family: ""DejaVu Sans""'>
    Blue styled text in custom font.
</div>";

bool result = generator.WritePdfFile(html, "./test_embedded_font.pdf");

if (result)
    Console.WriteLine("PDF generated successfully.");
else
    Console.WriteLine("PDF generation failed.");

