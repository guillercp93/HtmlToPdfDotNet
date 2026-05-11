using System.Text;
using HtmlAgilityPack;
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Tests.Css;

// ─────────────────────────────────────────────────────────────────────────────
// CssStyleSheetParser
// ─────────────────────────────────────────────────────────────────────────────
public class CssStyleSheetParserTests
{
    [Fact]
    public void Parse_SimpleRule_ExtractsDeclarations()
    {
        List<CssRule> rules = CssStyleSheetParser.Parse("p { color: red; font-size: 14pt; }").ToList();

        Assert.Single(rules);
        Assert.True(rules[0].Declarations.ContainsKey("color"));
        Assert.Equal("red", rules[0].Declarations["color"]);
    }

    [Fact]
    public void Parse_MultipleSelectors_SplitsByComma()
    {
        List<CssRule> rules = CssStyleSheetParser.Parse("h1, h2 { font-weight: bold; }").ToList();

        Assert.Single(rules);
        Assert.Equal(2, rules[0].Selectors.Count);
        Assert.Equal("h1", rules[0].Selectors[0].Raw);
        Assert.Equal("h2", rules[0].Selectors[1].Raw);
    }

    [Fact]
    public void Parse_ClassSelector_ParsesCorrectly()
    {
        List<CssRule> rules = CssStyleSheetParser.Parse(".intro { color: blue; }").ToList();

        CssSelector selector = rules[0].Selectors[0];
        SelectorPart part = selector.Parts[0];
        Assert.Null(part.Tag);
        Assert.Contains("intro", part.Classes);
    }

    [Fact]
    public void Parse_IdSelector_ParsesCorrectly()
    {
        List<CssRule> rules = CssStyleSheetParser.Parse("#main { margin: 10pt; }").ToList();

        SelectorPart part = rules[0].Selectors[0].Parts[0];
        Assert.Equal("main", part.Id);
    }

    [Fact]
    public void Parse_CompoundSelector_TagAndClass()
    {
        List<CssRule> rules = CssStyleSheetParser.Parse("p.intro { color: green; }").ToList();

        SelectorPart part = rules[0].Selectors[0].Parts[0];
        Assert.Equal("p", part.Tag);
        Assert.Contains("intro", part.Classes);
    }

    [Fact]
    public void Parse_DescendantSelector_TwoParts()
    {
        List<CssRule> rules = CssStyleSheetParser.Parse("div p { color: navy; }").ToList();

        CssSelector selector = rules[0].Selectors[0];
        Assert.Equal(2, selector.Parts.Count);
        Assert.Equal("div", selector.Parts[0].Tag);
        Assert.Equal("p", selector.Parts[1].Tag);
    }

    [Fact]
    public void Parse_CommentsAreRemoved()
    {
        List<CssRule> rules = CssStyleSheetParser.Parse("/* comment */ p { color: red; }").ToList();
        Assert.Single(rules);
    }

    [Fact]
    public void Parse_MediaQueryIsIgnored()
    {
        string css = "@media print { p { color: black; } } h1 { font-size: 20pt; }";
        List<CssRule> rules = CssStyleSheetParser.Parse(css).ToList();

        // Solo la regla h1 debe quedar
        Assert.Single(rules);
        Assert.Equal("h1", rules[0].Selectors[0].Raw);
    }

    [Fact]
    public void Parse_ImportantIsStripped()
    {
        List<CssRule> rules = CssStyleSheetParser.Parse("p { color: red !important; }").ToList();
        Assert.Equal("red", rules[0].Declarations["color"]);
    }

    [Fact]
    public void ParseFromHtml_ExtractsStyleBlock()
    {
        string html = "<html><head><style>h1 { font-size: 24pt; }</style></head><body></body></html>";
        List<CssRule> rules = CssStyleSheetParser.ParseFromHtml(html).ToList();

        Assert.Single(rules);
        Assert.Equal("24pt", rules[0].Declarations["font-size"]);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CssSelectorMatcher
// ─────────────────────────────────────────────────────────────────────────────
public class CssSelectorMatcherTests
{
    private static HtmlNode Node(string html)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        return doc.DocumentNode.SelectSingleNode("//*[1]");
    }

    private static CssSelector Selector(string raw)
        => CssStyleSheetParser.Parse($"{raw} {{ color: red }}").ToList()[0].Selectors[0];

    [Fact]
    public void TagSelector_MatchesCorrectTag()
    {
        HtmlNode node = Node("<p>text</p>");
        Assert.True(CssSelectorMatcher.Matches(node, Selector("p")));
        Assert.False(CssSelectorMatcher.Matches(node, Selector("div")));
    }

    [Fact]
    public void ClassSelector_MatchesNodeWithClass()
    {
        HtmlNode node = Node("<p class=\"intro\">text</p>");
        Assert.True(CssSelectorMatcher.Matches(node, Selector(".intro")));
        Assert.False(CssSelectorMatcher.Matches(node, Selector(".other")));
    }

    [Fact]
    public void IdSelector_MatchesNodeWithId()
    {
        HtmlNode node = Node("<div id=\"main\">text</div>");
        Assert.True(CssSelectorMatcher.Matches(node, Selector("#main")));
        Assert.False(CssSelectorMatcher.Matches(node, Selector("#other")));
    }

    [Fact]
    public void CompoundSelector_TagAndClass()
    {
        HtmlNode node = Node("<p class=\"intro\">text</p>");
        Assert.True(CssSelectorMatcher.Matches(node, Selector("p.intro")));
        Assert.False(CssSelectorMatcher.Matches(node, Selector("div.intro")));
    }

    [Fact]
    public void Specificity_IdHigherThanClass()
    {
        CssSelector id = Selector("#main");
        CssSelector cls = Selector(".intro");
        Assert.True(id.SpecificityScore > cls.SpecificityScore);
    }

    [Fact]
    public void Specificity_ClassHigherThanTag()
    {
        CssSelector cls = Selector(".intro");
        CssSelector tag = Selector("p");
        Assert.True(cls.SpecificityScore > tag.SpecificityScore);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// StyleResolver con cascade completa
// ─────────────────────────────────────────────────────────────────────────────
public class StyleResolverCascadeTests
{
    private static Dictionary<HtmlNode, ComputedStyle> Resolve(string html, string css = "")
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        List<CssRule> rules = string.IsNullOrEmpty(css) ? [] : CssStyleSheetParser.Parse(css).ToList();
        StyleResolver resolver = new(rules);
        return resolver.Resolve(doc.DocumentNode);
    }

    [Fact]
    public void StylesheetRule_AppliedToMatchingNode()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve("<p>text</p>", "p { color: #ff0000; }");
        ComputedStyle p = styles.First(kv => kv.Key.Name == "p").Value;

        Assert.Equal(1f, p.Color.R, precision: 2);
        Assert.Equal(0f, p.Color.G, precision: 2);
    }

    [Fact]
    public void InlineStyle_OverridesStylesheet()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve(
            "<p style=\"color: #00ff00\">text</p>", "p { color: #ff0000; }");
        ComputedStyle p = styles.First(kv => kv.Key.Name == "p").Value;

        // Inline gana sobre stylesheet
        Assert.Equal(0f, p.Color.R, precision: 2);
        Assert.Equal(1f, p.Color.G, precision: 2);
    }

    [Fact]
    public void HigherSpecificity_Wins()
    {
        Dictionary<HtmlNode, ComputedStyle> styles = Resolve(
            "<p class=\"intro\">text</p>",
            "p { color: red; } .intro { color: blue; }");

        ComputedStyle p = styles.First(kv => kv.Key.Name == "p").Value;

        // .intro (0,1,0) > p (0,0,1) → color azul
        Assert.Equal(0f, p.Color.R, precision: 2);
        Assert.Equal(0f, p.Color.G, precision: 2);
        Assert.Equal(1f, p.Color.B, precision: 2);
    }

    [Fact]
    public void ExternalStylesheet_AppliedViaConversionOptions()
    {
        ConversionOptions options = new()
        {
            StyleSheets = ["h1 { color: #ff0000; }"],
            CompressStreams = false,
        };
        byte[] pdf = new PdfGenerator(options).Convert("<h1>Title</h1>");
        string text = Encoding.Latin1.GetString(pdf);

        // El PDF debe existir y ser válido
        Assert.StartsWith("%PDF", text);
        Assert.Contains("%%EOF", text);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Tests E2E — documentos HTML completos
// ─────────────────────────────────────────────────────────────────────────────
public class EndToEndTests
{
    private static byte[] Generate(string html, bool compress = false)
        => new PdfGenerator(new ConversionOptions { CompressStreams = compress })
            .Convert(html);

    private static string Text(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    [Fact]
    public void E2E_MinimalHtml_ProducesValidPdf()
    {
        byte[] pdf = Generate("<html><body><p>Hello World</p></body></html>");
        Assert.StartsWith("%PDF-1.7", Text(pdf));
        Assert.Contains("%%EOF", Text(pdf));
    }

    [Fact]
    public void E2E_CompleteDocument_AllElements()
    {
        const string html = """
            <html>
            <head>
              <style>
                body  { font-family: Helvetica; font-size: 11pt; }
                h1    { color: #333333; }
                .note { background-color: #ffffcc; padding: 8pt; }
              </style>
            </head>
            <body>
              <h1>Informe Mensual</h1>
              <p>Este es el <strong>resumen ejecutivo</strong> del período.</p>
              <p class="note">Nota: los datos son preliminares.</p>
              <h2>Detalle</h2>
              <ul>
                <li>Ítem uno con descripción larga para probar el word wrap.</li>
                <li>Ítem dos</li>
                <li>Ítem tres</li>
              </ul>
            </body>
            </html>
            """;

        byte[] pdf = Generate(html);
        string t = Text(pdf);

        Assert.StartsWith("%PDF-1.7", t);
        Assert.Contains("/Type /Catalog", t);
        Assert.Contains("/Type /Pages", t);
        Assert.Contains("%%EOF", t);
        Assert.True(pdf.Length > 1_000);
    }

    [Fact]
    public void E2E_MultiplePages_PageCountCorrect()
    {
        string paras = string.Concat(
            Enumerable.Repeat(
                "<p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore.</p>",
                60));
        string html = $"<html><body>{paras}</body></html>";
        ConversionOptions options = new ConversionOptions { CompressStreams = false };
        LayoutResult result = new PdfGenerator(options).RunLayout(html);

        Assert.True(result.PageCount > 1, $"Esperaba >1 páginas, obtuvo {result.PageCount}");
    }

    [Fact]
    public void E2E_ExplicitPageBreak_SplitsPages()
    {
        const string html = """
            <div>Contenido de la página 1.</div>
            <div style="page-break-before: always">Contenido de la página 2.</div>
            """;

        ConversionOptions options = new ConversionOptions { CompressStreams = false };
        LayoutResult result = new PdfGenerator(options).RunLayout(html);

        Assert.True(result.PageCount >= 2);
    }

    [Fact]
    public void E2E_StyleBlockInHead_AppliedToBody()
    {
        const string html = """
            <html>
            <head><style>p { font-size: 20pt; }</style></head>
            <body><p>Big text</p></body>
            </html>
            """;

        ConversionOptions options = new ConversionOptions { CompressStreams = false };
        LayoutResult result = new PdfGenerator(options).RunLayout(html);
        List<TextPrimitive> texts = result.Primitives.OfType<TextPrimitive>().ToList();

        Assert.True(texts.Any(t => t.FontSize >= 20f),
            "Se esperaba al menos un TextPrimitive con FontSize >= 20pt");
    }

    [Fact]
    public void E2E_UsLetter_CorrectMediaBox()
    {
        ConversionOptions options = new()
        {
            Page = PageLayout.Letter,
            CompressStreams = false,
        };
        byte[] pdf = new PdfGenerator(options).Convert("<p>Letter</p>");
        string text = Text(pdf);

        // Letter = 612 x 792 pt
        Assert.Contains("612", text);
        Assert.Contains("792", text);
    }

    [Fact]
    public void E2E_TableHtml_ProducesOutput()
    {
        const string html = """
            <table>
              <tr><th>Nombre</th><th>Valor</th></tr>
              <tr><td>Alpha</td><td>100</td></tr>
              <tr><td>Beta</td><td>200</td></tr>
            </table>
            """;

        byte[] pdf = Generate(html);
        Assert.True(pdf.Length > 500);
        Assert.StartsWith("%PDF-1.7", Text(pdf));
    }

    [Fact]
    public void E2E_CompressedVsUncompressed_SameStructure()
    {
        const string html = "<h1>Test</h1><p>Paragraph content here.</p>";

        byte[] compressed = Generate(html, compress: true);
        byte[] uncompressed = Generate(html, compress: false);

        // Ambos son PDFs válidos
        Assert.StartsWith("%PDF-1.7", Encoding.Latin1.GetString(compressed));
        Assert.StartsWith("%PDF-1.7", Encoding.Latin1.GetString(uncompressed));

        // El comprimido debe ser más pequeño
        Assert.True(compressed.Length < uncompressed.Length,
            $"Comprimido={compressed.Length} debe ser < sin comprimir={uncompressed.Length}");
    }
}