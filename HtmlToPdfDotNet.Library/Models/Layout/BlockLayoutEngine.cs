using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Library.Models.Layout;

public sealed class BlockLayoutEngine
{
    private readonly PageLayout _page;
    private readonly Dictionary<HtmlNode, ComputedStyle> _styles;
    private readonly LayoutResult _result = new();

    #region Pagination Status
    private int _currentPage = 0;
    private float _cursorY = 0f;
    private float _contentTop = 0f;
    private float _contentLeft = 0f;
    private float _pageContentH = 0f;
    #endregion

    public BlockLayoutEngine(PageLayout page, Dictionary<HtmlNode, ComputedStyle> styles)
    {
        _page = page;
        _styles = styles;
        _contentTop = page.Margins.Top;
        _contentLeft = page.Margins.Left;
        _pageContentH = page.ContentHeight;
    }

    public LayoutResult Layout(HtmlNode root)
    {
        _result.PageCount = 1;
        _currentPage = 0;
        _cursorY = 0f;

        LayoutChildren(root, _contentLeft, _page.ContentWidth);

        _result.PageCount = _currentPage + 1;
        return _result;
    }

    private float LayoutChildren(HtmlNode parent, float x, float availableWidth)
    {
        float startY = _cursorY;

        foreach (var child in parent.ChildNodes)
        {
            if (!_styles.TryGetValue(child, out var style)) continue;
            if (style.Display == DisplayType.None) continue;

            if (child.NodeType == HtmlNodeType.Text)
            {
                var text = Helpers.NormalizeText(child.InnerText);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    LayoutTextRuns(new[] { Helpers.MakeRun(text, style) }, x, availableWidth, style.TextAlign);
                }
                continue;
            }

            if (child.NodeType != HtmlNodeType.Element) continue;
            if (style.PageBreakBefore)
            {
                AdvancePage();
            }

            var display = style.Display;
            if (display == DisplayType.Block || display == DisplayType.InlineBlock || display == DisplayType.Table)
            {
                LayoutBlock(child, style, x, availableWidth);
            }
            else
            {
                LayoutInlineContainer(child, style, x, availableWidth);
            }

            if (style.PageBreakAfter)
            {
                AdvancePage();
            }
        }

        return _cursorY - startY;
    }

    private void LayoutBlock(HtmlNode node, ComputedStyle style, float parentX, float availableWidth)
    {
        var box = new BoxModel(node, style);
        box.ResolveWidth(availableWidth);
        box.X = parentX;
        box.Y = _cursorY;

        // Add Margin Top to cursor
        _cursorY += style.Margin.Top.Points;

        float boxTop = _cursorY; // start of border box

        // Check if it fits on the page (if it's smaller than the entire page)
        float estimatedHeight = style.FontSize * style.LineHeight * 2 + style.Padding.Vertical.Points;
        if (estimatedHeight < _pageContentH && _cursorY + estimatedHeight > _pageContentH)
        {
            AdvancePage();
        }

        // Move cursor to start of content area
        _cursorY += style.BorderTop.Width.Points + style.Padding.Top.Points;

        float contentX = parentX + style.Margin.Left.Points + style.BorderLeft.Width.Points + style.Padding.Left.Points;
        float contentWidth = box.ContentWidth;

        // Layout children nodes
        float childrenHeight = LayoutBlockContent(node, style, contentX, contentWidth);

        float contentHeight = Math.Max(
            style.Height.IsAuto ? childrenHeight : style.Height.Points,
            childrenHeight);

        box.ContentHeight = contentHeight;
        _cursorY = boxTop + style.BorderTop.Width.Points + style.Padding.Top.Points + contentHeight
                          + style.Padding.Bottom.Points + style.BorderBottom.Width.Points;

        // Emit box primitives
        EmitBox(box, boxTop, parentX + style.Margin.Left.Points);

        _cursorY += style.Margin.Bottom.Points;

        // Check page overflow
        CheckPageOverflow();
    }

    private float LayoutBlockContent(HtmlNode node, ComputedStyle style, float contentX, float contentWidth)
    {
        // Are all children inline (text and inline elements)?
        if (Helpers.HasOnlyInlineContent(node, _styles))
        {
            return LayoutInlineContent(node, style, contentX, contentWidth);
        }

        // Mixed children or only blocks
        float startY = _cursorY;
        LayoutChildren(node, contentX, contentWidth);
        return _cursorY - startY;
    }

    private float LayoutInlineContent(HtmlNode node, ComputedStyle containerStyle, float contentX, float contentWidth)
    {
        var runs = CollectInlineRuns(node);
        if (runs.Count == 0)
        {
            return 0f;
        }

        return LayoutTextRuns(runs, contentX, contentWidth, containerStyle.TextAlign);
    }

    private float LayoutInlineContainer(HtmlNode node, ComputedStyle style, float x, float availableWidth)
    {
        var runs = CollectInlineRuns(node);
        if (runs.Count == 0)
        {
            return 0f;
        }
        return LayoutTextRuns(runs, x, availableWidth, style.TextAlign);
    }

    private float LayoutTextRuns(IReadOnlyList<InlineRun> runs, float contentX, float contentWidth, TextAlign align)
    {
        if (runs.Count == 0) return 0f;

        var lines = InlineLayoutEngine.Layout(runs, contentWidth, align);
        float startY = _cursorY;

        foreach (var line in lines)
        {
            // Check if the line fits on the current page
            if (_cursorY + line.LineHeight > _pageContentH)
            {
                AdvancePage();
            }

            // Baseline Y (reference: inside the page, Y from top)
            float baselineY = _cursorY + line.Ascent;

            foreach (var (runItem, runX) in line.Items)
            {
                if (string.IsNullOrEmpty(runItem.Text)) continue;

                _result.Primitives.Add(new TextPrimitive
                {
                    PageIndex = _currentPage,
                    X = contentX + runX,
                    Y = baselineY,      // the writer inverts to PDF coordinates
                    Text = runItem.Text,
                    FontName = runItem.FontName,
                    FontSize = runItem.FontSize,
                    Bold = runItem.Bold,
                    Italic = runItem.Italic,
                    Color = runItem.Color,
                });
            }

            _cursorY += line.LineHeight;
        }

        return _cursorY - startY;
    }

    private List<InlineRun> CollectInlineRuns(HtmlNode node)
    {
        var runs = new List<InlineRun>();
        CollectRunsRecursive(node, runs);
        return runs;
    }

    private void CollectRunsRecursive(HtmlNode node, List<InlineRun> runs)
    {
        foreach (var child in node.ChildNodes)
        {
            if (!_styles.TryGetValue(child, out var style)) continue;
            if (style.Display == DisplayType.None) continue;

            if (child.NodeType == HtmlNodeType.Text)
            {
                var text = Helpers.NormalizeText(child.InnerText);
                if (!string.IsNullOrEmpty(text))
                {
                    runs.Add(Helpers.MakeRun(text, style));
                }
            }
            else if (child.NodeType == HtmlNodeType.Element)
            {
                if (style.Display == DisplayType.Inline || style.Display == DisplayType.InlineBlock)
                {
                    CollectRunsRecursive(child, runs);
                }
                // Blocks inside inline → ignore in this pass (edge case)
            }
        }
    }

    private void EmitBox(BoxModel box, float borderBoxTop, float borderBoxX)
    {
        float bbW = box.BorderBoxWidth;
        float bbH = box.BorderBoxHeight;

        // Background
        if (box.Style.BackgroundColor.A > 0f)
        {
            _result.Primitives.Add(new RectPrimitive
            {
                PageIndex = _currentPage,
                X = borderBoxX,
                Y = borderBoxTop,
                Width = bbW,
                Height = bbH,
                Fill = box.Style.BackgroundColor,
            });
        }

        // Bordes
        EmitBorder(box.Style.BorderTop, borderBoxX, borderBoxTop, borderBoxX + bbW, borderBoxTop);
        EmitBorder(box.Style.BorderBottom, borderBoxX, borderBoxTop + bbH, borderBoxX + bbW, borderBoxTop + bbH);
        EmitBorder(box.Style.BorderLeft, borderBoxX, borderBoxTop, borderBoxX, borderBoxTop + bbH);
        EmitBorder(box.Style.BorderRight, borderBoxX + bbW, borderBoxTop, borderBoxX + bbW, borderBoxTop + bbH);
    }

    private void EmitBorder(CssBorderSide side, float x1, float y1, float x2, float y2)
    {
        if (!side.IsVisible) return;
        _result.Primitives.Add(new BorderLinePrimitive
        {
            PageIndex = _currentPage,
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Width = side.Width.Points,
            Color = side.Color,
            Style = side.Style,
        });
    }

    private void AdvancePage()
    {
        _currentPage += 1;
        _cursorY = 0f;
        _result.PageCount = _currentPage + 1;
        _result.Primitives.Add(new PageBreakPrimitive { PageIndex = _currentPage - 1 });
    }

    /// <summary>
    /// Checks if the current page is full and advances to the next page if necessary.
    /// </summary>
    private void CheckPageOverflow()
    {
        if (_cursorY > _pageContentH) AdvancePage();
    }
}