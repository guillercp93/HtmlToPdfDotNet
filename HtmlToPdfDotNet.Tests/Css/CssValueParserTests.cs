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

        [Theory]
        [InlineData("1pt 2pt 3pt", 1f, 2f, 3f, 2f)]                 // three values: left mirrors right
        [InlineData("1pt 2pt 3pt 4pt 5pt", 1f, 2f, 3f, 4f)]         // extra values are ignored
        [InlineData("   1pt    2pt   ", 1f, 2f, 1f, 2f)]            // repeated and surrounding spaces
        [InlineData("1pt\t2pt", 0f, 0f, 0f, 0f)]                    // tab is not a separator: one invalid token
        [InlineData("\t3pt\n", 3f, 3f, 3f, 3f)]                     // surrounding whitespace is trimmed
        [InlineData("abc 2pt", 0f, 2f, 0f, 2f)]                     // invalid token parses as zero
        [InlineData("8px 1in", 6f, Constants.PointsPerInch, 6f, Constants.PointsPerInch)]
        [InlineData("2EM 0", 20f, 0f, 20f, 0f)]                     // case-insensitive units, parent font size 10
        [InlineData("4 none", 3f, 0f, 3f, 0f)]                      // unitless is px
        public void ParseEdges_VariousInputs_MatchesCurrentBehavior(string input, float top, float right, float bottom, float left)
        {
            CssEdges e = CssValueParser.ParseEdges(input, parentFontSize: 10f);
            Assert.Equal(top, e.Top.Points);
            Assert.Equal(right, e.Right.Points);
            Assert.Equal(bottom, e.Bottom.Points);
            Assert.Equal(left, e.Left.Points);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseEdges_NullOrWhitespace_ReturnsZero(string? input)
        {
            CssEdges e = CssValueParser.ParseEdges(input);
            Assert.True(e.Top.IsZero);
            Assert.True(e.Right.IsZero);
            Assert.True(e.Bottom.IsZero);
            Assert.True(e.Left.IsZero);
        }

        [Fact]
        public void ParseEdges_AutoToken_KeepsAutoFlag()
        {
            CssEdges e = CssValueParser.ParseEdges("auto 10pt");
            Assert.True(e.Top.IsAuto);
            Assert.False(e.Right.IsAuto);
            Assert.Equal(10f, e.Right.Points);
            Assert.True(e.Bottom.IsAuto);
            Assert.False(e.Left.IsAuto);
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

        // ── ParseDisplay ───────────────────────────────────────────────────────

        [Fact]
        public void ParseDisplay_ListItem_ReturnsListItem()
        {
            Assert.Equal(DisplayType.ListItem, CssValueParser.ParseDisplay("list-item"));
        }

        [Fact]
        public void ParseDisplay_ListItemWithUpperCase_ReturnsListItem()
        {
            Assert.Equal(DisplayType.ListItem, CssValueParser.ParseDisplay("LIST-ITEM"));
        }

        // ── ParseListStyleType ──────────────────────────────────────────────────

        [Theory]
        [InlineData("disc", ListStyleType.Disc)]
        [InlineData("circle", ListStyleType.Circle)]
        [InlineData("square", ListStyleType.Square)]
        [InlineData("decimal", ListStyleType.Decimal)]
        [InlineData("lower-alpha", ListStyleType.LowerAlpha)]
        [InlineData("upper-alpha", ListStyleType.UpperAlpha)]
        [InlineData("lower-roman", ListStyleType.LowerRoman)]
        [InlineData("upper-roman", ListStyleType.UpperRoman)]
        [InlineData("none", ListStyleType.None)]
        [InlineData("DISC", ListStyleType.Disc)]
        [InlineData("invalid-value", ListStyleType.Disc)]
        [InlineData("", ListStyleType.Disc)]
        [InlineData(null, ListStyleType.Disc)]
        public void ParseListStyleType_ReturnsCorrectValue(string? input, ListStyleType expected)
        {
            Assert.Equal(expected, CssValueParser.ParseListStyleType(input));
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

        // ── ParseFlexWrap ────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("nowrap", FlexWrap.Nowrap)]
        [InlineData("wrap", FlexWrap.Wrap)]
        [InlineData("wrap-reverse", FlexWrap.WrapReverse)]
        [InlineData("NOWRAP", FlexWrap.Nowrap)]
        [InlineData("invalid", FlexWrap.Nowrap)]
        [InlineData("", FlexWrap.Nowrap)]
        [InlineData(null, FlexWrap.Nowrap)]
        public void ParseFlexWrap_ReturnsCorrectValue(string? input, FlexWrap expected)
        {
            Assert.Equal(expected, CssValueParser.ParseFlexWrap(input));
        }

        // ── ParseAlignContent ─────────────────────────────────────────────────────────

        [Theory]
        [InlineData("flex-start", AlignContent.FlexStart)]
        [InlineData("flex-end", AlignContent.FlexEnd)]
        [InlineData("center", AlignContent.Center)]
        [InlineData("space-between", AlignContent.SpaceBetween)]
        [InlineData("space-around", AlignContent.SpaceAround)]
        [InlineData("stretch", AlignContent.Stretch)]
        [InlineData("FLEX-START", AlignContent.FlexStart)]
        [InlineData("invalid", AlignContent.Stretch)]
        [InlineData("", AlignContent.Stretch)]
        [InlineData(null, AlignContent.Stretch)]
        public void ParseAlignContent_ReturnsCorrectValue(string? input, AlignContent expected)
        {
            Assert.Equal(expected, CssValueParser.ParseAlignContent(input));
        }

        // ── ParseFlexShorthand ─────────────────────────────────────────────────────────

        [Fact]
        public void ParseFlexShorthand_None_ReturnsZeroGrowZeroShrinkAutoBasis()
        {
            var (grow, shrink, basis) = CssValueParser.ParseFlexShorthand("none", 12f);
            Assert.Equal(0f, grow);
            Assert.Equal(0f, shrink);
            Assert.True(basis.IsAuto);
        }

        [Fact]
        public void ParseFlexShorthand_SingleNumber_SetsGrowShrink1Basis0Percent()
        {
            var (grow, shrink, basis) = CssValueParser.ParseFlexShorthand("2", 12f);
            Assert.Equal(2f, grow);
            Assert.Equal(1f, shrink);
            Assert.False(basis.IsAuto);
            Assert.Equal(0f, basis.Points);
        }

        [Fact]
        public void ParseFlexShorthand_TwoNumbers_SetsGrowShrinkBasis0Percent()
        {
            var (grow, shrink, basis) = CssValueParser.ParseFlexShorthand("2 3", 12f);
            Assert.Equal(2f, grow);
            Assert.Equal(3f, shrink);
            Assert.False(basis.IsAuto);
            Assert.Equal(0f, basis.Points);
        }

        [Fact]
        public void ParseFlexShorthand_NumberAndLength_SetsGrowBasis()
        {
            var (grow, shrink, basis) = CssValueParser.ParseFlexShorthand("1 200px", 12f);
            Assert.Equal(1f, grow);
            Assert.Equal(1f, shrink);  // default
            Assert.Equal(200f * Constants.PointsPerPx, basis.Points);
        }

        [Fact]
        public void ParseFlexShorthand_ThreeValues_SetsAll()
        {
            var (grow, shrink, basis) = CssValueParser.ParseFlexShorthand("1 2 100px", 12f);
            Assert.Equal(1f, grow);
            Assert.Equal(2f, shrink);
            Assert.Equal(100f * Constants.PointsPerPx, basis.Points);
        }

        [Fact]
        public void ParseFlexShorthand_Default_CorrectValues()
        {
            var (grow, shrink, basis) = CssValueParser.ParseFlexShorthand("0 1 auto", 12f);
            Assert.Equal(0f, grow);
            Assert.Equal(1f, shrink);
            Assert.True(basis.IsAuto);
        }

        [Fact]
        public void ParseFlexShorthand_EmptyString_ReturnsDefaults()
        {
            var (grow, shrink, basis) = CssValueParser.ParseFlexShorthand("", 12f);
            Assert.Equal(0f, grow);
            Assert.Equal(1f, shrink);
            Assert.True(basis.IsAuto);
        }

        // ── flex-grow / flex-shrink ───────────────────────────────────────────────────

        [Theory]
        [InlineData("0", 0f)]
        [InlineData("1", 1f)]
        [InlineData("2.5", 2.5f)]
        [InlineData("invalid", 0f)]
        [InlineData("", 0f)]
        public void ParseFlexGrow_ReturnsCorrectValue(string input, float expected)
        {
            Assert.Equal(expected, CssValueParser.ParseFlexGrow(input));
        }

        [Theory]
        [InlineData("0", 0f)]
        [InlineData("1", 1f)]
        [InlineData("0.5", 0.5f)]
        [InlineData("invalid", 1f)]
        [InlineData("", 1f)]
        public void ParseFlexShrink_ReturnsCorrectValue(string input, float expected)
        {
            Assert.Equal(expected, CssValueParser.ParseFlexShrink(input));
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("-0.5")]
        public void ParseFlexGrow_Negative_FallsBackToZero(string input)
        {
            Assert.Equal(0f, CssValueParser.ParseFlexGrow(input));
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("-0.5")]
        public void ParseFlexShrink_Negative_FallsBackToOne(string input)
        {
            Assert.Equal(1f, CssValueParser.ParseFlexShrink(input));
        }

        // ── ParseFlexShorthand: keywords, single basis, invalid input ─────────────────

        [Theory]
        [InlineData("auto", 1f, 1f)]
        [InlineData("AUTO", 1f, 1f)]
        [InlineData("initial", 0f, 1f)]
        [InlineData("none", 0f, 0f)]
        public void ParseFlexShorthand_Keyword_ReturnsSpecValuesWithAutoBasis(string input, float expectedGrow, float expectedShrink)
        {
            var (grow, shrink, basis) = CssValueParser.ParseFlexShorthand(input, 12f);
            Assert.Equal(expectedGrow, grow);
            Assert.Equal(expectedShrink, shrink);
            Assert.True(basis.IsAuto);
        }

        [Fact]
        public void ParseFlexShorthand_SingleLength_SetsGrow1Shrink1AndBasis()
        {
            var (grow, shrink, basis) = CssValueParser.ParseFlexShorthand("200px", 12f);
            Assert.Equal(1f, grow);
            Assert.Equal(1f, shrink);
            Assert.False(basis.IsAuto);
            Assert.Equal(200f * Constants.PointsPerPx, basis.Points);
        }

        [Fact]
        public void ParseFlexShorthand_SinglePercentage_SetsGrow1Shrink1NonAutoBasis()
        {
            var (grow, shrink, basis) = CssValueParser.ParseFlexShorthand("50%", 12f);
            Assert.Equal(1f, grow);
            Assert.Equal(1f, shrink);
            Assert.False(basis.IsAuto);
        }

        [Fact]
        public void ParseFlexShorthand_NumberAndAuto_SetsGrowShrink1AutoBasis()
        {
            var (grow, shrink, basis) = CssValueParser.ParseFlexShorthand("2 auto", 12f);
            Assert.Equal(2f, grow);
            Assert.Equal(1f, shrink);
            Assert.True(basis.IsAuto);
        }

        [Fact]
        public void ParseFlexShorthand_ThreeValuesWithUnitlessZeroBasis_SetsZeroBasis()
        {
            var (grow, shrink, basis) = CssValueParser.ParseFlexShorthand("1 1 0", 12f);
            Assert.Equal(1f, grow);
            Assert.Equal(1f, shrink);
            Assert.False(basis.IsAuto);
            Assert.Equal(0f, basis.Points);
        }

        [Theory]
        [InlineData("garbage")]
        [InlineData("1 garbage")]
        [InlineData("1 2 3 4")]
        [InlineData("-1")]
        [InlineData("1 -2")]
        [InlineData("-1 1 100px")]
        public void ParseFlexShorthand_InvalidOrNegative_ReturnsInitialValue(string input)
        {
            var (grow, shrink, basis) = CssValueParser.ParseFlexShorthand(input, 12f);
            Assert.Equal(0f, grow);
            Assert.Equal(1f, shrink);
            Assert.True(basis.IsAuto);
        }
    }
}
