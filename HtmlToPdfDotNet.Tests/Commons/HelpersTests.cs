using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using Xunit;

namespace HtmlToPdfDotNet.Tests.Commons;

public class HelpersTests
{
    // ── CleanHtml ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("<p>plain</p>", "<p>plain</p>")]
    [InlineData("a\0b", "ab")]
    [InlineData("line1\r\nline2\tend", "line1line2end")]          // CR, LF and TAB are control characters
    [InlineData("\u0001\u001Fx\u007F", "x\u007F")]                // DEL (0x7F) is above space and kept
    [InlineData("&amp;&lt;&#x2B;&#43;", "&<++")]                  // entities are decoded
    [InlineData("&am\0p;", "&")]                                  // filtering happens before decoding
    [InlineData("﻿​<b>x</b>​﻿", "<b>x</b>")]  // BOM and ZWSP trimmed at both ends
    [InlineData("a﻿b", "a﻿b")]                          // inner BOM is kept
    [InlineData("  spaced  ", "  spaced  ")]                      // ordinary whitespace is not trimmed
    [InlineData("\0\0", "")]
    public void CleanHtml_FiltersControlCharsDecodesAndTrims(string? input, string expected)
    {
        string result = Helpers.CleanHtml(input!);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CleanHtml_LargeInputWithControlChars_RemovesAllOfThem()
    {
        string input = string.Concat(Enumerable.Repeat("<td>\tcell\n</td>", 500));
        string expected = string.Concat(Enumerable.Repeat("<td>cell</td>", 500));

        Assert.Equal(expected, Helpers.CleanHtml(input));
    }

    // ── ShiftAll ──────────────────────────────────────────────────────────────

    private static LineBox LineWith(params (string Text, float X)[] items)
    {
        LineBox line = new();
        foreach ((string text, float x) in items)
            line.Items.Add((new InlineRun { Text = text }, x));
        return line;
    }

    [Fact]
    public void ShiftAll_PositiveOffset_ShiftsEveryItemPreservingRunsAndOrder()
    {
        LineBox line = LineWith(("a", 0f), ("b", 10.5f), ("c", 3f));
        List<InlineRun> runsBefore = line.Items.Select(i => i.Run).ToList();
        List<(InlineRun Run, float X)> listBefore = line.Items;

        Helpers.ShiftAll(line, 2.25f);

        Assert.Same(listBefore, line.Items);
        Assert.Equal(3, line.Items.Count);
        Assert.Equal(runsBefore, line.Items.Select(i => i.Run).ToList());
        Assert.Equal([2.25f, 12.75f, 5.25f], line.Items.Select(i => i.X).ToArray());
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    public void ShiftAll_NonPositiveOffset_LeavesItemsUnchanged(float offset)
    {
        LineBox line = LineWith(("a", 1f), ("b", 2f));

        Helpers.ShiftAll(line, offset);

        Assert.Equal([1f, 2f], line.Items.Select(i => i.X).ToArray());
    }

    [Fact]
    public void ShiftAll_EmptyLine_StaysEmpty()
    {
        LineBox line = new();

        Helpers.ShiftAll(line, 4f);

        Assert.Empty(line.Items);
    }
}
