# HtmlToPdfDotNet

HtmlToPdfDotNet is a lightweight, fully managed .NET library that converts HTML and CSS into PDF documents. It does not rely on headless browsers (like Chromium) or external system tools (like wkhtmltopdf). Instead, it implements its own CSS styling engine, layout engines, and PDF writer in C#, using HtmlAgilityPack to parse HTML.

---

## Library Overview

### Purpose
The primary goal of HtmlToPdfDotNet is to provide a fully managed solution for generating PDFs from HTML templates within .NET applications, with no native dependencies. It is well suited to invoices, reports, receipts, and other structured documents.

### Main Features
- **Fully Managed:** 100% C# code, no external browser or unmanaged bindings required.
- **CSS Support:** Type, class, id, universal (`*`), compound, and descendant selectors plus selector lists; the box model (margins, borders, padding); inline `style` attributes, `<style>` blocks, and additional stylesheets passed through `ConversionOptions.StyleSheets`.
- **Flexbox Layout:** `display: flex` with `flex-direction`, `flex-wrap`, `justify-content`, `align-items`, `align-content`, `gap`, `flex-grow`, `flex-shrink`, `flex-basis`, and the `flex` shorthand.
- **Font Embedding:** Registered TrueType (`glyf`) fonts are subset so only the used glyphs are embedded; CFF-based OpenType fonts are embedded in full.
- **Images:** Embeds JPEG and PNG images from local files or `data:` URIs.
- **Tables:** `<table>` layouts with `<thead>`, `<tbody>`, `<tfoot>`, and borders.
- **Lists:** `<ul>` and `<ol>` with `list-style-type` (`disc`, `circle`, `square`, `decimal`, `lower-alpha`, `upper-alpha`, `lower-roman`, `upper-roman`, `none`).
- **Links:** `<a href>` elements become clickable PDF link annotations.
- **Headers and Footers:** Running header and footer HTML templates with `{page}` and `{total}` placeholders.
- **Page Breaks:** `page-break-before`, `page-break-after`, and `page-break-inside`.
- **Dependency Injection:** Registration extension for `Microsoft.Extensions.DependencyInjection`.

### Supported .NET Versions
- **.NET 10.0** (Target framework: `net10.0`)

### Typical Use Cases
- Generating automated financial reports (invoices, receipts, ledgers).
- Exporting dashboards and analytics views to PDF.
- Creating standardized legal or medical documents from HTML templates.
- Generating tickets, boarding passes, or printable labels.

---

## Installation

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or newer must be installed on your development machine or build server.

### NuGet Package Manager
You can install the package via the NuGet Package Manager UI in Visual Studio, or by using the following commands:

**Using .NET CLI:**
```bash
dotnet add package HtmlToPdfDotNet --version 1.0.0
```

**Using Package Manager Console (Visual Studio):**
```powershell
Install-Package HtmlToPdfDotNet -Version 1.0.0
```

**Using PackageReference (in `.csproj`):**
```xml
<PackageReference Include="HtmlToPdfDotNet" Version="1.0.0" />
```

---

## How to Use

### Quick Start Example
The simplest way to generate a PDF is by instantiating the generator directly and passing HTML text.

```csharp
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;

// 1. Initialize configuration options
var options = new ConversionOptions
{
    Page = PageLayout.A4,
    CompressStreams = true
};

// 2. Create the generator instance
IPdfGenerator generator = new PdfGenerator(options);

// 3. Define HTML content
string html = @"
    <html>
        <head>
            <style>
                body { font-family: 'Helvetica'; font-size: 12pt; }
                h1 { color: #2c3e50; }
                .content { border: 1px solid #bdc3c7; padding: 10px; }
            </style>
        </head>
        <body>
            <h1>Hello, World!</h1>
            <div class='content'>
                <p>This PDF was generated entirely in .NET.</p>
            </div>
        </body>
    </html>";

// 4. Generate and save the PDF
generator.WritePdfFile(html, "output.pdf");
```

### Dependency Injection (DI) Example
`AddHtmlToPdfDotNet()` registers `PdfGenerator` as a singleton `IPdfGenerator` with default `ConversionOptions` (A4, compressed streams). It has no options overload; to customize options, register the generator yourself with a factory (see the ASP.NET Core example below).

The library only references `Microsoft.Extensions.DependencyInjection.Abstractions`. ASP.NET Core and Worker Service apps already include the full container; a console app needs the `Microsoft.Extensions.DependencyInjection` package to call `BuildServiceProvider()`.

```csharp
using Microsoft.Extensions.DependencyInjection;
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Infrastructure;

ServiceCollection services = new();

// Register the PDF Generator using the extension method
services.AddHtmlToPdfDotNet();

ServiceProvider serviceProvider = services.BuildServiceProvider();

// Resolve and use
IPdfGenerator generator = serviceProvider.GetRequiredService<IPdfGenerator>();
generator.WritePdfFile("<h1>Injected Generator</h1>", "di_output.pdf");
```

### Headers and Footers
Set `ConversionOptions.HeaderFooter` to a `HeaderFooterConfig` to render HTML templates on every page. `{page}` and `{total}` are replaced with the current page number and the total page count while `ShowPageNumbers` is `true` (the default). `FirstPageHeaderHtml`, when set, replaces the header on the first page only.

The header is drawn at the top of the page content area and the footer at its bottom. `PageLayout.ReservedHeaderFooterHeight` shifts body content down to leave room for the header (see [Known Limitations](#known-limitations)).

```csharp
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;

var options = new ConversionOptions
{
    Page = new PageLayout(595.28f, 841.89f, new PageMargins(40f))
    {
        ReservedHeaderFooterHeight = 30f
    },
    HeaderFooter = new HeaderFooterConfig
    {
        FirstPageHeaderHtml = "<h2>Quarterly Report</h2>",
        HeaderHtml = "<p style='font-size: 9pt'>Quarterly Report (continued)</p>",
        FooterHtml = "<p style='text-align: center; font-size: 9pt'>Page {page} of {total}</p>"
    }
};

var generator = new PdfGenerator(options);
generator.WritePdfFile("<p>Report body...</p>", "report-with-footer.pdf");
```

### Custom Fonts
Register TTF/OTF files on `ConversionOptions.Fonts` (a `FontRegistry`) before constructing the generator. The family name is matched case-insensitively against CSS `font-family`; when omitted, the file name without extension is used. Font families that are not registered fall back to the standard PDF fonts (Helvetica, Times, Courier).

```csharp
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;

var options = new ConversionOptions();
options.Fonts.RegisterFont("fonts/Roboto-Regular.ttf", "Roboto");
options.Fonts.RegisterFont("fonts/Roboto-Bold.ttf", "Roboto", bold: true);

// Alternatively, register every *.ttf and *.otf file in a directory (recursively);
// family, bold and italic are detected from the file names.
// options.Fonts.RegisterDirectory("fonts");

var generator = new PdfGenerator(options);
byte[] pdf = generator.Convert("<p style=\"font-family: 'Roboto'\">Custom font</p>");
```

### Important Classes and Interfaces
- **`IPdfGenerator`** (`HtmlToPdfDotNet.Library`): The primary interface. Exposes `Convert(html)` (returns a `byte[]`), `Convert(html, stream)`, `WritePdfFile(html, path)`, and `RunLayout(html)`, which returns the computed `LayoutResult` (page primitives) without writing a PDF.
- **`PdfGenerator`** (`HtmlToPdfDotNet.Library`): The default implementation of `IPdfGenerator`.
- **`ConversionOptions`** (`HtmlToPdfDotNet.Library.Commons`): Configuration for page size and margins (`Page`), stream compression (`CompressStreams`), the base directory for local resources (`BasePath`), additional stylesheets (`StyleSheets`), custom fonts (`Fonts`), and running headers/footers (`HeaderFooter`).
- **`HeaderFooterConfig`** (`HtmlToPdfDotNet.Library.Commons`): Header and footer templates.
- **`PageLayout`** (`HtmlToPdfDotNet.Library.Models.Layout`): Page size and margins, in points. Presets: `PageLayout.A4`, `PageLayout.Letter`, `PageLayout.Legal`. Custom sizes: `new PageLayout(width, height, new PageMargins(top, right, bottom, left))`. The default margins (`PageMargins.Default`) are 56.7pt (about 2 cm) on every side.

### Best Practices
- **Reuse the Generator:** `PdfGenerator` captures a snapshot of `ConversionOptions` when it is constructed, is safe to reuse across concurrent conversions, and should be registered as a singleton. Configure page settings, stylesheets, base paths, and fonts before constructing the generator.
- **Shared Stylesheets:** `ConversionOptions.StyleSheets` accepts entries that are either paths to existing CSS files or raw CSS text, and applies them before the document's own `<style>` blocks. `<link rel="stylesheet">` elements are not processed.
- **Font Size:** Register custom TrueType fonts through `ConversionOptions.Fonts` so they are subset to the glyphs actually used.

### Error Handling Example
Wrap PDF generation in a `try-catch` block, especially when dealing with external HTML input or file I/O operations.

```csharp
try
{
    string htmlContent = "<h1>Invoice #1234</h1>";
    generator.WritePdfFile(htmlContent, "invoice.pdf");
    Console.WriteLine("PDF generated successfully.");
}
catch (UnauthorizedAccessException ex)
{
    Console.Error.WriteLine($"Permission denied while writing PDF: {ex.Message}");
}
catch (Exception ex) // Handles parsing, resource-loading, or rendering errors
{
    Console.Error.WriteLine($"Failed to generate PDF: {ex.Message}");
}
```

---

## Supported HTML and CSS

- **Selectors:** type, class, id, `*`, compound (`p.intro`, `div#main`), descendant (`div p`), and selector lists (`h1, h2`).
- **Display:** `block`, `inline`, `inline-block`, `flex`, `table`, `list-item`, `none`. Unknown values fall back to `block`.
- **Box model:** `width`, `height`, `margin`, `padding`, `border` and their per-side longhands, `border-radius`.
- **Text:** `color`, `font-family`, `font-size`, `font-weight`, `font-style`, `line-height`, `text-align`, `text-decoration`, `text-transform`.
- **Background:** `background`, `background-color`.
- **Flexbox:** see [Main Features](#main-features).
- **Paging:** `page-break-before`, `page-break-after`, `page-break-inside`.
- **Units:** `px` (converted at 1px = 0.75pt), `pt`, `em`, `rem`, `cm`, `mm`, `in`. Percentages are supported for `font-size`, `flex-basis`, and table cell `width` attributes; other lengths given in `%` are treated as zero.

### Known Limitations
- No CSS Grid (`display: grid` falls back to block layout), `float`, or `position`.
- `@media`, `@font-face`, `@import`, and other at-rules are ignored; `!important` is stripped and has no effect on priority.
- No attribute selectors, pseudo-classes, pseudo-elements, or child/sibling combinators.
- Tables do not support `colspan` or `rowspan`.
- No remote resources: images and stylesheets are never fetched over HTTP(S).
- PNG alpha channel is currently not preserved.
- Interlaced (Adam7) PNG images are not supported and cause the conversion to fail.
- CMYK JPEG images are tagged as RGB, so their colors render incorrectly.
- Font families that are not registered fall back to the standard PDF fonts.
- `HeaderFooterConfig.HeaderHeight` and `FooterHeight` are not yet applied by the layout engine, so body content is not automatically kept clear of the footer.

---

## Dependencies

HtmlToPdfDotNet relies on a minimal set of managed packages.

| Package | Version | Purpose |
|---------|---------|---------|
| **HtmlAgilityPack** | `1.12.4` | Parses HTML strings into a navigable DOM tree. Handles malformed HTML gracefully. |
| **Microsoft.Extensions.DependencyInjection.Abstractions** | `10.0.6` | `IServiceCollection` abstractions used by the `AddHtmlToPdfDotNet()` extension. |

*There are no unmanaged dependencies (no native `.dll` or `.so` files required).*

---

## Building from Source

Requires the .NET 10 SDK.

```bash
# Build the solution
dotnet build

# Run the unit tests
dotnet test HtmlToPdfDotNet.Tests

# Run the benchmarks
dotnet run -c Release --project HtmlToPdfDotNet.Benchmarks -- --filter '*'
```

The `HtmlToPdfDotNet.Example` project is an ASP.NET Core sample that renders a Razor view both as HTML and as a PDF (see [ASP.NET Core Integration](#aspnet-core-integration-with-razor-views)).

---

## Code Policies and Contribution Guidelines

### Development Standards
- **Coding Standards:** Follow standard Microsoft C# coding conventions. Use modern C# language features (e.g., file-scoped namespaces, pattern matching, primary constructors).
- **Naming Conventions:**
  - Interfaces: `IPascalCase`
  - Classes/Records/Structs: `PascalCase`
  - Private Fields: `_camelCase`
  - Local Variables/Parameters: `camelCase`
- **Separation of Concerns:** Layout engines (`BlockLayoutEngine`, `InlineLayoutEngine`, `FlexLayoutEngine`, `TableLayoutEngine`) are separated from PDF writing (`PdfDocumentWriter`).
- **Folder Structure:**
  - `HtmlToPdfDotNet.Library/`: Public entry points (`IPdfGenerator`, `PdfGenerator`).
  - `HtmlToPdfDotNet.Library/Commons/`: Public configuration (`ConversionOptions`, `HeaderFooterConfig`), CSS value parsing, helpers, and constants.
  - `HtmlToPdfDotNet.Library/Infrastructure/`: Dependency injection extension (`AddHtmlToPdfDotNet`).
  - `HtmlToPdfDotNet.Library/Models/Layout/`: Layout engines, page layout, and rendering primitives.
  - `HtmlToPdfDotNet.Library/Models/Styles/`: CSS parsing, selector matching, and cascade resolution.
  - `HtmlToPdfDotNet.Library/Models/Writer/`: PDF binary generation (XRefs, streams).
  - `HtmlToPdfDotNet.Library/Models/Fonts/`: TTF/OTF font loading, parsing, and subsetting.
  - `HtmlToPdfDotNet.Library/Models/Imaging/`: Native decoding of JPEG and PNG images and PDF Image XObject generation.

### Process Guidelines
- **Unit Testing:** All new features must include xUnit tests. Aim for high coverage of `HtmlToPdfDotNet.Library`. Internals are visible to `HtmlToPdfDotNet.Tests` (`InternalsVisibleTo`) for testing layout behavior.
- **Error Handling:** Use exceptions for truly exceptional circumstances (e.g., missing files, I/O failures). Use fallback values for unsupported or invalid CSS (e.g., `auto` or default colors).
- **Logging:** Do not write to the console from the library.
- **Performance:** Avoid excessive allocations. Prefer `Span<T>` and `ReadOnlySpan<char>` for text and CSS parsing, and static readonly lookup tables.

### Source Control Strategy
- **Branching:** `Master` contains production-ready code and `develop` is the integration branch. Work happens on feature branches.
- **Pull Requests:** PRs must pass the GitHub Actions CI build and tests (which run for `Master`) and require at least one approving review from a maintainer.
- **Versioning:** We follow [Semantic Versioning (SemVer)](https://semver.org/).

### Security
- HTML input is treated as untrusted. Image `src` values are resolved relative to `ConversionOptions.BasePath` (or the current working directory when it is not set) and confined to that directory; leading slashes and drive roots are stripped, so absolute paths cannot escape it. `data:` URIs are also supported. Remote URLs are never fetched.
- When `BasePath` is set, `WritePdfFile` only writes inside `BasePath`; any other output path throws `UnauthorizedAccessException`.

---

## Advanced Examples

### ASP.NET Core Integration with Razor Views
A practical pattern for generating PDFs in ASP.NET Core is rendering a Razor view (.cshtml) to an HTML string in memory, and then passing it to `HtmlToPdfDotNet`. This lets you use the same template and CSS for both in-browser HTML previewing and PDF generation.

This pattern is implemented in the **[`HtmlToPdfDotNet.Example`](https://github.com/guillercp93/HtmlToPdfDotNet/tree/Master/HtmlToPdfDotNet.Example)** project.

#### 1. Register Services in `Program.cs`
Register the MVC controller views, view-renderer helper, and the generator as a Singleton:
```csharp
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;

var builder = WebApplication.CreateBuilder(args);

// MVC + Razor Views
builder.Services.AddControllersWithViews();

// Register a service to render Razor views to strings (see below)
builder.Services.AddScoped<IRazorViewRenderer, RazorViewRenderer>();

// Register the PDF Generator as a Singleton
builder.Services.AddSingleton<IPdfGenerator>(_ =>
    new PdfGenerator(new ConversionOptions
    {
        Page            = PageLayout.A4,
        CompressStreams = true
    }));
```

#### 2. The Razor View Renderer
Use the standard ASP.NET Core view engine to render templates into raw HTML strings asynchronously:
```csharp
public interface IRazorViewRenderer
{
    Task<string> RenderToStringAsync<TModel>(string viewName, TModel model);
}

public class RazorViewRenderer(
    IRazorViewEngine viewEngine,
    ITempDataProvider tempDataProvider,
    IServiceProvider serviceProvider) : IRazorViewRenderer
{
    public async Task<string> RenderToStringAsync<TModel>(string viewName, TModel model)
    {
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        using var sw = new StringWriter();
        var viewResult = viewEngine.FindView(actionContext, viewName, isMainPage: true);

        if (!viewResult.Success)
            throw new InvalidOperationException($"View '{viewName}' not found.");

        var viewData = new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        var tempData = new TempDataDictionary(httpContext, tempDataProvider);
        var viewContext = new ViewContext(actionContext, viewResult.View, viewData, tempData, sw, new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return sw.ToString();
    }
}
```

#### 3. The Web API Controller
Expose endpoints that either render the HTML in-browser (useful for designing and debugging CSS) or generate a downloadable PDF:
```csharp
[ApiController]
public class ReportController(IRazorViewRenderer renderer, IPdfGenerator pdfGenerator) : ControllerBase
{
    // GET /report — Preview HTML in browser
    [HttpGet("report")]
    [Produces("text/html")]
    public async Task<ContentResult> GetReportHtml()
    {
        var model = new SalesReportModel();
        string html = await renderer.RenderToStringAsync("Report/SalesReport", model);
        return Content(html, "text/html");
    }

    // GET /report/pdf — Download the same report as PDF
    [HttpGet("report/pdf")]
    [Produces("application/pdf")]
    public async Task<FileContentResult> GetReportPdf()
    {
        var model = new SalesReportModel();
        string html = await renderer.RenderToStringAsync("Report/SalesReport", model);

        // Convert to PDF byte array directly
        byte[] pdfBytes = pdfGenerator.Convert(html);

        return File(pdfBytes, "application/pdf", "SalesReport.pdf");
    }
}
```

### Loading and Embedding Images
HtmlToPdfDotNet loads images from local files under the base directory or from Base64 `data:` URIs.

#### Option A: Embedding as Base64 Data URIs (Recommended)
This approach avoids path-resolution issues across environments (development, production containers, etc.).
```csharp
// Inside your View Model
public string LogoDataUri => GetLogoAsBase64();

private string GetLogoAsBase64()
{
    string filePath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "images", "logo.png");
    byte[] bytes = File.ReadAllBytes(filePath);
    return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
}
```
Then, reference the property directly in your `.cshtml` view:
```html
<img src="@Model.LogoDataUri" alt="Company Logo" />
```

#### Option B: Loading via BasePath
Relative image paths (e.g. `<img src="images/logo.png" />`) are resolved against `ConversionOptions.BasePath`, or against the current working directory when it is not set. `BasePath` must be an existing directory. When it is set, the output path passed to `WritePdfFile` must also be inside `BasePath`; alternatively, use `Convert` and write the bytes yourself.
```csharp
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;

string assetsDir = "/var/www/html/assets"; // must exist and contain images/logo.png

var options = new ConversionOptions
{
    BasePath = assetsDir, // Resolves images relative to this directory
    Page = PageLayout.A4
};

var generator = new PdfGenerator(options);
string html = @"<img src='images/logo.png' width='200' />";

// Write inside BasePath...
generator.WritePdfFile(html, Path.Combine(assetsDir, "report.pdf"));

// ...or write anywhere yourself
File.WriteAllBytes("report.pdf", generator.Convert(html));
```

---

## Troubleshooting

### Common Installation Problems
- **Error: `NU1202: Package HtmlToPdfDotNet 1.0.0 is not compatible with net8.0`**
  - **Fix:** This library targets `net10.0` only. Upgrade your project to .NET 10.0 or higher.

### Runtime Issues
- **`FileNotFoundException` or `NotSupportedException` while converting HTML with images**
  - **Fix:** A missing image file throws `FileNotFoundException` and an unsupported image format throws `NotSupportedException`; in both cases the conversion fails. Make sure the `src` is either a `data:` URI or a path relative to `ConversionOptions.BasePath` (or the working directory when `BasePath` is not set), and that the image is a JPEG or non-interlaced PNG.
- **`UnauthorizedAccessException` from `WritePdfFile`**
  - **Fix:** When `BasePath` is set, the output path must be inside `BasePath`. Use `Convert` and write the bytes yourself to save elsewhere.
- **Text overlapping or incorrect font sizes**
  - **Fix:** HTML assumes 96 DPI, while PDF uses 72 DPI. The library scales `px` to `pt` (`1px = 0.75pt`). Use `pt` or `px` consistently in your CSS.

### Debugging Tips
- If the layout looks wrong, set `CompressStreams = false` in `ConversionOptions`. This outputs uncompressed content streams that you can inspect in a text editor (e.g., `BT`, `Tf`, `cm` operators).
- `IPdfGenerator.RunLayout(html)` returns the computed layout primitives without writing a PDF, which is useful for inspecting positions in tests.

---

## FAQ

**Q: Does it support JavaScript execution?**
A: No. HtmlToPdfDotNet is a static HTML/CSS layout engine. It does not execute JavaScript. If you need JS rendering (like React/Angular SPAs), you must pre-render the HTML before passing it to the library.

**Q: Can I use TailwindCSS or Bootstrap?**
A: Only partially. The library supports a subset of CSS: simple selectors, the box model, flexbox, and tables. Utility frameworks rely heavily on unsupported features (media queries, pseudo-classes, grid, positioning), so prefer purpose-written CSS using block, inline-block, flex, and table layouts.

**Q: Why is the generated PDF larger than expected?**
A: Make sure `CompressStreams = true` (the default) is set. CFF-based OpenType fonts are embedded in full; prefer TrueType (`.ttf`) fonts, which are subset to the glyphs used.

---

## License

This project is licensed under the **MIT License with the Commons Clause License Condition v1.0**.

- **MIT License:** You may use, copy, modify, merge, publish and distribute the software, subject to the conditions below.
- **Commons Clause:** You may **NOT** sell the software. "Sell" means providing to third parties, for a fee or other consideration, a product or service whose value derives entirely or substantially from the functionality of the software (for example, a paid "PDF Generation API as a Service", or a paid product whose main value is the PDF conversion this library performs).

This is not an OSI-approved open source license. The summary above is informational only; the [`LICENSE`](https://github.com/guillercp93/HtmlToPdfDotNet/blob/Master/LICENSE) file in the repository root is the full and binding legal text.
