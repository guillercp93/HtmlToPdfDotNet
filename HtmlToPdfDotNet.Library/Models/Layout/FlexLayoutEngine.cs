using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Fonts;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Library.Models.Layout;

/// <summary>
/// Lays out child elements inside a <c>display: flex</c> container
/// following the main-axis and cross-axis model defined in CSS Flexbox.
/// </summary>
/// <remarks>
/// Supported properties:
/// <list type="bullet">
///   <item><c>flex-direction</c>: Row | Column</item>
///   <item><c>justify-content</c>: FlexStart | FlexEnd | Center | SpaceBetween | SpaceAround</item>
///   <item><c>align-items</c>: FlexStart | FlexEnd | Center | Stretch</item>
/// </list>
/// Nested flexbox, <c>flex-wrap</c>, <c>flex-grow</c>/<c>flex-shrink</c> are deferred.
/// </remarks>
public static class FlexLayoutEngine
{
    /// <summary>
    /// Holds the measured layout data for a single flex item.
    /// </summary>
    private struct FlexItemLayout
    {
        public HtmlNode Node;
        public ComputedStyle Style;
        /// <summary>Content-box width allocated to this item (after subtracting its own margin/border/padding).</summary>
        public float ContentWidth;
        /// <summary>Total height the item occupies (content + padding + border + margin).</summary>
        public float TotalHeight;
        /// <summary>The raw layout result produced by the child engine.</summary>
        public LayoutResult InnerResult;
    }

    /// <summary>
    /// Lays out the children of a flex container.
    /// </summary>
    /// <param name="container">The flex container node.</param>
    /// <param name="style">The computed style of the container.</param>
    /// <param name="styles">All computed styles.</param>
    /// <param name="x">The X position of the container's margin box.</param>
    /// <param name="availableWidth">The width available to the container (from its parent).</param>
    /// <param name="result">The layout result to append primitives to.</param>
    /// <param name="currentPage">The current page before this container.</param>
    /// <param name="cursorY">The Y cursor before processing this container.</param>
    /// <param name="pageContentH">The usable content height on each page.</param>
    /// <param name="newCursorY">Updated cursor Y after the container is laid out.</param>
    /// <param name="newPage">Updated page number.</param>
    /// <param name="registry">Optional font registry.</param>
    /// <param name="basePath">Optional base path for relative file resolution.</param>
    /// <returns>The total height consumed by this flex container in the flow.</returns>
    public static float Layout(
        HtmlNode container,
        ComputedStyle style,
        Dictionary<HtmlNode, ComputedStyle> styles,
        float x,
        float availableWidth,
        LayoutResult result,
        int currentPage,
        float cursorY,
        float pageContentH,
        out float newCursorY,
        out int newPage,
        FontRegistry? registry = null,
        string? basePath = null)
    {
        newCursorY = cursorY;
        newPage = currentPage;

        // Apply container top margin
        newCursorY += style.Margin.Top.Points;

        // Compute content area dimensions
        float borderBoxLeft = x + style.Margin.Left.Points;
        float contentX = borderBoxLeft
                         + style.BorderLeft.Width.Points
                         + style.Padding.Left.Points;
        float contentWidth = availableWidth
                           - style.Margin.Left.Points - style.Margin.Right.Points
                           - style.BorderLeft.Width.Points - style.BorderRight.Width.Points
                           - style.Padding.Left.Points - style.Padding.Right.Points;

        // Collect in-flow children (skip text nodes and display: none)
        List<FlexItemLayout> items = CollectFlexItems(container, styles);
        if (items.Count == 0)
        {
            newCursorY += style.Margin.Bottom.Points;
            return newCursorY - cursorY;
        }

        // --- Page-break-inside: avoid on the flex container itself ---
        if (style.PageBreakInside == PageBreakInside.Avoid)
        {
            float estimatedHeight = EstimateContainerHeight(style, items.Count);
            if (estimatedHeight < pageContentH && newCursorY + estimatedHeight > pageContentH)
            {
                AdvancePage(result, ref newPage, ref newCursorY);
            }
        }

        float startY = newCursorY;

        // Measure and layout each item
        MeasureFlexItems(items, styles, contentWidth, pageContentH, registry, basePath);

        // Position and emit based on flex-direction
        if (style.FlexDirection == FlexDirection.Row)
        {
            LayoutRow(items, contentX, contentWidth, result,
                      ref newPage, ref newCursorY, pageContentH, style);
        }
        else
        {
            LayoutColumn(items, contentX, contentWidth, result,
                         ref newPage, ref newCursorY, pageContentH, style);
        }

        // --- Emit container background rect ---
        if (style.BackgroundColor.A > 0f)
        {
            float containerHeight = newCursorY - startY;
            float borderBoxWidth = contentWidth
                                   + style.Padding.Left.Points + style.Padding.Right.Points
                                   + style.BorderLeft.Width.Points + style.BorderRight.Width.Points;

            result.Primitives.Add(new RectPrimitive
            {
                PageIndex = newPage,
                X = borderBoxLeft,
                Y = startY,
                Width = borderBoxWidth,
                Height = containerHeight + style.BorderTop.Width.Points + style.BorderBottom.Width.Points,
                Fill = style.BackgroundColor,
            });
        }

        // Apply container bottom margin
        newCursorY += style.Margin.Bottom.Points;

        // Check page overflow after the container
        if (newCursorY > pageContentH)
        {
            AdvancePage(result, ref newPage, ref newCursorY);
        }

        result.PageCount = Math.Max(result.PageCount, newPage + 1);
        return newCursorY - cursorY;
    }

    #region Item Collection

    /// <summary>
    /// Collects in-flow element children of the flex container.
    /// Text nodes and <c>display: none</c> elements are skipped.
    /// </summary>
    private static List<FlexItemLayout> CollectFlexItems(
        HtmlNode container, Dictionary<HtmlNode, ComputedStyle> styles)
    {
        List<FlexItemLayout> items = new();
        foreach (HtmlNode child in container.ChildNodes)
        {
            if (child.NodeType != HtmlNodeType.Element) continue;
            if (!styles.TryGetValue(child, out ComputedStyle? childStyle)) continue;
            if (childStyle.Display == DisplayType.None) continue;
            items.Add(new FlexItemLayout { Node = child, Style = childStyle });
        }
        return items;
    }

    #endregion

    #region Measurement

    /// <summary>
    /// Roughly estimates the container height before layout, for page-break-inside decisions.
    /// </summary>
    private static float EstimateContainerHeight(ComputedStyle style, int itemCount)
    {
        float paddingV = style.Padding.Top.Points + style.Padding.Bottom.Points;
        float borderV = style.BorderTop.Width.Points + style.BorderBottom.Width.Points;
        return style.FontSize * style.LineHeight * Math.Max(1, itemCount) + paddingV + borderV;
    }

    /// <summary>
    /// Measures each flex item by running a <see cref="BlockLayoutEngine"/> on it.
    /// </summary>
    private static void MeasureFlexItems(
        List<FlexItemLayout> items,
        Dictionary<HtmlNode, ComputedStyle> styles,
        float containerContentWidth,
        float pageContentH,
        FontRegistry? registry,
        string? basePath)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            float childMarginH = item.Style.Margin.Left.Points + item.Style.Margin.Right.Points;
            float childBorderH = item.Style.BorderLeft.Width.Points + item.Style.BorderRight.Width.Points;
            float childPaddingH = item.Style.Padding.Left.Points + item.Style.Padding.Right.Points;
            float childContentWidth = Math.Max(0f, containerContentWidth - childMarginH - childBorderH - childPaddingH);

            // Layout the child inline to measure its natural height
            PageLayout childPage = new(childContentWidth, pageContentH, new PageMargins(0f));
            BlockLayoutEngine childEngine = new(childPage, styles, registry, basePath);
            LayoutResult innerResult = childEngine.Layout(item.Node);

            // Total height this item occupies in the flow
            float itemHeight = innerResult.TotalHeight
                               + item.Style.Margin.Top.Points + item.Style.Margin.Bottom.Points
                               + item.Style.Padding.Top.Points + item.Style.Padding.Bottom.Points
                               + item.Style.BorderTop.Width.Points + item.Style.BorderBottom.Width.Points;

            // Store measurement — directly index (safe: no structural modification)
            items[i] = new FlexItemLayout
            {
                Node = item.Node,
                Style = item.Style,
                ContentWidth = Math.Max(0f, childContentWidth),
                TotalHeight = Math.Max(0f, itemHeight),
                InnerResult = innerResult,
            };
        }
    }

    #endregion

    #region Row Layout

    /// <summary>
    /// Lays out flex items in a row (main axis = horizontal).
    /// </summary>
    private static void LayoutRow(
        List<FlexItemLayout> items,
        float contentX,
        float contentWidth,
        LayoutResult result,
        ref int newPage,
        ref float newCursorY,
        float pageContentH,
        ComputedStyle containerStyle)
    {
        int count = items.Count;
        if (count == 0) return;

        // Allocate equal width per item in the row
        float totalAllocatedWidth = 0f;
        float[] allocatedWidths = new float[count];
        for (int i = 0; i < count; i++)
        {
            float marginH = items[i].Style.Margin.Left.Points + items[i].Style.Margin.Right.Points;
            float borderH = items[i].Style.BorderLeft.Width.Points + items[i].Style.BorderRight.Width.Points;
            float paddingH = items[i].Style.Padding.Left.Points + items[i].Style.Padding.Right.Points;
            float itemTotalH = marginH + borderH + paddingH;

            float share = Math.Max(0f, contentWidth / count - itemTotalH);
            allocatedWidths[i] = share;
            totalAllocatedWidth += share + itemTotalH;
        }

        // Compute remaining space for justify-content
        float remainingSpace = Math.Max(0f, contentWidth - totalAllocatedWidth);

        // Calculate X offsets based on justify-content
        float[] xPositions = CalculateJustifyPositions(allocatedWidths, items, remainingSpace, containerStyle.JustifyContent);

        // Determine row height (tallest item)
        float rowHeight = items.Max(i => i.TotalHeight);

        // Page-break-inside: avoid — check if the whole row fits
        if (rowHeight < pageContentH && newCursorY + rowHeight > pageContentH)
        {
            AdvancePage(result, ref newPage, ref newCursorY);
        }

        // Emit shifted primitives
        for (int i = 0; i < count; i++)
        {
            var item = items[i];

            // Determine Y offset based on align-items
            float yOffset = containerStyle.AlignItems switch
            {
                AlignItems.FlexEnd => rowHeight - item.TotalHeight,
                AlignItems.Center => (rowHeight - item.TotalHeight) / 2f,
                _ => 0f, // FlexStart, Stretch
            };

            // Page-break-inside: avoid on individual flex item
            if (item.Style.PageBreakInside == PageBreakInside.Avoid)
            {
                if (item.TotalHeight < pageContentH && newCursorY + item.TotalHeight > pageContentH)
                {
                    AdvancePage(result, ref newPage, ref newCursorY);
                }
            }

            float itemX = contentX + xPositions[i]
                          + item.Style.Margin.Left.Points;
            float itemY = newCursorY + yOffset
                          + item.Style.Margin.Top.Points;

            // Append flex item's primitives with shifted positions
            CopyPrimitives(item.InnerResult, result, newPage, itemX, itemY);
        }

        newCursorY += rowHeight;
    }

    #endregion

    #region Column Layout

    /// <summary>
    /// Lays out flex items in a column (main axis = vertical).
    /// </summary>
    private static void LayoutColumn(
        List<FlexItemLayout> items,
        float contentX,
        float contentWidth,
        LayoutResult result,
        ref int newPage,
        ref float newCursorY,
        float pageContentH,
        ComputedStyle containerStyle)
    {
        int count = items.Count;
        if (count == 0) return;

        // Measure total used height and per-item heights
        float totalUsedHeight = items.Sum(i => i.TotalHeight);

        // Remaining vertical space for justify-content
        float remainingSpace = 0f;
        float containerHeight = containerStyle.Height.IsAuto ? totalUsedHeight : containerStyle.Height.Points;
        if (!containerStyle.Height.IsAuto && containerStyle.Height.Points > totalUsedHeight)
        {
            remainingSpace = containerStyle.Height.Points - totalUsedHeight;
        }

        // Calculate Y offsets based on justify-content
        float[] yPositions = CalculateColumnJustifyPositions(items, remainingSpace, containerStyle.JustifyContent);

        // Emit shifted primitives
        for (int i = 0; i < count; i++)
        {
            var item = items[i];

            // Determine X offset based on align-items
            float itemMarginH = item.Style.Margin.Left.Points + item.Style.Margin.Right.Points;
            float itemContentBoxWidth = contentWidth - itemMarginH;

            float crossAxisRemaining = containerStyle.AlignItems switch
            {
                AlignItems.FlexEnd => contentWidth - itemContentBoxWidth - item.Style.Margin.Left.Points - item.Style.Margin.Right.Points,
                AlignItems.Center => (contentWidth - itemContentBoxWidth - item.Style.Margin.Left.Points - item.Style.Margin.Right.Points) / 2f,
                AlignItems.Stretch => 0f, // stretch to full width
                _ => 0f, // FlexStart
            };

            float itemX = contentX + crossAxisRemaining
                          + item.Style.Margin.Left.Points;
            float itemY = newCursorY + yPositions[i]
                          + item.Style.Margin.Top.Points;

            // Page-break-inside: avoid on individual flex item
            if (item.Style.PageBreakInside == PageBreakInside.Avoid)
            {
                if (item.TotalHeight < pageContentH && itemY + item.TotalHeight > pageContentH)
                {
                    // Move this item to the next page
                    AdvancePage(result, ref newPage, ref newCursorY);
                    itemY = newCursorY + yPositions[i] + item.Style.Margin.Top.Points;
                }
            }

            // Check page overflow for this item
            if (itemY + item.TotalHeight > pageContentH)
            {
                AdvancePage(result, ref newPage, ref newCursorY);
                itemY = newCursorY + yPositions[i] + item.Style.Margin.Top.Points;
            }

            CopyPrimitives(item.InnerResult, result, newPage, itemX, itemY);

            newCursorY = Math.Max(newCursorY, itemY + item.TotalHeight - yPositions[i] - item.Style.Margin.Top.Points);
        }
    }

    #endregion

    #region Justify-Content Calculations

    /// <summary>
    /// Calculates X offsets for each flex item in a row based on justify-content.
    /// </summary>
    private static float[] CalculateJustifyPositions(
        float[] allocatedWidths,
        List<FlexItemLayout> items,
        float remainingSpace,
        JustifyContent justify)
    {
        int count = allocatedWidths.Length;
        float[] positions = new float[count];
        if (count == 0) return positions;

        // Calculate the total width each item occupies (allocated + margin + border + padding)
        float[] totalItemWidths = new float[count];
        for (int i = 0; i < count; i++)
        {
            float marginH = items[i].Style.Margin.Left.Points + items[i].Style.Margin.Right.Points;
            float borderH = items[i].Style.BorderLeft.Width.Points + items[i].Style.BorderRight.Width.Points;
            float paddingH = items[i].Style.Padding.Left.Points + items[i].Style.Padding.Right.Points;
            totalItemWidths[i] = allocatedWidths[i] + marginH + borderH + paddingH;
        }

        switch (justify)
        {
            case JustifyContent.FlexEnd:
            {
                float currentX = remainingSpace;
                for (int i = 0; i < count; i++)
                {
                    positions[i] = currentX;
                    currentX += totalItemWidths[i];
                }
                break;
            }
            case JustifyContent.Center:
            {
                float currentX = remainingSpace / 2f;
                for (int i = 0; i < count; i++)
                {
                    positions[i] = currentX;
                    currentX += totalItemWidths[i];
                }
                break;
            }
            case JustifyContent.SpaceBetween when count > 1:
            {
                float gap = remainingSpace / (count - 1);
                float currentX = 0f;
                for (int i = 0; i < count; i++)
                {
                    positions[i] = currentX;
                    currentX += totalItemWidths[i] + gap;
                }
                break;
            }
            case JustifyContent.SpaceAround:
            {
                float gap = remainingSpace / count;
                float halfGap = gap / 2f;
                float currentX = halfGap;
                for (int i = 0; i < count; i++)
                {
                    positions[i] = currentX;
                    currentX += totalItemWidths[i] + gap;
                }
                break;
            }
            default: // FlexStart
            {
                float currentX = 0f;
                for (int i = 0; i < count; i++)
                {
                    positions[i] = currentX;
                    currentX += totalItemWidths[i];
                }
                break;
            }
        }

        return positions;
    }

    /// <summary>
    /// Calculates Y offsets for each flex item in a column based on justify-content.
    /// </summary>
    private static float[] CalculateColumnJustifyPositions(
        List<FlexItemLayout> items,
        float remainingSpace,
        JustifyContent justify)
    {
        int count = items.Count;
        float[] positions = new float[count];
        if (count == 0) return positions;

        switch (justify)
        {
            case JustifyContent.FlexEnd:
            {
                float currentY = remainingSpace;
                for (int i = 0; i < count; i++)
                {
                    positions[i] = currentY;
                    currentY += items[i].TotalHeight;
                }
                break;
            }
            case JustifyContent.Center:
            {
                float currentY = remainingSpace / 2f;
                for (int i = 0; i < count; i++)
                {
                    positions[i] = currentY;
                    currentY += items[i].TotalHeight;
                }
                break;
            }
            case JustifyContent.SpaceBetween when count > 1:
            {
                float gap = remainingSpace / (count - 1);
                float currentY = 0f;
                for (int i = 0; i < count; i++)
                {
                    positions[i] = currentY;
                    currentY += items[i].TotalHeight + gap;
                }
                break;
            }
            case JustifyContent.SpaceAround:
            {
                float gap = remainingSpace / count;
                float halfGap = gap / 2f;
                float currentY = halfGap;
                for (int i = 0; i < count; i++)
                {
                    positions[i] = currentY;
                    currentY += items[i].TotalHeight + gap;
                }
                break;
            }
            default: // FlexStart
            {
                float currentY = 0f;
                for (int i = 0; i < count; i++)
                {
                    positions[i] = currentY;
                    currentY += items[i].TotalHeight;
                }
                break;
            }
        }

        return positions;
    }

    #endregion

    #region Primitive Copying

    /// <summary>
    /// Copies primitives from a child layout result into the parent result,
    /// shifting their X/Y coordinates and updating the page index.
    /// </summary>
    private static void CopyPrimitives(
        LayoutResult innerResult,
        LayoutResult parentResult,
        int page,
        float offsetX,
        float offsetY)
    {
        foreach (RenderPrimitive prim in innerResult.Primitives)
        {
            switch (prim)
            {
                case TextPrimitive tp:
                    parentResult.Primitives.Add(new TextPrimitive
                    {
                        PageIndex = page,
                        X = offsetX + tp.X,
                        Y = offsetY + tp.Y,
                        Text = tp.Text,
                        FontName = tp.FontName,
                        FontSize = tp.FontSize,
                        Bold = tp.Bold,
                        Italic = tp.Italic,
                        Color = tp.Color,
                        EmbeddedFont = tp.EmbeddedFont,
                    });
                    break;

                case RectPrimitive rp:
                    parentResult.Primitives.Add(new RectPrimitive
                    {
                        PageIndex = page,
                        X = offsetX + rp.X,
                        Y = offsetY + rp.Y,
                        Width = rp.Width,
                        Height = rp.Height,
                        Fill = rp.Fill,
                        BorderRadius = rp.BorderRadius,
                        Stroke = rp.Stroke,
                        StrokeWidth = rp.StrokeWidth,
                    });
                    break;

                case BorderLinePrimitive blp:
                    parentResult.Primitives.Add(new BorderLinePrimitive
                    {
                        PageIndex = page,
                        X1 = offsetX + blp.X1,
                        Y1 = offsetY + blp.Y1,
                        X2 = offsetX + blp.X2,
                        Y2 = offsetY + blp.Y2,
                        Width = blp.Width,
                        Color = blp.Color,
                        Style = blp.Style,
                    });
                    break;

                case ImagePrimitive ip:
                    parentResult.Primitives.Add(new ImagePrimitive
                    {
                        PageIndex = page,
                        X = offsetX + ip.X,
                        Y = offsetY + ip.Y,
                        Width = ip.Width,
                        Height = ip.Height,
                        ImageData = ip.ImageData,
                        XObjectAlias = ip.XObjectAlias,
                    });
                    break;
            }
        }
    }

    #endregion

    #region Page Helpers

    /// <summary>
    /// Advances to the next page, resetting cursor Y and emitting a page break primitive.
    /// </summary>
    private static void AdvancePage(LayoutResult result, ref int page, ref float cursorY)
    {
        page++;
        cursorY = 0f;
        result.PageCount = Math.Max(result.PageCount, page + 1);
        result.Primitives.Add(new PageBreakPrimitive { PageIndex = page - 1 });
    }

    #endregion
}
