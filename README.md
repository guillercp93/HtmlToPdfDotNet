# HtmlToPdfDotNet

HtmlToPdfDotNet is a fast, lightweight, and completely native .NET library designed to convert HTML and CSS into high-quality PDF documents. It does not rely on headless browsers (like Chromium) or external system dependencies (like wkhtmltopdf). Instead, it implements a custom HTML parser, CSS styling engine, and PDF writer directly in C# to provide maximum performance and lower memory usage.

---

## Library Overview

### Purpose
The primary goal of HtmlToPdfDotNet is to provide a fully managed, dependency-free solution for generating PDFs from HTML templates within .NET applications. It is ideal for generating invoices, reports, receipts, and other structured documents.

### Main Features
- **Fully Managed:** 100% C# code, no external browser or unmanaged bindings required.
- **Advanced CSS Support:** Supports modern CSS selectors, the standard box model (margins, borders, padding), inline styles, and external stylesheets.
- **Font Subsetting:** Embeds only the required glyphs from TTF/OTF fonts to significantly reduce PDF file sizes.
- **Rich Media Support:** Embeds JPEG and PNG images natively, preserving aspect ratios and transparency.
- **Table Layouts:** Comprehensive support for complex `<table>` layouts, including spanning and borders.
- **Dependency Injection:** First-class support for `Microsoft.Extensions.DependencyInjection`.

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
dotnet add package HtmlToPdfDotNet.Library --version 1.0.0
```

**Using Package Manager Console (Visual Studio):**
```powershell
Install-Package HtmlToPdfDotNet.Library -Version 1.0.0
```

**Using PackageReference (in `.csproj`):**
```xml
<PackageReference Include="HtmlToPdfDotNet.Library" Version="1.0.0" />
```

---

## How to Use

### Quick Start Example
The simplest way to generate a PDF is by instantiating the generator directly and passing HTML text.

```csharp
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;

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
HtmlToPdfDotNet integrates seamlessly with modern .NET DI containers.

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

### Important Classes and Interfaces
- **`IPdfGenerator`**: The primary interface. Exposes methods like `Convert(html)`, `Convert(html, stream)`, and `WritePdfFile(html, path)`.
- **`PdfGenerator`**: The default implementation of `IPdfGenerator`.
- **`ConversionOptions`**: Configuration class allowing customization of page sizes (`PageLayout`), base paths for resources (`BasePath`), external stylesheets (`StyleSheets`), and custom fonts (`FontRegistry`).
- **`PageLayout`**: Defines the physical page size (e.g., `PageLayout.A4`, `PageLayout.Letter`) and document margins.

### Best Practices
- **Reuse the Generator:** The `PdfGenerator` class is thread-safe and should be registered as a Singleton to prevent redundant memory allocations.
- **Use External Stylesheets:** Instead of large `<style>` blocks, pass pre-compiled CSS files via `ConversionOptions.StyleSheets` for faster parsing.
- **Font Subsetting:** When using custom TTF fonts, explicitly register them using the `FontRegistry` to ensure only used glyphs are embedded, drastically reducing output file size.

### Error Handling Example
Always wrap PDF generation in a `try-catch` block, especially when dealing with external HTML input or file I/O operations.

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
catch (Exception ex) // Handles parsing or rendering errors
{
    Console.Error.WriteLine($"Failed to generate PDF: {ex.Message}");
}
```

---

## Dependencies

HtmlToPdfDotNet is designed to be lightweight, relying on minimal external packages.

| Package | Version | Required | Purpose |
|---------|---------|----------|---------|
| **HtmlAgilityPack** | `1.12.4+` | Yes | Used for robust parsing of HTML strings into a navigable DOM tree. Handles malformed HTML gracefully. |
| **Microsoft.Extensions.DependencyInjection** | `10.0.6+` | Yes | Provides core abstractions for the native DI container implementation, enabling seamless integration with ASP.NET Core and Worker Services. |

*There are no transitive unmanaged dependencies (no native `.dll` or `.so` files required).*

---

## Code Policies and Contribution Guidelines

We enforce strict engineering standards to ensure high performance and maintainability.

### Development Standards
- **Coding Standards:** Follow standard Microsoft C# coding conventions. Use C# 10.0+ language features (e.g., file-scoped namespaces, pattern matching, global usings).
- **Naming Conventions:**
  - Interfaces: `IPascalCase`
  - Classes/Records/Structs: `PascalCase`
  - Private Fields: `_camelCase`
  - Local Variables/Parameters: `camelCase`
- **SOLID Principles:** Architecture is highly decoupled. Layout engines (`BlockLayoutEngine`, `TableLayoutEngine`) are strictly separated from PDF writing constructs (`PdfDocumentWriter`).
- **Folder Structure:**
  - `HtmlToPdfDotNet.Library/Commons/`: Static helpers and constants.
  - `HtmlToPdfDotNet.Library/Models/Layout/`: Layout tree and rendering primitives.
  - `HtmlToPdfDotNet.Library/Models/Styles/`: CSS parsing and cascade resolution.
  - `HtmlToPdfDotNet.Library/Models/Writer/`: PDF binary generation (XRefs, streams).
  - `HtmlToPdfDotNet.Library/Models/Fonts/`: Custom TTF/OTF font loading, parsing, and subsetting to minimize PDF size.
  - `HtmlToPdfDotNet.Library/Models/Imaging/`: Native decoding of JPEG and PNG images and PDF Image XObject generation.

### Process Guidelines
- **Unit Testing:** All new features must include xUnit tests. Code coverage for `HtmlToPdfDotNet.Library` must remain above 85%. Use `InternalsVisibleTo` to test internal layout behaviors.
- **Error Handling:** Use exceptions only for truly exceptional circumstances (e.g., missing files, I/O failures). Use standard fallback mechanisms for CSS parsing errors (e.g., reverting to `auto` or default colors).
- **Logging:** Do not use `Console.WriteLine` in the library. If diagnostics are required, inject an `ILogger<T>` instance.
- **Performance:** Avoid excessive allocations. Use `Span<T>` and `ReadOnlySpan<char>` for text and CSS parsing. Use static readonly dictionaries for lookup tables.

### Source Control Strategy
- **Branching:** We use standard GitFlow. `main` contains production-ready code. Development happens on `feature/*` branches.
- **Pull Requests:** PRs must include an updated CHANGELOG, pass all GitHub Actions CI tests, and require at least one approving review from a maintainer.
- **Versioning:** We follow strict [Semantic Versioning (SemVer)](https://semver.org/).

### Security
- HTML input is treated as untrusted. External resources (images referenced via `src`) are only resolved if they fall under the configured `BasePath` or are valid `data:` URIs, preventing SSRF attacks.

---

## Advanced Examples

### Generating PDF as a Byte Array (Web API Scenario)
If you are returning a PDF directly from an ASP.NET Core controller, you don't need to write it to disk.

```csharp
[ApiController]
[Route("api/[controller]")]
public class ReportController : ControllerBase
{
    private readonly IPdfGenerator _pdfGenerator;

    public ReportController(IPdfGenerator pdfGenerator)
    {
        _pdfGenerator = pdfGenerator;
    }

    [HttpPost("generate")]
    public IActionResult GenerateReport([FromBody] ReportRequest request)
    {
        string html = $"<h1>Report for {request.UserName}</h1><p>Data...</p>";
        
        using MemoryStream memoryStream = new();
        _pdfGenerator.Convert(html, memoryStream);
        
        return File(memoryStream.ToArray(), "application/pdf", "report.pdf");
    }
}
```

### Loading Images from a Base Path
If your HTML contains relative image paths (e.g., `<img src="images/logo.png" />`), you must configure the `BasePath`.

```csharp
ConversionOptions options = new 
{
    BasePath = "/var/www/html/assets/", // Linux path example
    Page = PageLayout.A4
};

PdfGenerator generator = new(options);
string html = @"<img src='logo.png' width='200' />"; // Resolves to /var/www/html/assets/logo.png
generator.WritePdfFile(html, "report.pdf");
```

---

## Troubleshooting

### Common Installation Problems
- **Error: `NU1202: Package HtmlToPdfDotNet.Library is not compatible with net8.0`**
  - **Fix:** This library strictly targets `net10.0`. You must upgrade your project to .NET 10.0 or higher.

### Runtime Issues
- **Images are not rendering (Blank spaces in PDF)**
  - **Fix:** Ensure the `src` attribute is either an absolute path, a valid Base64 string (`data:image/png;base64,...`), or that you have configured `ConversionOptions.BasePath` for relative paths.
- **Text overlapping or incorrect font sizes**
  - **Fix:** By default, HTML assumes 96 DPI, while PDF uses 72 DPI. The library automatically scales `px` to `pt` (`1px = 0.75pt`). Ensure your CSS explicitly uses `pt` or `px` consistently.

### Debugging Tips
- If the layout looks wrong, try disabling `CompressStreams = false` in `ConversionOptions`. This will output a raw, human-readable PDF file. You can then open the `.pdf` file in a text editor to inspect the raw PDF commands (e.g., `BT`, `Tf`, `cm`) being emitted.

---

## FAQ

**Q: Does it support JavaScript execution?**
A: No. HtmlToPdfDotNet is a static HTML/CSS layout engine. It does not execute JavaScript. If you need JS rendering (like React/Angular SPAs), you must pre-render the HTML before passing it to the library.

**Q: Can I use TailwindCSS or Bootstrap?**
A: Yes, but keep in mind that the library supports a subset of CSS. Complex flexbox or grid layouts might degrade gracefully into block layouts. It is recommended to use standard block, inline-block, and table layouts for maximum compatibility.

**Q: Why is the generated PDF larger than expected?**
A: Make sure `CompressStreams = true` is enabled in your options. If you are embedding custom TTF fonts, ensure the subsetter is actively trimming unused glyphs.

---

## License

This project is licensed under the **MIT + Commercial License** model.

- **MIT License:** You are free to use, modify, and include this software in commercial or non-commercial products.
- **Commercial Clause:** You may **NOT** sell this library as a standalone SaaS product (e.g., a "PDF Generation API as a Service"). 

Please refer to the `LICENSE` file in the repository root for the full legal text.
