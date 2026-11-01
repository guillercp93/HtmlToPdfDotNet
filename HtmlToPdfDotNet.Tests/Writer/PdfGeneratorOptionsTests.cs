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

/// <summary>
/// ConversionOptions.MaxHtmlLength and MaxNestingDepth: documents above a limit are
/// rejected before layout instead of exhausting memory or overflowing the stack.
/// </summary>
public class DocumentLimitsTests
{
    private static string Nested(int depth)
        => string.Concat(Enumerable.Repeat("<div>", depth)) + "x" + string.Concat(Enumerable.Repeat("</div>", depth));

    [Fact]
    public void Defaults_DepthIs256_LengthIsUnlimited()
    {
        ConversionOptions options = new();

        Assert.Equal(256, options.MaxNestingDepth);
        Assert.Null(options.MaxHtmlLength);
    }

    [Fact]
    public void NestingAtDefaultLimit_Converts()
    {
        byte[] pdf = new PdfGenerator().Convert(Nested(256));

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void NestingAboveDefaultLimit_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new PdfGenerator().Convert(Nested(257)));

        Assert.Contains("MaxNestingDepth", ex.Message);
    }

    [Fact]
    public void ExtremeNesting_IsRejectedWithoutCrashingTheProcess()
    {
        // 20,000 levels overflows the layout engine's stack on every platform (1 MB stacks crash
        // near 1,000); the check itself must not recurse. Kept moderate because HtmlAgilityPack's
        // parse time grows quadratically with nesting depth.
        Assert.Throws<ArgumentException>(() => new PdfGenerator().Convert(Nested(20_000)));
    }

    [Fact]
    public void NullMaxNestingDepth_DisablesTheDepthCheck()
    {
        byte[] pdf = new PdfGenerator(new ConversionOptions { MaxNestingDepth = null }).Convert(Nested(300));

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void HeaderTemplateAboveDepthLimit_ThrowsArgumentException()
    {
        PdfGenerator generator = new(new ConversionOptions
        {
            MaxNestingDepth = 4,
            HeaderFooter = new HeaderFooterConfig { HeaderHtml = Nested(5) },
        });

        Assert.Throws<ArgumentException>(() => generator.Convert("<p>Hi</p>"));
    }

    [Fact]
    public void HtmlAtMaxLength_Converts_AndOneCharMore_Throws()
    {
        string html = "<p>" + new string('a', 93) + "</p>"; // 100 characters
        PdfGenerator generator = new(new ConversionOptions { MaxHtmlLength = 100 });

        Assert.NotEmpty(generator.Convert(html));
        ArgumentException ex = Assert.Throws<ArgumentException>(() => generator.Convert(html + " "));
        Assert.Contains("MaxHtmlLength", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveLimits_AreRejectedBySetters(int value)
    {
        ConversionOptions options = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxHtmlLength = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxNestingDepth = value);
    }
}
