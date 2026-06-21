using HtmlToPdfDotNet.Example.Services;
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ── MVC + Razor Views ─────────────────────────────────────────────────────
builder.Services
    .AddControllersWithViews()
    .AddRazorRuntimeCompilation();   // enables .cshtml hot-reload in development

// ── Application services ──────────────────────────────────────────────────
builder.Services.AddScoped<IRazorViewRenderer, RazorViewRenderer>();

// ── HtmlToPdfDotNet ───────────────────────────────────────────────────────
builder.Services.AddSingleton<IPdfGenerator>(_ =>
{
    ConversionOptions opts = new()
    {
        Page = HtmlToPdfDotNet.Library.Models.Layout.PageLayout.A4,
        CompressStreams = true,
    };

    // Register system font for emoji/symbol rendering
    string symbolsFont = "/usr/share/fonts/noto/NotoSansSymbols2-Regular.ttf";
    if (File.Exists(symbolsFont))
    {
        opts.Fonts.RegisterFont(symbolsFont, familyName: "Noto Sans Symbols 2");
    }

    return new PdfGenerator(opts);
});

// ── Build & configure pipeline ────────────────────────────────────────────
WebApplication app = builder.Build();

app.UseStaticFiles();
app.MapControllers();

// Landing page — quick help for the developer
app.MapGet("/", () => Results.Content("""
    <html><body style="font-family:sans-serif;padding:2rem">
      <h1>HtmlToPdfDotNet.Example</h1>
      <ul>
        <li><a href="/report">GET /report</a> — view the sales report as HTML</li>
        <li><a href="/report/pdf">GET /report/pdf</a> — download the same report as PDF</li>
      </ul>
    </body></html>
    """, "text/html"));

app.Run();
