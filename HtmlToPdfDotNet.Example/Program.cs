using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHtmlToPdfDotNet();

var generator = new PdfGenerator();
var result = generator.GeneratePdf("<em>Hello, .NET 10!</em>");

Console.WriteLine(result);
