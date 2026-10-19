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
        /// <summary>The flex-basis used for grow/shrink calculations (content-box).</summary>
        public float BasisWidth;
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

        // Respect the container's explicit width (cap by content area)
        // Note: BoxModel does not include padding/border in Width, so subtract them.
        float containerBoxExtrasH = style.Padding.Left.Points + style.Padding.Right.Points
                                  + style.BorderLeft.Width.Points + style.BorderRight.Width.Points;
        if (!style.Width.IsAuto && style.Width.Points > 0f)
            contentWidth = Math.Min(contentWidth, style.Width.Points - containerBoxExtrasH);

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
        MeasureFlexItems(items, styles, contentWidth, pageContentH, registry, basePath, style.FlexDirection, style.Gap.Points);

        // Position and emit based on flex-direction
        if (style.FlexDirection == FlexDirection.Row)
        {
            LayoutRow(items, contentX, contentWidth, result,
                      ref newPage, ref newCursorY, pageContentH, style,
                      styles, registry, basePath);
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

    /// <summary>Width of the virtual page used to measure the natural width of auto-basis items.</summary>
    private const float MeasureWidth = 10000f;

    /// <summary>
    /// Returns <c>true</c> when a primitive's right edge lies in the far half of the
    /// measurement width, i.e. it belongs to an auto-width box stretched to the measurement
    /// page. Using half the width (rather than a tolerance near the edge) keeps the check
    /// independent of ancestor padding, borders, and margins that inset the stretched box.
    /// </summary>
    private static bool SpansMeasureWidth(float rightEdge) => rightEdge > MeasureWidth / 2f;

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
    /// Uses flex-basis to determine the starting width for each item.
    /// </summary>
    private static void MeasureFlexItems(
        List<FlexItemLayout> items,
        Dictionary<HtmlNode, ComputedStyle> styles,
        float containerContentWidth,
        float pageContentH,
        FontRegistry? registry,
        string? basePath,
        FlexDirection flexDirection,
        float gap = 0f)
    {
        int count = items.Count;
        float totalGapSpace = gap * Math.Max(0, count - 1);

        for (int i = 0; i < count; i++)
        {
            var item = items[i];
            float childMarginH = item.Style.Margin.Left.Points + item.Style.Margin.Right.Points;
            float childBorderH = item.Style.BorderLeft.Width.Points + item.Style.BorderRight.Width.Points;
            float childPaddingH = item.Style.Padding.Left.Points + item.Style.Padding.Right.Points;
            float itemBoxH = childMarginH + childBorderH + childPaddingH;

            // Determine flex-basis for this item (content-box width before grow/shrink)
            float basisContentWidth;
            if (flexDirection == FlexDirection.Row)
            {
                if (!item.Style.FlexBasis.IsAuto)
                {
                    // Flex-basis is an explicit length → use it as the starting content width
                    basisContentWidth = Math.Max(0f, item.Style.FlexBasis.Points);
                }
                else if (!item.Style.Width.IsAuto && item.Style.Width.Points > 0f)
                {
                    // Flex-basis: auto, explicit width → use the width property
                    basisContentWidth = Math.Max(0f, item.Style.Width.Points);
                }
                else
                {
                    // Flex-basis: auto, no explicit width → measure natural content width
                    // by doing a test layout at a very large width, then reading the
                    // rightmost extent of text, images, and sized boxes.
                    PageLayout measurePage = new(MeasureWidth, pageContentH, new PageMargins(0f));
                    BlockLayoutEngine measureEngine = new(measurePage, styles, registry, basePath);
                    LayoutResult measureResult = measureEngine.Layout(item.Node);

                    // Find rightmost content extent from text and image primitives
                    // measured from the test layout's origin. Inline text primitives
                    // do NOT include the flex item's own margin/border/padding offset
                    // (the inner layout processes children, not the item itself), so
                    // no offset subtraction is needed here.
                    float maxExtent = 0f;
                    foreach (RenderPrimitive prim in measureResult.Primitives)
                    {
                        switch (prim)
                        {
                            case TextPrimitive tp:
                            {
                                var run = new InlineRun
                                {
                                    Text = tp.Text,
                                    FontName = tp.FontName,
                                    FontSize = tp.FontSize,
                                    Bold = tp.Bold,
                                    Italic = tp.Italic,
                                    EmbeddedFont = tp.EmbeddedFont,
                                };
                                float textWidth = Helpers.MeasureText(tp.Text, run);
                                maxExtent = Math.Max(maxExtent, tp.X + textWidth);
                                break;
                            }
                            case ImagePrimitive ip:
                                maxExtent = Math.Max(maxExtent, ip.X + ip.Width);
                                break;
                            // Boxes with an explicit width contribute their extent; auto-width
                            // boxes stretch to the measurement width and are ignored.
                            case RectPrimitive rp when !SpansMeasureWidth(rp.X + rp.Width):
                                maxExtent = Math.Max(maxExtent, rp.X + rp.Width);
                                break;
                            case BorderLinePrimitive blp when !SpansMeasureWidth(Math.Max(blp.X1, blp.X2)):
                                maxExtent = Math.Max(maxExtent, Math.Max(blp.X1, blp.X2));
                                break;
                        }
                    }

                    basisContentWidth = Math.Max(0f, maxExtent);
                }
            }
            else
            {
                // Column layout: use full container width (no equal-division)
                basisContentWidth = Math.Max(0f, containerContentWidth - itemBoxH);
            }

            // Layout the child at the TOTAL width (content + padding + border + margin)
            // so that BlockLayoutEngine's ResolveWidth yields the correct content-box size.
            float totalItemWidth = basisContentWidth + itemBoxH;
            PageLayout childPage = new(totalItemWidth, pageContentH, new PageMargins(0f));
            BlockLayoutEngine childEngine = new(childPage, styles, registry, basePath);
            LayoutResult innerResult = childEngine.Layout(item.Node);

            // Total height this item occupies in the flow.
            // innerResult.TotalHeight covers only the laid-out children
            // (inline content → lineHeight; block children → their own TotalHeight).
            // The flex item's own padding, border, and margin must be added here
            // because the inner engine does NOT apply them to the root node.
            float contentH = innerResult.TotalHeight;
            if (!item.Style.Height.IsAuto && item.Style.Height.Points > contentH)
                contentH = item.Style.Height.Points;

            float itemHeight = contentH
                               + item.Style.Margin.Top.Points + item.Style.Margin.Bottom.Points
                               + item.Style.Padding.Top.Points + item.Style.Padding.Bottom.Points
                               + item.Style.BorderTop.Width.Points + item.Style.BorderBottom.Width.Points;

            // Store measurement — directly index (safe: no structural modification)
            items[i] = new FlexItemLayout
            {
                Node = item.Node,
                Style = item.Style,
                ContentWidth = basisContentWidth,
                BasisWidth = basisContentWidth,
                TotalHeight = Math.Max(0f, itemHeight),
                InnerResult = innerResult,
            };
        }
    }

    #endregion

    #region Row Layout

    /// <summary>
    /// Lays out flex items in a row (main axis = horizontal).
    /// Delegates to <see cref="LayoutRowSegment"/> for single-line layout,
    /// or <see cref="LayoutRowWrap"/> for multi-line (flex-wrap) layout.
    /// </summary>
    private static void LayoutRow(
        List<FlexItemLayout> items,
        float contentX,
        float contentWidth,
        LayoutResult result,
        ref int newPage,
        ref float newCursorY,
        float pageContentH,
        ComputedStyle containerStyle,
        Dictionary<HtmlNode, ComputedStyle> styles,
        FontRegistry? registry,
        string? basePath)
    {
        if (containerStyle.FlexWrap == FlexWrap.Nowrap)
        {
            LayoutRowSegment(items, contentX, contentWidth, result,
                ref newPage, ref newCursorY, pageContentH, containerStyle,
                styles, registry, basePath);
        }
        else
        {
            LayoutRowWrap(items, contentX, contentWidth, result,
                ref newPage, ref newCursorY, pageContentH, containerStyle,
                styles, registry, basePath);
        }
    }

    /// <summary>
    /// Lays out a single row (segment) of flex items with grow/shrink distribution.
    /// Used by both single-line and multi-line layouts. The line starts at
    /// <paramref name="newCursorY"/>, which is advanced past the line on return.
    /// </summary>
    private static void LayoutRowSegment(
        List<FlexItemLayout> items,
        float contentX,
        float contentWidth,
        LayoutResult result,
        ref int newPage,
        ref float newCursorY,
        float pageContentH,
        ComputedStyle containerStyle,
        Dictionary<HtmlNode, ComputedStyle> styles,
        FontRegistry? registry,
        string? basePath)
    {
        int count = items.Count;
        if (count == 0) return;

        float gap = containerStyle.Gap.Points;
        float totalGapSpace = gap * (count - 1);

        // ── Step 1: Compute total flex-basis (including box-model extras) ──
        float[] itemBoxExtras = new float[count];
        float totalBasis = 0f;

        for (int i = 0; i < count; i++)
        {
            float marginH = items[i].Style.Margin.Left.Points + items[i].Style.Margin.Right.Points;
            float borderH = items[i].Style.BorderLeft.Width.Points + items[i].Style.BorderRight.Width.Points;
            float paddingH = items[i].Style.Padding.Left.Points + items[i].Style.Padding.Right.Points;
            itemBoxExtras[i] = marginH + borderH + paddingH;
            totalBasis += items[i].BasisWidth + itemBoxExtras[i];
        }

        float totalDesired = totalBasis + totalGapSpace;

        // ── Step 2: Apply flex-grow or flex-shrink ──
        float[] allocatedWidths = new float[count];

        if (totalDesired <= contentWidth && totalDesired >= 0f)
        {
            float remaining = contentWidth - totalDesired;
            float totalGrow = 0f;
            for (int i = 0; i < count; i++)
                totalGrow += items[i].Style.FlexGrow;

            if (totalGrow > 0f)
            {
                for (int i = 0; i < count; i++)
                {
                    float growShare = remaining * items[i].Style.FlexGrow / totalGrow;
                    allocatedWidths[i] = items[i].BasisWidth + growShare;
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                    allocatedWidths[i] = items[i].BasisWidth;
            }
        }
        else
        {
            float overflow = totalDesired - contentWidth;
            float weightedSum = 0f;
            for (int i = 0; i < count; i++)
            {
                if (items[i].Style.FlexShrink > 0f && items[i].BasisWidth > 0f)
                    weightedSum += items[i].Style.FlexShrink * items[i].BasisWidth;
            }

            if (weightedSum > 0f)
            {
                for (int i = 0; i < count; i++)
                {
                    if (items[i].Style.FlexShrink > 0f && items[i].BasisWidth > 0f)
                    {
                        float reduction = overflow * (items[i].Style.FlexShrink * items[i].BasisWidth) / weightedSum;
                        allocatedWidths[i] = Math.Max(0f, items[i].BasisWidth - reduction);
                    }
                    else
                    {
                        allocatedWidths[i] = items[i].BasisWidth;
                    }
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                    allocatedWidths[i] = items[i].BasisWidth;
            }
        }

        // ── Step 2b: Re-layout items whose allocated width differs from their
        // measured content width, so primitives are generated at the correct size.
        for (int i = 0; i < count; i++)
        {
            float allocated = allocatedWidths[i];
            if (Math.Abs(allocated - items[i].ContentWidth) > 0.01f)
            {
                float marginH = items[i].Style.Margin.Left.Points + items[i].Style.Margin.Right.Points;
                float borderH = items[i].Style.BorderLeft.Width.Points + items[i].Style.BorderRight.Width.Points;
                float paddingH = items[i].Style.Padding.Left.Points + items[i].Style.Padding.Right.Points;
                float itemBoxH = marginH + borderH + paddingH;

                // Total width for the re-layout page (content + padding + border + margin)
                float totalItemWidth = allocated + itemBoxH;
                PageLayout relayoutPage = new(Math.Max(1f, totalItemWidth), pageContentH, new PageMargins(0f));
                BlockLayoutEngine relayoutEngine = new(relayoutPage, styles, registry, basePath);
                LayoutResult relayoutResult = relayoutEngine.Layout(items[i].Node);

                float relayoutContentH = relayoutResult.TotalHeight;
                if (!items[i].Style.Height.IsAuto && items[i].Style.Height.Points > relayoutContentH)
                    relayoutContentH = items[i].Style.Height.Points;

                float newHeight = relayoutContentH
                                  + items[i].Style.Margin.Top.Points + items[i].Style.Margin.Bottom.Points
                                  + items[i].Style.Padding.Top.Points + items[i].Style.Padding.Bottom.Points
                                  + items[i].Style.BorderTop.Width.Points + items[i].Style.BorderBottom.Width.Points;

                items[i] = new FlexItemLayout
                {
                    Node = items[i].Node,
                    Style = items[i].Style,
                    ContentWidth = allocated,
                    BasisWidth = items[i].BasisWidth,
                    TotalHeight = Math.Max(0f, newHeight),
                    InnerResult = relayoutResult,
                };
            }
        }

        // ── Step 3: Compute remaining space for justify-content ──
        float totalAllocated = 0f;
        for (int i = 0; i < count; i++)
            totalAllocated += allocatedWidths[i] + itemBoxExtras[i];

        float remainingSpace = Math.Max(0f, contentWidth - totalAllocated - totalGapSpace);

        float[] xPositions = CalculateJustifyPositions(allocatedWidths, items, remainingSpace, containerStyle.JustifyContent, gap);

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

            float yOffset = containerStyle.AlignItems switch
            {
                AlignItems.FlexEnd => rowHeight - item.TotalHeight,
                AlignItems.Center => (rowHeight - item.TotalHeight) / 2f,
                _ => 0f,
            };

            if (item.Style.PageBreakInside == PageBreakInside.Avoid)
            {
                if (item.TotalHeight < pageContentH && newCursorY + item.TotalHeight > pageContentH)
                {
                    AdvancePage(result, ref newPage, ref newCursorY);
                }
            }

            float itemMarginLeft = item.Style.Margin.Left.Points;
            float itemMarginTop = item.Style.Margin.Top.Points;
            float itemX = contentX + xPositions[i] + itemMarginLeft;
            float itemY = newCursorY + yOffset + itemMarginTop;

            float bbW = allocatedWidths[i]
                        + item.Style.BorderLeft.Width.Points + item.Style.BorderRight.Width.Points
                        + item.Style.Padding.Left.Points + item.Style.Padding.Right.Points;
            float bbH = item.TotalHeight - itemMarginTop - item.Style.Margin.Bottom.Points;

            float borderRadius = item.Style.BorderRadius.Points;
            if (borderRadius > 0f)
            {
                CssBorderSide activeBorder = item.Style.BorderTop.IsVisible ? item.Style.BorderTop :
                                              item.Style.BorderBottom.IsVisible ? item.Style.BorderBottom :
                                              item.Style.BorderLeft.IsVisible ? item.Style.BorderLeft :
                                              item.Style.BorderRight;

                float strokeWidth = activeBorder.IsVisible ? activeBorder.Width.Points : 0f;
                CssColor strokeColor = activeBorder.IsVisible ? activeBorder.Color : CssColor.Transparent;

                result.Primitives.Add(new RectPrimitive
                {
                    PageIndex = newPage,
                    X = itemX,
                    Y = itemY,
                    Width = bbW,
                    Height = bbH,
                    Fill = item.Style.BackgroundColor,
                    BorderRadius = borderRadius,
                    Stroke = strokeColor,
                    StrokeWidth = strokeWidth,
                });
            }
            else
            {
                if (item.Style.BackgroundColor.A > 0f)
                {
                    result.Primitives.Add(new RectPrimitive
                    {
                        PageIndex = newPage,
                        X = itemX,
                        Y = itemY,
                        Width = bbW,
                        Height = bbH,
                        Fill = item.Style.BackgroundColor,
                    });
                }

                EmitFlexBorder(result, newPage,
                    itemX, itemY, itemX + bbW, itemY, item.Style.BorderTop);
                EmitFlexBorder(result, newPage,
                    itemX, itemY + bbH, itemX + bbW, itemY + bbH, item.Style.BorderBottom);
                EmitFlexBorder(result, newPage,
                    itemX, itemY, itemX, itemY + bbH, item.Style.BorderLeft);
                EmitFlexBorder(result, newPage,
                    itemX + bbW, itemY, itemX + bbW, itemY + bbH, item.Style.BorderRight);
            }

            CopyPrimitives(item.InnerResult, result, newPage,
                itemX + item.Style.Padding.Left.Points + item.Style.BorderLeft.Width.Points,
                itemY + item.Style.Padding.Top.Points + item.Style.BorderTop.Width.Points);
        }

        newCursorY += rowHeight;
    }

    /// <summary>
    /// Lays out flex items across multiple lines when <c>flex-wrap: wrap</c> or <c>wrap-reverse</c>.
    /// Each line runs its own grow/shrink distribution via <see cref="LayoutRowSegment"/>.
    /// </summary>
    private static void LayoutRowWrap(
        List<FlexItemLayout> items,
        float contentX,
        float contentWidth,
        LayoutResult result,
        ref int newPage,
        ref float newCursorY,
        float pageContentH,
        ComputedStyle containerStyle,
        Dictionary<HtmlNode, ComputedStyle> styles,
        FontRegistry? registry,
        string? basePath)
    {
        int count = items.Count;
        if (count == 0) return;

        float gap = containerStyle.Gap.Points;

        // ── Step 1: Distribute items into lines ──
        List<List<FlexItemLayout>> lines = new();
        List<float> lineHeights = new();
        List<FlexItemLayout> currentLine = new();
        float currentLineWidth = 0f;

        for (int i = 0; i < count; i++)
        {
            var item = items[i];
            float itemBoxH = item.Style.Margin.Left.Points + item.Style.Margin.Right.Points
                           + item.Style.BorderLeft.Width.Points + item.Style.BorderRight.Width.Points
                           + item.Style.Padding.Left.Points + item.Style.Padding.Right.Points;
            float itemTotalWidth = item.BasisWidth + itemBoxH;
            float gapWidth = currentLine.Count > 0 ? gap : 0f;

            // Start new line if current item doesn't fit (only for non-empty lines)
            if (currentLine.Count > 0 && currentLineWidth + gapWidth + itemTotalWidth > contentWidth)
            {
                lines.Add(currentLine);
                lineHeights.Add(currentLine.Max(i2 => i2.TotalHeight));
                currentLine = new List<FlexItemLayout>();
                currentLineWidth = 0f;
            }

            currentLine.Add(item);
            currentLineWidth += (currentLine.Count > 1 ? gap : 0f) + itemTotalWidth;
        }

        if (currentLine.Count > 0)
        {
            lines.Add(currentLine);
            lineHeights.Add(currentLine.Max(i => i.TotalHeight));
        }

        if (lines.Count == 0) return;

        // Handle WrapReverse
        if (containerStyle.FlexWrap == FlexWrap.WrapReverse)
        {
            lines.Reverse();
            lineHeights.Reverse();
        }

        // ── Step 2: Compute total content height of all lines ──
        float totalLinesHeight = 0f;
        for (int l = 0; l < lines.Count; l++)
            totalLinesHeight += lineHeights[l] + (l < lines.Count - 1 ? gap : 0f);

        // ── Step 3: Compute Y offsets per line using align-content ──
        // The cross size is the container's definite (content-box) height when set;
        // otherwise it equals the lines' total height and there is no free space.
        // Free space is clamped at 0 so lines never start above the container.
        float crossSize = totalLinesHeight;
        if (!containerStyle.Height.IsAuto && containerStyle.Height.Points > totalLinesHeight)
            crossSize = containerStyle.Height.Points;

        float freeSpace = crossSize - totalLinesHeight;
        float[] lineYOffsets = new float[lines.Count];

        switch (containerStyle.AlignContent)
        {
            case AlignContent.FlexEnd:
            {
                float currentY = freeSpace;
                for (int l = 0; l < lines.Count; l++)
                {
                    lineYOffsets[l] = currentY;
                    currentY += lineHeights[l] + gap;
                }
                break;
            }
            case AlignContent.Center:
            {
                float currentY = freeSpace / 2f;
                for (int l = 0; l < lines.Count; l++)
                {
                    lineYOffsets[l] = currentY;
                    currentY += lineHeights[l] + gap;
                }
                break;
            }
            case AlignContent.SpaceBetween when lines.Count > 1:
            {
                float spacing = freeSpace / (lines.Count - 1);
                float currentY = 0f;
                for (int l = 0; l < lines.Count; l++)
                {
                    lineYOffsets[l] = currentY;
                    currentY += lineHeights[l] + gap + spacing;
                }
                break;
            }
            case AlignContent.SpaceAround when lines.Count > 1:
            {
                float spacing = freeSpace / lines.Count;
                float currentY = spacing / 2f;
                for (int l = 0; l < lines.Count; l++)
                {
                    lineYOffsets[l] = currentY;
                    currentY += lineHeights[l] + gap + spacing;
                }
                break;
            }
            default: // FlexStart, Stretch
            {
                float currentY = 0f;
                for (int l = 0; l < lines.Count; l++)
                {
                    lineYOffsets[l] = currentY;
                    currentY += lineHeights[l] + gap;
                }
                break;
            }
        }

        // ── Step 4: Layout each line ──
        // Advance the cursor to each line's start before laying it out, so the
        // segment's page-break check sees the real line position (gap included).
        float previousLineEnd = 0f;
        for (int l = 0; l < lines.Count; l++)
        {
            newCursorY += lineYOffsets[l] - previousLineEnd;
            LayoutRowSegment(lines[l], contentX, contentWidth, result,
                ref newPage, ref newCursorY, pageContentH, containerStyle,
                styles, registry, basePath);
            previousLineEnd = lineYOffsets[l] + lineHeights[l];
        }

        // Advance past any remaining cross space after the last line.
        newCursorY += crossSize - previousLineEnd;
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
        float colGap = containerStyle.Gap.Points;
        float totalColGap = colGap * Math.Max(0, count - 1);
        float remainingSpace = 0f;
        float containerHeight = containerStyle.Height.IsAuto ? totalUsedHeight + totalColGap : containerStyle.Height.Points;
        if (!containerStyle.Height.IsAuto && containerStyle.Height.Points > totalUsedHeight + totalColGap)
        {
            remainingSpace = containerStyle.Height.Points - totalUsedHeight - totalColGap;
        }

        // Calculate Y offsets based on justify-content (with gap)
        float[] yPositions = CalculateColumnJustifyPositions(items, remainingSpace, containerStyle.JustifyContent, colGap);

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

            // ── Emit flex item's own background & border box ─────────────
            float colBbW = contentWidth - crossAxisRemaining - item.Style.Margin.Right.Points;
            float colBbH = item.TotalHeight - item.Style.Margin.Top.Points - item.Style.Margin.Bottom.Points;
            float colBorderRadius = item.Style.BorderRadius.Points;

            if (colBorderRadius > 0f)
            {
                CssBorderSide activeBorder = item.Style.BorderTop.IsVisible ? item.Style.BorderTop :
                                              item.Style.BorderBottom.IsVisible ? item.Style.BorderBottom :
                                              item.Style.BorderLeft.IsVisible ? item.Style.BorderLeft :
                                              item.Style.BorderRight;

                float strokeWidth = activeBorder.IsVisible ? activeBorder.Width.Points : 0f;
                CssColor strokeColor = activeBorder.IsVisible ? activeBorder.Color : CssColor.Transparent;

                result.Primitives.Add(new RectPrimitive
                {
                    PageIndex = newPage,
                    X = itemX,
                    Y = itemY,
                    Width = colBbW,
                    Height = colBbH,
                    Fill = item.Style.BackgroundColor,
                    BorderRadius = colBorderRadius,
                    Stroke = strokeColor,
                    StrokeWidth = strokeWidth,
                });
            }
            else
            {
                if (item.Style.BackgroundColor.A > 0f)
                {
                    result.Primitives.Add(new RectPrimitive
                    {
                        PageIndex = newPage,
                        X = itemX,
                        Y = itemY,
                        Width = colBbW,
                        Height = colBbH,
                        Fill = item.Style.BackgroundColor,
                    });
                }

                EmitFlexBorder(result, newPage,
                    itemX, itemY, itemX + colBbW, itemY, item.Style.BorderTop);
                EmitFlexBorder(result, newPage,
                    itemX, itemY + colBbH, itemX + colBbW, itemY + colBbH, item.Style.BorderBottom);
                EmitFlexBorder(result, newPage,
                    itemX, itemY, itemX, itemY + colBbH, item.Style.BorderLeft);
                EmitFlexBorder(result, newPage,
                    itemX + colBbW, itemY, itemX + colBbW, itemY + colBbH, item.Style.BorderRight);
            }

            CopyPrimitives(item.InnerResult, result, newPage,
                itemX + item.Style.Padding.Left.Points + item.Style.BorderLeft.Width.Points,
                itemY + item.Style.Padding.Top.Points + item.Style.BorderTop.Width.Points);

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
        JustifyContent justify,
        float gap = 0f)
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
                    if (i < count - 1) currentX += gap;
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
                    if (i < count - 1) currentX += gap;
                }
                break;
            }
            case JustifyContent.SpaceBetween when count > 1:
            {
                float flexGap = (remainingSpace - gap * (count - 1)) / (count - 1);
                float currentX = 0f;
                for (int i = 0; i < count; i++)
                {
                    positions[i] = currentX;
                    currentX += totalItemWidths[i] + flexGap + gap;
                }
                break;
            }
            case JustifyContent.SpaceAround:
            {
                float flexGap = (remainingSpace - gap * (count - 1)) / count;
                float halfGap = flexGap / 2f;
                float currentX = halfGap;
                for (int i = 0; i < count; i++)
                {
                    positions[i] = currentX;
                    currentX += totalItemWidths[i] + flexGap + gap;
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
                    if (i < count - 1) currentX += gap;
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
        JustifyContent justify,
        float gap = 0f)
    {
        int count = items.Count;
        float[] positions = new float[count];
        if (count == 0) return positions;

        switch (justify)
        {
            case JustifyContent.FlexEnd:
            {
                for (int i = 0; i < count; i++)
                    positions[i] = remainingSpace + i * gap;
                break;
            }
            case JustifyContent.Center:
            {
                float baseOffset = remainingSpace / 2f;
                for (int i = 0; i < count; i++)
                    positions[i] = baseOffset + i * gap;
                break;
            }
            case JustifyContent.SpaceBetween when count > 1:
            {
                float spacing = (remainingSpace - gap * (count - 1)) / (count - 1);
                for (int i = 0; i < count; i++)
                    positions[i] = i * (gap + spacing);
                break;
            }
            case JustifyContent.SpaceAround:
            {
                float spacing = (remainingSpace - gap * (count - 1)) / count;
                float halfGap = spacing / 2f;
                for (int i = 0; i < count; i++)
                    positions[i] = halfGap + i * (gap + spacing);
                break;
            }
            default: // FlexStart
            {
                for (int i = 0; i < count; i++)
                    positions[i] = i * gap;
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

    #region Border emission for flex items

    /// <summary>
    /// Emits a border line for a flex item if the border side is visible.
    /// </summary>
    private static void EmitFlexBorder(LayoutResult result, int page,
        float x1, float y1, float x2, float y2, CssBorderSide side)
    {
        if (!side.IsVisible) return;
        result.Primitives.Add(new BorderLinePrimitive
        {
            PageIndex = page,
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Width = side.Width.Points,
            Color = side.Color,
            Style = side.Style,
        });
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
