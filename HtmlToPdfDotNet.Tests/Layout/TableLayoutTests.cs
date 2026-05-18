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
    }
}
