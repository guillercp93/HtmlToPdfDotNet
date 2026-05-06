using System.Text;
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Writer;
using HtmlToPdfDotNet.Library.Models.Fonts;

namespace HtmlToPdfDotNet.Tests.Writer;

// ─────────────────────────────────────────────────────────────────────────────
//  Helper: paths to system DejaVu fonts (skip tests gracefully when absent)
// ─────────────────────────────────────────────────────────────────────────────
file static class Fonts
{
    public const string Regular = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
    public const string Bold = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";

    public static bool Available => File.Exists(Regular);
}

// ─────────────────────────────────────────────────────────────────────────────
//  TrueTypeFontParser
// ─────────────────────────────────────────────────────────────────────────────
public class TrueTypeFontParserTests
{
    [Fact]
    public void Parse_DejaVuSans_ReturnsNonZeroUnitsPerEm()
    {
        if (!Fonts.Available) return;
        EmbeddedFontInfo info = TrueTypeFontParser.Parse(Fonts.Regular, "DejaVu Sans", false, false);
        Assert.True(info.UnitsPerEm > 0, $"unitsPerEm={info.UnitsPerEm}");
    }

    [Fact]
    public void Parse_DejaVuSans_CmapContainsBasicAscii()
    {
        if (!Fonts.Available) return;
        EmbeddedFontInfo info = TrueTypeFontParser.Parse(Fonts.Regular, "DejaVu Sans", false, false);

        // Basic ASCII must be present
        foreach (char ch in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
            Assert.True(info.CmapUnicodeToGid.ContainsKey(ch),
                $"cmap missing '{ch}' (U+{(int)ch:X4})");
    }

    [Fact]
    public void Parse_DejaVuSans_AdvanceWidthsNonEmpty()
    {
        if (!Fonts.Available) return;
        EmbeddedFontInfo info = TrueTypeFontParser.Parse(Fonts.Regular, "DejaVu Sans", false, false);
        Assert.NotEmpty(info.AdvanceWidths);
    }

    [Fact]
    public void Parse_DejaVuSans_AscenderAndDescenderReasonable()
    {
        if (!Fonts.Available) return;
        EmbeddedFontInfo info = TrueTypeFontParser.Parse(Fonts.Regular, "DejaVu Sans", false, false);

        // Ascender must be positive, descender negative (in design units)
        Assert.True(info.Ascender > 0, $"Ascender={info.Ascender}");
        Assert.True(info.Descender < 0, $"Descender={info.Descender}");
    }

    [Fact]
    public void Parse_DejaVuSans_IsTrueType_NotCff()
    {
        if (!Fonts.Available) return;
        EmbeddedFontInfo info = TrueTypeFontParser.Parse(Fonts.Regular, "DejaVu Sans", false, false);
        Assert.False(info.IsCff); // DejaVu is a TrueType font
    }

    [Fact]
    public void Parse_GetGlyphId_SpaceReturnsMeaningfulGid()
    {
        if (!Fonts.Available) return;
        EmbeddedFontInfo info = TrueTypeFontParser.Parse(Fonts.Regular, "DejaVu Sans", false, false);
        int gid = info.GetGlyphId(' ');
        Assert.True(gid > 0, $"Space GID should be > 0, got {gid}");
    }

    [Fact]
    public void Parse_MeasureWidth_PositiveForNonEmptyString()
    {
        if (!Fonts.Available) return;
        EmbeddedFontInfo info = TrueTypeFontParser.Parse(Fonts.Regular, "DejaVu Sans", false, false);
        float w = info.MeasureWidth("Hello", 12f);
        Assert.True(w > 0f, $"Width={w}");
    }

    [Fact]
    public void Parse_MeasureWidth_LongerStringIsWider()
    {
        if (!Fonts.Available) return;
        EmbeddedFontInfo info = TrueTypeFontParser.Parse(Fonts.Regular, "DejaVu Sans", false, false);
        float short_ = info.MeasureWidth("Hi", 12f);
        float long_ = info.MeasureWidth("Hello World", 12f);
        Assert.True(long_ > short_, $"short={short_}, long={long_}");
    }

    [Fact]
    public void Parse_GetPdfWidth_InRange()
    {
        if (!Fonts.Available) return;
        EmbeddedFontInfo info = TrueTypeFontParser.Parse(Fonts.Regular, "DejaVu Sans", false, false);
        int gid = info.GetGlyphId('A');
        int w = info.GetPdfWidth(gid);
        // Reasonable range: 100..1200 PDF units
        Assert.InRange(w, 100, 1200);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  EmbeddedFontInfo helpers
// ─────────────────────────────────────────────────────────────────────────────
public class EmbeddedFontInfoTests
{
    private static EmbeddedFontInfo MakeFont()
        => new()
        {
            FamilyName = "Test",
            UnitsPerEm = 1000,
            Ascender = 800,
            Descender = -200,
            CapHeight = 700,
            FontBBox = [0, -200, 1000, 800],
            CmapUnicodeToGid = new Dictionary<int, int>
            {
                ['A'] = 36,
                ['B'] = 37,
                [' '] = 3,
            },
            AdvanceWidths = [0, 500, 500, 278, 0, 0, 0, 0, 0, 0,
                             0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                             0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                             0, 0, 0, 0, 0, 0, 722, 667],
            LastAdvanceWidth = 500,
            FontBytes = [],
        };

    [Fact]
    public void GetGlyphId_KnownChar_ReturnsCorrectGid()
    {
        EmbeddedFontInfo f = MakeFont();
        Assert.Equal(36, f.GetGlyphId('A'));
    }

    [Fact]
    public void GetGlyphId_UnknownChar_ReturnsZero()
    {
        EmbeddedFontInfo f = MakeFont();
        Assert.Equal(0, f.GetGlyphId('Z')); // not in map
    }

    [Fact]
    public void GetPdfWidth_ScalesToThousandUnits()
    {
        EmbeddedFontInfo f = MakeFont(); // UnitsPerEm=1000, GID 36 has aw=722
        Assert.Equal(722, f.GetPdfWidth(36));
    }

    [Fact]
    public void MeasureWidth_EmptyString_ReturnsZero()
    {
        Assert.Equal(0f, MakeFont().MeasureWidth("", 12f));
    }

    [Fact]
    public void MeasureWidth_AB_UsesSumOfWidths()
    {
        EmbeddedFontInfo f = MakeFont();
        // GID 36 (A) = 722, GID 37 (B) = 667  → total=1389 units → 1389/1000*12 = 16.668
        float w = f.MeasureWidth("AB", 12f);
        Assert.InRange(w, 16f, 17f);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  ToUnicodeCMapBuilder
// ─────────────────────────────────────────────────────────────────────────────
public class ToUnicodeCMapBuilderTests
{
    [Fact]
    public void Build_ContainsCmapHeader()
    {
        Dictionary<int, int> cmap = new() { [36] = 'A', [37] = 'B' };
        byte[] bytes = ToUnicodeCMapBuilder.Build(cmap, "TestFont");
        string text = Encoding.ASCII.GetString(bytes);

        Assert.Contains("begincmap", text);
        Assert.Contains("endcmap", text);
        Assert.Contains("beginbfchar", text);
    }

    [Fact]
    public void Build_ContainsMappingEntries()
    {
        Dictionary<int, int> cmap = new() { [36] = 0x0041 }; // GID 36 → 'A' (U+0041)
        string text = Encoding.ASCII.GetString(ToUnicodeCMapBuilder.Build(cmap, "TestFont"));

        Assert.Contains("<0024>", text); // GID 36 = 0x0024
        Assert.Contains("<0041>", text); // Unicode 'A'
    }

    [Fact]
    public void InvertCmap_ProducesCorrectMapping()
    {
        Dictionary<int, int> unicodeToGid = new() { [0x0041] = 36, [0x0042] = 37 };
        Dictionary<int, int> inv = ToUnicodeCMapBuilder.InvertCmap(unicodeToGid);

        Assert.Equal(0x0041, inv[36]);
        Assert.Equal(0x0042, inv[37]);
    }

    [Fact]
    public void Build_EmptyMap_StillValidCmap()
    {
        string text = Encoding.ASCII.GetString(ToUnicodeCMapBuilder.Build(new Dictionary<int, int>(), "TestFont"));
        Assert.Contains("begincmap", text);
        Assert.Contains("endcmap", text);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  FontRegistry
// ─────────────────────────────────────────────────────────────────────────────
public class FontRegistryTests
{
    [Fact]
    public void RegisterFont_ThenResolve_ReturnsFontInfo()
    {
        if (!Fonts.Available) return;
        FontRegistry reg = new();
        reg.RegisterFont(Fonts.Regular, "DejaVu Sans", bold: false, italic: false);

        Assert.True(reg.TryResolve("DejaVu Sans", false, false, out EmbeddedFontInfo? info));
        Assert.NotNull(info);
        Assert.Equal("dejavu sans", info!.FamilyName);
    }

    [Fact]
    public void Resolve_UnknownFamily_ReturnsFalse()
    {
        FontRegistry reg = new();
        Assert.False(reg.TryResolve("NonExistentFamily", false, false, out _));
    }

    [Fact]
    public void Resolve_BoldFallsBackToRegular()
    {
        if (!Fonts.Available) return;
        FontRegistry reg = new();
        reg.RegisterFont(Fonts.Regular, "DejaVu Sans", bold: false, italic: false);

        // Bold not registered → should fall back to regular
        Assert.True(reg.TryResolve("DejaVu Sans", bold: true, italic: false, out EmbeddedFontInfo? info));
        Assert.NotNull(info);
        Assert.False(info!.IsBold); // got the regular face
    }

    [Fact]
    public void RegisterFont_SameKeyTwice_DoesNotThrow()
    {
        if (!Fonts.Available) return;
        FontRegistry reg = new();
        reg.RegisterFont(Fonts.Regular, "DejaVu Sans");
        Exception ex = Record.Exception(() => reg.RegisterFont(Fonts.Regular, "DejaVu Sans"));
        Assert.Null(ex);
    }

    [Fact]
    public void HasFamily_ReturnsTrueWhenRegistered()
    {
        if (!Fonts.Available) return;
        FontRegistry reg = new();
        reg.RegisterFont(Fonts.Regular, "DejaVu Sans");
        Assert.True(reg.HasFamily("DejaVu Sans"));
        Assert.False(reg.HasFamily("NotRegistered"));
    }

    [Fact]
    public void RegisterDirectory_LoadsDejaVuFonts()
    {
        const string dir = "/usr/share/fonts/truetype/dejavu";
        if (!Directory.Exists(dir)) return;

        FontRegistry reg = new();
        reg.RegisterDirectory(dir);

        // At least one font should be loadable
        Assert.NotEmpty(reg.AllFonts);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  ContentStreamBuilder – embedded font path
// ─────────────────────────────────────────────────────────────────────────────
public class ContentStreamBuilderEmbeddedFontTests
{
    private static EmbeddedFontInfo MakeMinimalFont()
    {
        // Minimal synthetic font info for unit testing (no real TTF bytes needed)
        return new EmbeddedFontInfo
        {
            FamilyName = "synthetic",
            UnitsPerEm = 1000,
            Ascender = 800,
            Descender = -200,
            CapHeight = 700,
            FontBBox = [0, -200, 1000, 800],
            CmapUnicodeToGid = new Dictionary<int, int>
            {
                ['H'] = 43,
                ['i'] = 78,
                [' '] = 3,
            },
            AdvanceWidths = Enumerable.Repeat(500, 200).ToArray(),
            LastAdvanceWidth = 500,
            FontBytes = [],
        };
    }

    [Fact]
    public void DrawText_EmbeddedFont_EmitsGidHexString()
    {
        EmbeddedFontInfo font = MakeMinimalFont();
        Dictionary<EmbeddedFontInfo, string> aliases = new() { [font] = "FE0" };

        ContentStreamBuilder b = new(841.89f, aliases);
        b.DrawText(new TextPrimitive
        {
            X = 10f,
            Y = 100f,
            Text = "Hi",
            FontName = "synthetic",
            FontSize = 12f,
            Color = new CssColor(0, 0, 0),
            EmbeddedFont = font,
        });

        string raw = b.RawContent;

        // Should contain a hex string (angle brackets), NOT a literal parenthesis string
        Assert.Contains("<", raw);
        Assert.Contains(">", raw);
        // GID of 'H' = 43 = 0x002B, GID of 'i' = 78 = 0x004E → hex "002B004E"
        Assert.Contains("002B004E", raw);
    }

    [Fact]
    public void DrawText_EmbeddedFont_UsesEmbeddedAlias()
    {
        EmbeddedFontInfo font = MakeMinimalFont();
        Dictionary<EmbeddedFontInfo, string> aliases = new() { [font] = "FE0" };

        ContentStreamBuilder b = new(841.89f, aliases);
        b.DrawText(new TextPrimitive
        {
            X = 0f,
            Y = 0f,
            Text = "H",
            FontName = "synthetic",
            FontSize = 10f,
            Color = new CssColor(0, 0, 0),
            EmbeddedFont = font,
        });

        Assert.Contains("/FE0", b.RawContent);
    }

    [Fact]
    public void DrawText_NullEmbeddedFont_FallsBackToStandardEncoding()
    {
        ContentStreamBuilder b = new(841.89f, embeddedAliases: null);
        b.DrawText(new TextPrimitive
        {
            X = 0f,
            Y = 0f,
            Text = "Hello",
            FontName = "Helvetica",
            FontSize = 12f,
            Color = new CssColor(0, 0, 0),
            EmbeddedFont = null,
        });

        string raw = b.RawContent;
        Assert.Contains("(Hello)", raw);       // Latin-1 string literal
        Assert.Contains("/F1", raw);            // Standard alias
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  FontSubsetBuilder
// ─────────────────────────────────────────────────────────────────────────────
public class FontSubsetBuilderTests
{
    [Fact]
    public void Build_TrueTypeFont_ReturnsValidSfnt()
    {
        if (!Fonts.Available) return;
        EmbeddedFontInfo info = TrueTypeFontParser.Parse(Fonts.Regular, "DejaVu Sans", false, false);
        int[] gids = [0, info.GetGlyphId('A'), info.GetGlyphId('B')];

        byte[] subset = FontSubsetBuilder.Build(info, gids);

        Assert.NotEmpty(subset);
        // sfnt magic: 0x00010000 (TrueType)
        uint magic = (uint)(subset[0] << 24 | subset[1] << 16 | subset[2] << 8 | subset[3]);
        Assert.Equal(0x00010000u, magic);
    }

    [Fact]
    public void Build_TrueTypeFont_SubsetSmallerThanOriginal()
    {
        if (!Fonts.Available) return;
        EmbeddedFontInfo info = TrueTypeFontParser.Parse(Fonts.Regular, "DejaVu Sans", false, false);
        int[] gids = [0, info.GetGlyphId('A')];
        byte[] subset = FontSubsetBuilder.Build(info, gids);

        Assert.True(subset.Length < info.FontBytes.Length,
            $"subset={subset.Length} should be < original={info.FontBytes.Length}");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  End-to-end integration: PDF with embedded font
// ─────────────────────────────────────────────────────────────────────────────
public class PdfEmbeddedFontIntegrationTests
{
    private static (byte[] Pdf, string Text) GenerateWithFont(
        string html, bool compress = false)
    {
        ConversionOptions opts = new() { CompressStreams = compress };
        opts.Fonts.RegisterFont(Fonts.Regular, "DejaVu Sans");
        opts.Fonts.RegisterFont(Fonts.Bold, "DejaVu Sans", bold: true);

        byte[] pdf = new PdfGenerator(opts).Convert(html);
        string text = Encoding.Latin1.GetString(pdf);
        return (pdf, text);
    }

    [Fact]
    public void EmbeddedFont_PdfContainsType0Font()
    {
        if (!Fonts.Available) return;
        (byte[] _, string text) = GenerateWithFont("<p style='font-family:DejaVu Sans'>Hello</p>");

        Assert.Contains("/Subtype /Type0", text);
    }

    [Fact]
    public void EmbeddedFont_PdfContainsCIDFontType2()
    {
        if (!Fonts.Available) return;
        (byte[] _, string text) = GenerateWithFont("<p style='font-family:DejaVu Sans'>Hello</p>");

        Assert.Contains("/Subtype /CIDFontType2", text);
    }

    [Fact]
    public void EmbeddedFont_PdfContainsFontDescriptor()
    {
        if (!Fonts.Available) return;
        (byte[] _, string text) = GenerateWithFont("<p style='font-family:DejaVu Sans'>Hello</p>");

        Assert.Contains("/Type /FontDescriptor", text);
    }

    [Fact]
    public void EmbeddedFont_PdfContainsFontFile2()
    {
        if (!Fonts.Available) return;
        (byte[] _, string text) = GenerateWithFont("<p style='font-family:DejaVu Sans'>Hello</p>");

        Assert.Contains("/FontFile2", text);
    }

    [Fact]
    public void EmbeddedFont_PdfContainsToUnicodeCMap()
    {
        if (!Fonts.Available) return;
        (byte[] _, string text) = GenerateWithFont("<p style='font-family:DejaVu Sans'>Hello</p>");

        Assert.Contains("/ToUnicode", text);
        Assert.Contains("begincmap", text);
    }

    [Fact]
    public void EmbeddedFont_PdfContainsIdentityHEncoding()
    {
        if (!Fonts.Available) return;
        (byte[] _, string text) = GenerateWithFont("<p style='font-family:DejaVu Sans'>Hello</p>");

        Assert.Contains("/Identity-H", text);
    }

    [Fact]
    public void EmbeddedFont_PdfContainsWidthArray()
    {
        if (!Fonts.Available) return;
        (byte[] _, string text) = GenerateWithFont("<p style='font-family:DejaVu Sans'>Hello</p>");

        // CIDFont /W entry
        Assert.Contains("/W [", text);
    }

    [Fact]
    public void EmbeddedFont_PdfContainsCIDSystemInfo()
    {
        if (!Fonts.Available) return;
        (byte[] _, string text) = GenerateWithFont("<p style='font-family:DejaVu Sans'>Hello</p>");

        Assert.Contains("/CIDSystemInfo", text);
        Assert.Contains("/Ordering (Identity)", text);
    }

    [Fact]
    public void EmbeddedFont_PdfIsValidBinaryStart()
    {
        if (!Fonts.Available) return;
        (byte[] pdf, string _) = GenerateWithFont("<p style='font-family:DejaVu Sans'>Hello World</p>");

        string header = Encoding.ASCII.GetString(pdf, 0, 8);
        Assert.StartsWith("%PDF-1.7", header);
    }

    [Fact]
    public void EmbeddedFont_PdfEndsWithEof()
    {
        if (!Fonts.Available) return;
        (byte[] pdf, string _) = GenerateWithFont("<p style='font-family:DejaVu Sans'>Test</p>");

        string tail = Encoding.Latin1.GetString(pdf, pdf.Length - 5, 5);
        Assert.Equal("%%EOF", tail);
    }

    [Fact]
    public void EmbeddedFont_SizeIsLargerThanStandardFontPdf()
    {
        if (!Fonts.Available) return;

        // Standard fonts only
        byte[] stdPdf = new PdfGenerator(new ConversionOptions { CompressStreams = false })
            .Convert("<p>Hello World</p>");

        // Embedded font (larger due to FontFile2 stream)
        (byte[] embPdf, string _) = GenerateWithFont("<p style='font-family:DejaVu Sans'>Hello World</p>");

        Assert.True(embPdf.Length > stdPdf.Length,
            $"Embedded PDF ({embPdf.Length} B) should be larger than standard ({stdPdf.Length} B)");
    }

    [Fact]
    public void EmbeddedFont_FallsBackToStandardFontForUnregisteredFamily()
    {
        if (!Fonts.Available) return;

        // Use Arial which is NOT registered – should fall back to Helvetica (Type1)
        ConversionOptions opts = new() { CompressStreams = false };
        opts.Fonts.RegisterFont(Fonts.Regular, "DejaVu Sans");

        byte[] pdf = new PdfGenerator(opts).Convert("<p style='font-family:Arial'>Test</p>");
        string text = Encoding.Latin1.GetString(pdf);

        // Standard Type1 must still be present
        Assert.Contains("/Subtype /Type1", text);
    }

    [Fact]
    public void NoEmbeddedFonts_PdfContainsOnlyType1()
    {
        // Verify baseline: without font registry nothing changed
        byte[] pdf = new PdfGenerator(new ConversionOptions { CompressStreams = false })
                      .Convert("<p>Hello</p>");
        string text = Encoding.Latin1.GetString(pdf);

        Assert.Contains("/Subtype /Type1", text);
        Assert.DoesNotContain("/Subtype /Type0", text);
    }
}
