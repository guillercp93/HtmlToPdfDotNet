using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// ── Phase 1: Configure options ──────────────────────────────────────
ConversionOptions options = new() { CompressStreams = false };
string fontPath = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
string fontPath2 = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";

// Register fonts if they exist (to avoid errors on different systems)
if (File.Exists(fontPath)) options.Fonts.RegisterFont(fontPath, "DejaVu Sans");
if (File.Exists(fontPath2)) options.Fonts.RegisterFont(fontPath2, "DejaVu Sans", bold: true);

// ── Phase 2: Register Services ──────────────────────────────────────
services.AddSingleton(options);
services.AddHtmlToPdfDotNet();

var serviceProvider = services.BuildServiceProvider();

// ── Phase 3: Use the interface ──────────────────────────────────────
// Resolve the interface instead of the concrete class
IPdfGenerator generator = serviceProvider.GetRequiredService<IPdfGenerator>();

string html = @"
<h1 style='font-family: ""DejaVu Sans""'>Hello World (via Interface)</h1>
<p style='font-family: ""DejaVu Sans""'>This PDF was generated using IPdfGenerator resolved from DI.</p>
<div style='border: 1pt solid green; color: darkgreen; font-family: ""DejaVu Sans""'>
    The library is now decoupled via interfaces!
</div>";

bool result = generator.WritePdfFile(html, "./test_interface.pdf");

if (result)
    Console.WriteLine("PDF generated successfully via IPdfGenerator.");
else
    Console.WriteLine("PDF generation failed.");

