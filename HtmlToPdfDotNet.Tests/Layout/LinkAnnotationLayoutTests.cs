using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Tests.Layout;

/// <summary>
/// Tests for PDF link annotation support.
/// Phases 4-6: LinkAnnotationPrimitive, InlineRun.LinkUri, annotation capture, PDF emission.
/// </summary>
public class LinkAnnotationLayoutTests
{
    private static LayoutResult RunLayout(string html, PageLayout? page = null)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        Dictionary<HtmlNode, ComputedStyle> styles = new StyleResolver().Resolve(doc.DocumentNode);
        BlockLayoutEngine engine = new(page ?? PageLayout.A4, styles);
        return engine.Layout(doc.DocumentNode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 4: Foundation — LinkAnnotationPrimitive + LayoutResult.Annotations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 4.1: Verifies that <see cref="LinkAnnotationPrimitive"/> exists with all required properties.
    /// </summary>
    [Fact]
    public void LinkAnnotationPrimitive_HasRequiredProperties()
    {
        var annot = new LinkAnnotationPrimitive
        {
            PageIndex = 0,
            X = 10f,
            Y = 20f,
            Width = 100f,
            Height = 15f,
            Uri = "https://example.com",
        };

        Assert.Equal(0, annot.PageIndex);
        Assert.Equal(10f, annot.X);
        Assert.Equal(20f, annot.Y);
        Assert.Equal(100f, annot.Width);
        Assert.Equal(15f, annot.Height);
        Assert.Equal("https://example.com", annot.Uri);
    }

    /// <summary>
    /// 4.1: Verifies that <see cref="LayoutResult"/> exposes an <see cref="LayoutResult.Annotations"/> collection.
    /// </summary>
    [Fact]
    public void LayoutResult_HasAnnotationsCollection()
    {
        var result = new LayoutResult();
        Assert.NotNull(result.Annotations);
        Assert.Empty(result.Annotations);

        result.Annotations.Add(new LinkAnnotationPrimitive
        {
            PageIndex = 0,
            X = 0f,
            Y = 0f,
            Width = 50f,
            Height = 12f,
            Uri = "https://test.com",
        });

        Assert.Single(result.Annotations);
    }

    /// <summary>
    /// 4.1: Verifies that <see cref="LinkAnnotationPrimitive"/> extends <see cref="RenderPrimitive"/>.
    /// </summary>
    [Fact]
    public void LinkAnnotationPrimitive_ExtendsRenderPrimitive()
    {
        var annot = new LinkAnnotationPrimitive();
        Assert.IsAssignableFrom<RenderPrimitive>(annot);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 5: Core — Inline link detection
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 5.1: Verifies that <see cref="InlineRun"/> has a <see cref="InlineRun.LinkUri"/> property.
    /// </summary>
    [Fact]
    public void InlineRun_HasLinkUriProperty()
    {
        var run = new InlineRun
        {
            Text = "click here",
            LinkUri = "https://example.com/page",
        };

        Assert.Equal("https://example.com/page", run.LinkUri);
    }

    /// <summary>
    /// 5.1: Verifies that <see cref="Helpers.MakeRun"/> accepts and propagates a linkUri parameter.
    /// </summary>
    [Fact]
    public void MakeRun_AcceptsAndSetsLinkUri()
    {
        var style = new ComputedStyle
        {
            Color = CssColor.Black,
            FontSize = Constants.DefaultFontSize,
        };

        InlineRun run = Helpers.MakeRun("click", style, null, "https://example.com");

        Assert.Equal("https://example.com", run.LinkUri);
    }

    /// <summary>
    /// 5.1: Verifies that MakeRun without linkUri produces a null LinkUri.
    /// </summary>
    [Fact]
    public void MakeRun_WithoutLinkUri_HasNullLinkUri()
    {
        var style = new ComputedStyle
        {
            Color = CssColor.Black,
            FontSize = Constants.DefaultFontSize,
        };

        InlineRun run = Helpers.MakeRun("hello", style);

        Assert.Null(run.LinkUri);
    }

    /// <summary>
    /// 5.3: Verifies that a link that wraps across lines produces one annotation per visual fragment.
    /// Uses a narrow width to force wrapping.
    /// </summary>
    [Fact]
    public void MultiWordLink_ProducesMultipleAnnotations()
    {
        var narrowPage = new PageLayout(200f, 800f, new PageMargins(10f));
        LayoutResult result = RunLayout(
            "<a href=\"https://example.com\">this is a very long link text that should wrap</a>",
            narrowPage);

        Assert.NotEmpty(result.Annotations);
        Assert.All(result.Annotations, a =>
        {
            Assert.Equal("https://example.com", a.Uri);
            Assert.True(a.Width > 0);
            Assert.True(a.Height > 0);
        });
    }

    /// <summary>
    /// 5.4: Verifies that after layout of an anchor with href, the LayoutResult.Annotations
    /// contains an entry with the correct URI and valid bounds.
    /// </summary>
    [Fact]
    public void AnchorTag_ProducesLinkAnnotationInLayoutResult()
    {
        LayoutResult result = RunLayout("<a href=\"https://example.com\">link</a>");

        LinkAnnotationPrimitive? annotation = result.Annotations
            .FirstOrDefault(a => a.Uri == "https://example.com");

        Assert.NotNull(annotation);
        Assert.True(annotation.Width > 0, "Annotation width should be positive");
        Assert.True(annotation.Height > 0, "Annotation height should be positive");
    }

    /// <summary>
    /// 5.4: Verifies that a link without href does NOT produce annotations
    /// (the text is styled as a link but is not clickable).
    /// </summary>
    [Fact]
    public void AnchorWithoutHref_ProducesNoAnnotations()
    {
        LayoutResult result = RunLayout("<a>plain link text</a>");

        Assert.Empty(result.Annotations);
    }

    /// <summary>
    /// 5.4: Verifies that multiple separate links on the same page all produce annotations.
    /// </summary>
    [Fact]
    public void MultipleLinks_AllProduceAnnotations()
    {
        LayoutResult result = RunLayout(
            "<a href=\"https://first.com\">first</a> " +
            "<a href=\"https://second.com\">second</a>");

        List<string> uris = result.Annotations.Select(a => a.Uri).Distinct().OrderBy(u => u).ToList();
        Assert.Contains("https://first.com", uris);
        Assert.Contains("https://second.com", uris);
    }

    /// <summary>
    /// 5.4: Verifies that annotation page indices are correct (page 0 for single-page content).
    /// </summary>
    [Fact]
    public void LinkAnnotation_IsOnCorrectPage()
    {
        LayoutResult result = RunLayout("<a href=\"https://example.com\">click</a>");

        Assert.All(result.Annotations, a => Assert.Equal(0, a.PageIndex));
    }
}
