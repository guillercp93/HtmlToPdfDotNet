using HtmlToPdfDotNet.Library.Commons;

namespace HtmlToPdfDotNet.Tests.Css
{
    public class CssValueParserTests
    {
        // ── ParseLength ───────────────────────────────────────────────────────────

        [Theory]
        [InlineData("0", 0f)]
        [InlineData("12pt", 12f)]
        [InlineData("16px", 12f)]    // 16 * 0.75 = 12pt
        [InlineData("1in", Constants.PointsPerInch)]
        [InlineData("1cm", Constants.PointsPerCm)]
        [InlineData("10mm", Constants.PointsPerMm * 10f)]
        [InlineData("1em", 12f)]    // parentFontSize = 12
        [InlineData("auto", 0f)]    // IsAuto=true
        public void ParseLength_ReturnsCorrectPoints(string input, float expectedPt)
        {
            CssLength len = CssValueParser.ParseLength(input, parentFontSize: Constants.DefaultFontSize);

            if (input == "auto")
                Assert.True(len.IsAuto);
            else
                Assert.Equal(expectedPt, len.Points, precision: 2);
        }

        // ── ParseColor ────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("black", 0f, 0f, 0f)]
        [InlineData("white", 1f, 1f, 1f)]
        [InlineData("#ff0000", 1f, 0f, 0f)]
        [InlineData("#f00", 1f, 0f, 0f)]
        [InlineData("red", 1f, 0f, 0f)]
        public void ParseColor_ReturnsCorrectRgb(string input, float r, float g, float b)
        {
            CssColor color = CssValueParser.ParseColor(input);
            Assert.Equal(r, color.R, precision: 2);
            Assert.Equal(g, color.G, precision: 2);
            Assert.Equal(b, color.B, precision: 2);
        }

        [Fact]
        public void ParseColor_Hex6_ParsesCorrectly()
        {
            CssColor c = CssValueParser.ParseColor("#336699");
            Assert.Equal(0x33 / 255f, c.R, precision: 3);
            Assert.Equal(0x66 / 255f, c.G, precision: 3);
            Assert.Equal(0x99 / 255f, c.B, precision: 3);
        }

        [Fact]
        public void ParseColor_RgbFunction_ParsesCorrectly()
        {
            CssColor c = CssValueParser.ParseColor("rgb(51, 102, 153)");
            Assert.Equal(51 / 255f, c.R, precision: 3);
            Assert.Equal(102 / 255f, c.G, precision: 3);
            Assert.Equal(153 / 255f, c.B, precision: 3);
        }

        [Fact]
        public void ParseColor_RgbaFunctionWithSpaces_ParsesCorrectly()
        {
            CssColor c = CssValueParser.ParseColor("rgba(255, 255, 255, 0.5)");
            Assert.Equal(1f, c.R);
            Assert.Equal(1f, c.G);
            Assert.Equal(1f, c.B);
            Assert.Equal(0.5f, c.A);
        }

        [Fact]
        public void ParseColor_Hex8_ParsesCorrectly()
        {
            CssColor c = CssValueParser.ParseColor("#ffffff40");
            Assert.Equal(1f, c.R);
            Assert.Equal(1f, c.G);
            Assert.Equal(1f, c.B);
            Assert.Equal(0x40 / 255f, c.A, precision: 3);
        }

        // ── ParseEdges ────────────────────────────────────────────────────────────

        [Fact]
        public void ParseEdges_OneValue_AllSidesEqual()
        {
            CssEdges e = CssValueParser.ParseEdges("8pt");
            Assert.Equal(8f, e.Top.Points);
            Assert.Equal(8f, e.Right.Points);
            Assert.Equal(8f, e.Bottom.Points);
            Assert.Equal(8f, e.Left.Points);
        }

        [Fact]
        public void ParseEdges_TwoValues_VerticalHorizontal()
        {
            CssEdges e = CssValueParser.ParseEdges("10pt 20pt");
            Assert.Equal(10f, e.Top.Points);
            Assert.Equal(20f, e.Right.Points);
            Assert.Equal(10f, e.Bottom.Points);
            Assert.Equal(20f, e.Left.Points);
        }

        [Fact]
        public void ParseEdges_FourValues_EachSide()
        {
            CssEdges e = CssValueParser.ParseEdges("1pt 2pt 3pt 4pt");
            Assert.Equal(1f, e.Top.Points);
            Assert.Equal(2f, e.Right.Points);
            Assert.Equal(3f, e.Bottom.Points);
            Assert.Equal(4f, e.Left.Points);
        }

        // ── ParseFontSize ─────────────────────────────────────────────────────────

        [Theory]
        [InlineData("medium", 12f)]
        [InlineData("large", 13.5f)]
        [InlineData("x-large", 18f)]
        [InlineData("small", 9f)]
        [InlineData("12pt", 12f)]
        [InlineData("16px", 12f)]
        public void ParseFontSize_Keywords_ReturnCorrectPoints(string input, float expected)
        {
            float result = CssValueParser.ParseFontSize(input, Constants.DefaultFontSize);
            Assert.Equal(expected, result, precision: 1);
        }

        [Fact]
        public void ParseFontSize_Percentage_RelatesToParent()
        {
            float result = CssValueParser.ParseFontSize("150%", parentFontSize: Constants.DefaultFontSize);
            Assert.Equal(18f, result, precision: 1);
        }

        // ── ParseBorderSide ───────────────────────────────────────────────────────

        [Fact]
        public void ParseBorderSide_Shorthand_ExtractsAllParts()
        {
            CssBorderSide b = CssValueParser.ParseBorderSide("2pt solid #336699");
            Assert.Equal(2f, b.Width.Points, precision: 1);
            Assert.Equal(BorderStyle.Solid, b.Style);
            Assert.Equal(0x33 / 255f, b.Color.R, precision: 3);
        }

        [Fact]
        public void ParseBorderSide_None_ReturnsNoBorder()
        {
            CssBorderSide b = CssValueParser.ParseBorderSide("none");
            Assert.False(b.IsVisible);
        }

        [Fact]
        public void ParseBorderSide_WithRgbaAndSpaces_ParsesCorrectly()
        {
            CssBorderSide b = CssValueParser.ParseBorderSide("1px solid rgba(255, 255, 255, 0.5)");
            Assert.Equal(1 * Constants.PointsPerPx, b.Width.Points);
            Assert.Equal(BorderStyle.Solid, b.Style);
            Assert.Equal(1f, b.Color.R);
            Assert.Equal(0.5f, b.Color.A);
        }

        // ── ParseFontWeight ───────────────────────────────────────────────────────

        [Theory]
        [InlineData("bold", FontWeight.Bold)]
        [InlineData("700", FontWeight.Bold)]
        [InlineData("normal", FontWeight.Normal)]
        [InlineData("400", FontWeight.Normal)]
        public void ParseFontWeight_ReturnsCorrectWeight(string input, FontWeight expected)
        {
            Assert.Equal(expected, CssValueParser.ParseFontWeight(input));
        }

        // ── ParseTextTransform ────────────────────────────────────────────────────

        [Theory]
        [InlineData("capitalize", TextTransForm.Capitalize)]
        [InlineData("uppercase", TextTransForm.Uppercase)]
        [InlineData("lowercase", TextTransForm.Lowercase)]
        [InlineData("full-width", TextTransForm.FullWidth)]
        [InlineData("none", TextTransForm.None)]
        [InlineData("INVALID", TextTransForm.None)]
        public void ParseTextTransform_ReturnsCorrectTransform(string input, TextTransForm expected)
        {
            Assert.Equal(expected, CssValueParser.ParseTextTransform(input));
        }

        // ── ParseTextDecoration ───────────────────────────────────────────────────

        [Theory]
        [InlineData("underline", TextDecoration.Underline)]
        [InlineData("overline", TextDecoration.Overline)]
        [InlineData("line-through", TextDecoration.LineThrough)]
        [InlineData("none", TextDecoration.None)]
        [InlineData("INVALID", TextDecoration.None)]
        public void ParseTextDecoration_ReturnsCorrectDecoration(string input, TextDecoration expected)
        {
            Assert.Equal(expected, CssValueParser.ParseTextDecoration(input));
        }
    }
}