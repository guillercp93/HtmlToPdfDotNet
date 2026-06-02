using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Fonts;
using HtmlToPdfDotNet.Library.Models.Imaging;

namespace HtmlToPdfDotNet.Tests.Validation;

public class PublicApiValidationTests : IDisposable
{
    private readonly string _sandbox;

    public PublicApiValidationTests()
    {
        _sandbox = Path.Combine(Path.GetTempPath(), $"api_validation_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        try { Directory.Delete(_sandbox, recursive: true); }
        catch { }
    }

    [Fact]
    public void Convert_NullHtml_ThrowsArgumentNullException()
    {
        PdfGenerator generator = new();

        Assert.Throws<ArgumentNullException>(() => generator.Convert(null!));
    }

    [Fact]
    public void Convert_NullOutputStream_ThrowsArgumentNullException()
    {
        PdfGenerator generator = new();

        Assert.Throws<ArgumentNullException>(() => generator.Convert("<p>Hello</p>", null!));
    }

    [Fact]
    public void Convert_NonWritableOutputStream_ThrowsArgumentException()
    {
        PdfGenerator generator = new();
        using MemoryStream output = new(new byte[1024], writable: false);

        Assert.Throws<ArgumentException>(() => generator.Convert("<p>Hello</p>", output));
    }

    [Fact]
    public void WritePdfFile_EmptyPath_ThrowsArgumentException()
    {
        PdfGenerator generator = new();

        Assert.Throws<ArgumentException>(() => generator.WritePdfFile("<p>Hello</p>", " "));
    }

    [Fact]
    public void RunLayout_NullHtml_ThrowsArgumentNullException()
    {
        PdfGenerator generator = new();

        Assert.Throws<ArgumentNullException>(() => generator.RunLayout(null!));
    }

    [Fact]
    public void Constructor_MissingBasePath_ThrowsDirectoryNotFoundException()
    {
        string missingBasePath = Path.Combine(_sandbox, "missing");

        Assert.Throws<DirectoryNotFoundException>(
            () => new PdfGenerator(new ConversionOptions { BasePath = missingBasePath }));
    }

    [Fact]
    public void Convert_MissingStylesheetPath_ThrowsFileNotFoundException()
    {
        string missingStylesheet = Path.Combine(_sandbox, "missing.css");
        PdfGenerator generator = new(new ConversionOptions
        {
            StyleSheets = [missingStylesheet]
        });

        Assert.Throws<FileNotFoundException>(() => generator.Convert("<p>Hello</p>"));
    }

    [Fact]
    public void Convert_ExistingStylesheetPath_LoadsCss()
    {
        string stylesheet = Path.Combine(_sandbox, "style.css");
        File.WriteAllText(stylesheet, "p { color: #ff0000; }");

        PdfGenerator generator = new(new ConversionOptions
        {
            CompressStreams = false,
            StyleSheets = [stylesheet]
        });

        byte[] pdf = generator.Convert("<p>Hello</p>");

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void FontRegistry_RegisterMissingFont_ThrowsFileNotFoundException()
    {
        FontRegistry registry = new();
        string missingFont = Path.Combine(_sandbox, "missing.ttf");

        Assert.Throws<FileNotFoundException>(() => registry.RegisterFont(missingFont, "Missing"));
    }

    [Fact]
    public void FontRegistry_RegisterMissingDirectory_ThrowsDirectoryNotFoundException()
    {
        FontRegistry registry = new();
        string missingDirectory = Path.Combine(_sandbox, "fonts");

        Assert.Throws<DirectoryNotFoundException>(() => registry.RegisterDirectory(missingDirectory));
    }

    [Fact]
    public void ImageLoader_LoadFromBytes_UnsupportedFormat_ThrowsNotSupportedException()
    {
        byte[] bytes = [0x00, 0x01, 0x02, 0x03];

        Assert.Throws<NotSupportedException>(() => ImageLoader.LoadFromBytes(bytes));
    }

    [Fact]
    public void ImageLoader_LoadFromBytes_EmptyBytes_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ImageLoader.LoadFromBytes([]));
    }

    [Fact]
    public void ImageLoader_Load_InvalidDataUriBase64_ThrowsArgumentException()
    {
        string dataUri = "data:image/png;base64,not-valid-base64";

        Assert.Throws<ArgumentException>(() => ImageLoader.Load(dataUri));
    }
}
