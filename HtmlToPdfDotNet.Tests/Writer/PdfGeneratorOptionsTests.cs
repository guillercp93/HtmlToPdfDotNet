using System.Text;
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;

namespace HtmlToPdfDotNet.Tests.Writer;

file static class SnapshotFonts
{
    public const string Regular = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";

    public static bool Available => File.Exists(Regular);
}

public class PdfGeneratorOptionsTests
{
    [Fact]
    public void Constructor_SnapshotsCompressionOption()
    {
        ConversionOptions options = new() { CompressStreams = false };
        PdfGenerator generator = new(options);

        options.CompressStreams = true;

        byte[] pdf = generator.Convert("<p>Hello</p>");
        string text = Encoding.Latin1.GetString(pdf);

        Assert.DoesNotContain("/Filter /FlateDecode", text);
    }

    [Fact]
    public void Constructor_SnapshotsFontRegistry()
    {
        if (!SnapshotFonts.Available) return;

        ConversionOptions options = new() { CompressStreams = false };
        PdfGenerator generator = new(options);

        options.Fonts.RegisterFont(SnapshotFonts.Regular, "DejaVu Sans");

        byte[] pdf = generator.Convert("<p style='font-family:DejaVu Sans'>Hello</p>");
        string text = Encoding.Latin1.GetString(pdf);

        Assert.DoesNotContain("/Subtype /Type0", text);
    }
}
