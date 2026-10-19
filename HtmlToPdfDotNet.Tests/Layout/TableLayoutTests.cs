using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;
using HtmlToPdfDotNet.Library.Models.Writer;
using HtmlNode = HtmlAgilityPack.HtmlNode;
using Xunit;

namespace HtmlToPdfDotNet.Tests.Layout
{
    public class TableLayoutTests
    {
        [Fact]
        public void TableLayout_RowBackground_EmitsRectPrimitive()
        {
            // Arrange
            var html = @"<table style='background-color: #ff0000;'>
                            <tr style='background-color: #00ff00;'>
                                <td>Cell 1</td>
                            </tr>
                         </table>";
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);
            var tableNode = doc.DocumentNode.SelectSingleNode("//table");
            var trNode = doc.DocumentNode.SelectSingleNode("//tr");
            var tdNode = doc.DocumentNode.SelectSingleNode("//td");

            var styles = new Dictionary<HtmlNode, ComputedStyle>();
            var resolver = new StyleResolver();
            
            // Mocking style resolution for simplicity
            var tableStyle = new ComputedStyle { Display = DisplayType.Table, BackgroundColor = CssColor.FromRgb(255, 0, 0) };
            var trStyle = new ComputedStyle { Display = DisplayType.Block, BackgroundColor = CssColor.FromRgb(0, 255, 0) };
            var tdStyle = new ComputedStyle { Display = DisplayType.Block, Padding = CssEdges.Zero };
            
            styles[tableNode] = tableStyle;
            styles[trNode] = trStyle;
            styles[tdNode] = tdStyle;
            styles[tdNode.FirstChild] = new ComputedStyle { Display = DisplayType.Inline };

            var result = new LayoutResult();
            float newCursorY;
            int newPage;

            // Act
            TableLayoutEngine.Layout(tableNode, tableStyle, styles, 0, 500, result, 0, 0, 800, 
                                     out newCursorY, out newPage, null, "");

            // Assert
            // We expect at least two RectPrimitives: one for table background, one for row background
            var rects = result.Primitives.OfType<RectPrimitive>().ToList();
            Assert.Contains(rects, r => r.Fill.G == 1f); // Row background (Green)
            Assert.Contains(rects, r => r.Fill.R == 1f); // Table background (Red)
        }

        [Fact]
        public void TableLayout_HrInsideCell_EmitsBorderLinePrimitive()
        {
            // Arrange
            var html = "<table><tr><td><hr style='border-bottom: 2px solid #0000ff;' /></td></tr></table>";
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);
            var tableNode = doc.DocumentNode.SelectSingleNode("//table");
            var trNode = doc.DocumentNode.SelectSingleNode("//tr");
            var tdNode = doc.DocumentNode.SelectSingleNode("//td");
            var hrNode = doc.DocumentNode.SelectSingleNode("//hr");

            var styles = new Dictionary<HtmlNode, ComputedStyle>();
            var tableStyle = new ComputedStyle { Display = DisplayType.Table };
            var trStyle = new ComputedStyle { Display = DisplayType.Block };
            var tdStyle = new ComputedStyle { Display = DisplayType.Block, Padding = CssEdges.Zero };
            var hrStyle = new ComputedStyle { 
                Display = DisplayType.Block, 
                BorderBottom = new CssBorderSide(new CssLength(2f), BorderStyle.Solid, CssColor.FromRgb(0, 0, 255)),
                Margin = CssEdges.Zero
            };

            styles[tableNode] = tableStyle;
            styles[trNode] = trStyle;
            styles[tdNode] = tdStyle;
            styles[hrNode] = hrStyle;

            var result = new LayoutResult();
            float newCursorY;
            int newPage;

            // Act
            TableLayoutEngine.Layout(tableNode, tableStyle, styles, 0, 500, result, 0, 0, 800, 
                                     out newCursorY, out newPage, null, "");

            // Assert
            var borders = result.Primitives.OfType<BorderLinePrimitive>().ToList();
            Assert.NotEmpty(borders);
            Assert.Contains(borders, b => b.Color.B == 1f && b.Width == 2f);
        }
    
        private const string RoundedBoxStyle =
            "border: 2px solid #ff0000; border-radius: 6px; background-color: #00ff00; width: 60px; height: 20px;";

        private static LayoutResult RunLayout(string html)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);
            Dictionary<HtmlNode, ComputedStyle> styles = new StyleResolver().Resolve(doc.DocumentNode);
            return new BlockLayoutEngine(PageLayout.A4, styles).Layout(doc.DocumentNode);
        }

        private static string TableWithRepeatedHeader(string headerCell, int bodyRows)
        {
            var body = string.Concat(Enumerable.Range(0, bodyRows).Select(i => $"<tr><td>Row {i}</td></tr>"));
            return $"<table><thead><tr><th>{headerCell}</th></tr></thead><tbody>{body}</tbody></table>";
        }

        [Fact]
        public void TableLayout_RoundedBorderedBoxInsideCell_KeepsRadiusAndStroke()
        {
            // Arrange
            RectPrimitive expected = RunLayout($"<div style='{RoundedBoxStyle}'></div>")
                .Primitives.OfType<RectPrimitive>().Single(r => r.Fill.G == 1f);
            Assert.True(expected.BorderRadius > 0f, "Reference box must be rounded");

            // Act
            LayoutResult result = RunLayout($"<table><tr><td><div style='{RoundedBoxStyle}'></div></td></tr></table>");

            // Assert
            RectPrimitive actual = result.Primitives.OfType<RectPrimitive>().Single(r => r.Fill.G == 1f);
            Assert.Equal(expected.BorderRadius, actual.BorderRadius);
            Assert.Equal(expected.Stroke, actual.Stroke);
            Assert.Equal(expected.StrokeWidth, actual.StrokeWidth);
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);
        }

        [Fact]
        public void TableLayout_RoundedBoxInRepeatedHeader_KeepsRadiusOnEveryPage()
        {
            // Act
            LayoutResult result = RunLayout(TableWithRepeatedHeader($"<div style='{RoundedBoxStyle}'></div>", 120));

            // Assert
            Assert.True(result.PageCount > 1, "Table must span several pages");
            List<RectPrimitive> boxes = result.Primitives.OfType<RectPrimitive>().Where(r => r.Fill.G == 1f).ToList();
            Assert.Equal(Enumerable.Range(0, result.PageCount), boxes.Select(b => b.PageIndex).Order());
            Assert.All(boxes, b => Assert.True(b.BorderRadius > 0f && b.StrokeWidth > 0f));
        }

        [Fact]
        public void TableLayout_UnderlineInRepeatedHeader_IsEmittedOnEveryPage()
        {
            // Act
            LayoutResult result = RunLayout(TableWithRepeatedHeader(
                "<span style='text-decoration: underline; color: #ff0000;'>Head</span>", 120));

            // Assert
            Assert.True(result.PageCount > 1, "Table must span several pages");
            IEnumerable<int> underlinePages = result.Primitives.OfType<BorderLinePrimitive>()
                .Where(b => b.Color.R == 1f && b.Color.G == 0f)
                .Select(b => b.PageIndex)
                .Order();
            Assert.Equal(Enumerable.Range(0, result.PageCount), underlinePages);
        }

        [Fact]
        public void TableLayout_PageBreakInsideCell_IsNotEmittedByTheTable()
        {
            // Arrange: the table owns row pagination; a forced break inside a cell
            // must not leak a page-break marker into the table output.
            const string html = "<table><tr><td><p>First</p><p style='page-break-before: always;'>Second</p></td></tr></table>";

            // Act
            LayoutResult result = RunLayout(html);

            // Assert
            Assert.Empty(result.Primitives.OfType<PageBreakPrimitive>());
            Assert.Equal(1, result.PageCount);
            Assert.All(result.Primitives, p => Assert.Equal(0, p.PageIndex));
            Assert.Contains(result.Primitives.OfType<TextPrimitive>(), t => t.Text.Contains("Second"));
        }
}
}
