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

    // ── FlexWrap enum ───────────────────────────────────────────────────────────

    [Fact]
    public void FlexWrap_HasThreeValues()
    {
        Assert.Equal(3, Enum.GetNames<FlexWrap>().Length);
    }

    [Fact]
    public void FlexWrap_Nowrap_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(FlexWrap), "Nowrap"));
    }

    [Fact]
    public void FlexWrap_Wrap_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(FlexWrap), "Wrap"));
    }

    [Fact]
    public void FlexWrap_WrapReverse_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(FlexWrap), "WrapReverse"));
    }

    // ── AlignContent enum ────────────────────────────────────────────────────────

    [Fact]
    public void AlignContent_HasSixValues()
    {
        Assert.Equal(6, Enum.GetNames<AlignContent>().Length);
    }

    [Fact]
    public void AlignContent_FlexStart_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(AlignContent), "FlexStart"));
    }

    [Fact]
    public void AlignContent_FlexEnd_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(AlignContent), "FlexEnd"));
    }

    [Fact]
    public void AlignContent_Center_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(AlignContent), "Center"));
    }

    [Fact]
    public void AlignContent_SpaceBetween_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(AlignContent), "SpaceBetween"));
    }

    [Fact]
    public void AlignContent_SpaceAround_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(AlignContent), "SpaceAround"));
    }

    [Fact]
    public void AlignContent_Stretch_IsDefined()
    {
        Assert.True(Enum.IsDefined(typeof(AlignContent), "Stretch"));
    }

    // ── ComputedStyle new flex defaults ───────────────────────────────────────────

    [Fact]
    public void ComputedStyle_FlexWrap_DefaultIsNowrap()
    {
        ComputedStyle style = new();
        Assert.Equal(FlexWrap.Nowrap, style.FlexWrap);
    }

    [Fact]
    public void ComputedStyle_FlexGrow_DefaultIsZero()
    {
        ComputedStyle style = new();
        Assert.Equal(0f, style.FlexGrow);
    }

    [Fact]
    public void ComputedStyle_FlexShrink_DefaultIsOne()
    {
        ComputedStyle style = new();
        Assert.Equal(1f, style.FlexShrink);
    }

    [Fact]
    public void ComputedStyle_FlexBasis_DefaultIsAuto()
    {
        ComputedStyle style = new();
        Assert.True(style.FlexBasis.IsAuto);
    }

    [Fact]
    public void ComputedStyle_AlignContent_DefaultIsStretch()
    {
        ComputedStyle style = new();
        Assert.Equal(AlignContent.Stretch, style.AlignContent);
    }
}
