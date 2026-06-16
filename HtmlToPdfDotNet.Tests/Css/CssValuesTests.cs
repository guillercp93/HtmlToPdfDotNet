using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Tests.Css;

/// <summary>
/// Tests for CSS value enums and types.
/// </summary>
public class CssValuesTests
{
    // ── ListStyleType enum ─────────────────────────────────────────────────

    [Fact]
    public void ListStyleType_HasAllNineValues()
    {
        Assert.Equal(9, Enum.GetNames<ListStyleType>().Length);
    }

    [Fact]
    public void ListStyleType_Disc_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(ListStyleType), "Disc"));
    }

    [Fact]
    public void ListStyleType_Circle_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(ListStyleType), "Circle"));
    }

    [Fact]
    public void ListStyleType_Square_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(ListStyleType), "Square"));
    }

    [Fact]
    public void ListStyleType_Decimal_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(ListStyleType), "Decimal"));
    }

    [Fact]
    public void ListStyleType_LowerAlpha_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(ListStyleType), "LowerAlpha"));
    }

    [Fact]
    public void ListStyleType_UpperAlpha_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(ListStyleType), "UpperAlpha"));
    }

    [Fact]
    public void ListStyleType_LowerRoman_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(ListStyleType), "LowerRoman"));
    }

    [Fact]
    public void ListStyleType_UpperRoman_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(ListStyleType), "UpperRoman"));
    }

    [Fact]
    public void ListStyleType_None_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(ListStyleType), "None"));
    }

    // ── DisplayType.ListItem ────────────────────────────────────────────────

    [Fact]
    public void DisplayType_ListItem_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(DisplayType), "ListItem"));
    }

    // ── ComputedStyle.ListStyleType ─────────────────────────────────────────

    [Fact]
    public void ComputedStyle_ListStyleType_DefaultIsDisc()
    {
        ComputedStyle style = new();
        Assert.Equal(ListStyleType.Disc, style.ListStyleType);
    }

    // ── Gap property ──────────────────────────────────────────────────────────

    /// <summary>
    /// GIVEN a new ComputedStyle
    /// THEN Gap MUST default to CssLength.Zero
    /// </summary>
    [Fact]
    public void ComputedStyle_Gap_DefaultIsZero()
    {
        ComputedStyle style = new();
        Assert.Equal(CssLength.Zero, style.Gap);
        Assert.Equal(0f, style.Gap.Points);
    }
}
