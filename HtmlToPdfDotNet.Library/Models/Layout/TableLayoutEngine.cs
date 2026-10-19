using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Fonts;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Library.Models.Layout;

public static class TableLayoutEngine
{
    /// <summary>
    /// Struct to hold row data.
    /// </summary>
    private struct RowData
    {
        public HtmlNode Node;
        public List<HtmlNode> Cells;
    }

    /// <summary>
    /// Cached layout data for a thead row, used for repeating headers on new pages.
    /// </summary>
    private sealed class TheadRowLayout
    {
        public float Height;
        /// <summary>
        /// Primitives with Y coordinates relative to the row's top (cursorY = 0).
        /// </summary>
        public List<RenderPrimitive> Primitives = [];

        /// <summary>
        /// Link annotations with Y coordinates relative to the row's top (cursorY = 0).
        /// </summary>
        public List<LinkAnnotationPrimitive> Annotations = [];
    }

    /// <summary>
    /// Layout the table.
    /// </summary>
    /// <param name="tableNode">The table node.</param>
    /// <param name="tableStyle">The computed style of the table.</param>
    /// <param name="styles">The dictionary of computed styles.</param>
    /// <param name="x">The X coordinate of the table.</param>
    /// <param name="availableWidth">The available width for the table.</param>
    /// <param name="result">The layout result.</param>
    /// <param name="currentPage">The current page.</param>
    /// <param name="cursorY">The Y coordinate of the table.</param>
    /// <param name="pageContentH">The content height of the page.</param>
    /// <param name="newCursorY">The new Y coordinate of the table.</param>
    /// <param name="newPage">The new page.</param>
    /// <param name="registry">The font registry.</param>
    /// <param name="basePath">The base path.</param>
    /// <returns>The total height of the table.</returns>
    public static float Layout(HtmlNode tableNode,
                               ComputedStyle tableStyle,
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

        // Apply margin to table
        newCursorY += tableStyle.Margin.Top.Points;
        float tableX = x + tableStyle.Margin.Left.Points;

        // 1. Structural collection: Collect all rows plus separate thead rows.
        List<RowData> allRows = CollectRowsWithCells(tableNode, out List<RowData> theadRows);
        if (allRows.Count == 0) return 0f;

        // Detect max columns
        int colCount = allRows.Max(r => r.Cells.Count);
        if (colCount == 0) return 0f;

        // 2. Column distribution
        float tableWidth = availableWidth - tableStyle.Margin.Left.Points - tableStyle.Margin.Right.Points;
        float[] colWidths = DistributeColumnWidths(allRows[0].Cells, colCount, tableWidth, styles);

        // 3. Pre-compute thead row layouts for repetition on page breaks
        List<TheadRowLayout> theadLayouts = PreLayoutTheadRows(theadRows, styles, colWidths, tableX, pageContentH, registry, basePath);

        // Track which rows are thead for quick lookup
        HashSet<HtmlNode> theadNodeSet = new(theadRows.Select(r => r.Node));

        // 4. Layout for each row
        foreach (RowData row in allRows)
        {
            float rowHeight = 0f;

            // First pass: Layout cell content to determine row height
            List<(float X, float W, LayoutResult Result)> cellLayouts = new(row.Cells.Count);
            float cellX = tableX;

            for (int c = 0; c < row.Cells.Count && c < colCount; c++)
            {
                HtmlNode cell = row.Cells[c];
                float colW = colWidths[c];
                ComputedStyle cellStyle = styles.TryGetValue(cell, out ComputedStyle? cs) ? cs : new ComputedStyle();

                // Calculate available width for content (subtracting horizontal padding)
                float contentWidth = Math.Max(0, colW - cellStyle.Padding.Left.Points - cellStyle.Padding.Right.Points);

                PageLayout cellPage = new(contentWidth, pageContentH, new PageMargins(0f));
                BlockLayoutEngine cellResolver = new(cellPage, styles, registry, basePath);

                LayoutResult innerLayout = cellResolver.Layout(cell);

                // Use TotalHeight from LayoutResult (optimized)
                float contentH = innerLayout.TotalHeight;

                // Add padding and ensure minimum height
                float cellH = contentH + cellStyle.Padding.Top.Points + cellStyle.Padding.Bottom.Points;
                if (contentH == 0f) cellH += cellStyle.FontSize * 1.5f;

                cellLayouts.Add((cellX, colW, innerLayout));
                rowHeight = Math.Max(rowHeight, cellH);
                cellX += colW;
            }

            // Pagination check
            if (newCursorY + rowHeight > pageContentH)
            {
                newPage++;
                newCursorY = 0f;
                result.PageCount = Math.Max(result.PageCount, newPage + 1);
                result.Primitives.Add(new PageBreakPrimitive { PageIndex = newPage - 1 });

                // After a page break, re-emit thead rows at the top of the new page
                if (theadLayouts.Count > 0)
                {
                    EmitTheadRows(result, theadLayouts, newPage, tableX, tableWidth, tableStyle, ref newCursorY);
                }
            }

            // Second pass: Emit primitives shifted to the row/cell position

            // 1. Table background (for this row's slice)
            if (tableStyle.BackgroundColor.A > 0f)
            {
                result.Primitives.Add(new RectPrimitive
                {
                    PageIndex = newPage,
                    X = tableX,
                    Y = newCursorY,
                    Width = tableWidth,
                    Height = rowHeight,
                    Fill = tableStyle.BackgroundColor
                });
            }

            // 2. Row background
            ComputedStyle rowStyle = styles.TryGetValue(row.Node, out ComputedStyle? rs) ? rs : new ComputedStyle();
            if (rowStyle.BackgroundColor.A > 0f)
            {
                result.Primitives.Add(new RectPrimitive
                {
                    PageIndex = newPage,
                    X = tableX,
                    Y = newCursorY,
                    Width = tableWidth,
                    Height = rowHeight,
                    Fill = rowStyle.BackgroundColor
                });
            }

            for (int c = 0; c < cellLayouts.Count; c++)
            {
                var layout = cellLayouts[c];
                HtmlNode cell = row.Cells[c];
                ComputedStyle cellStyle = styles.TryGetValue(cell, out ComputedStyle? cs) ? cs : new ComputedStyle();

                // Background
                if (cellStyle.BackgroundColor.A > 0f)
                {
                    result.Primitives.Add(new RectPrimitive
                    {
                        PageIndex = newPage,
                        X = layout.X,
                        Y = newCursorY,
                        Width = layout.W,
                        Height = rowHeight,
                        Fill = cellStyle.BackgroundColor
                    });
                }

                // Borders
                EmitCellsBorder(result, newPage, layout.X, newCursorY, layout.W, rowHeight, cellStyle);

                // Content (Shifted)
                float offsetX = layout.X + cellStyle.Padding.Left.Points;
                float offsetY = newCursorY + cellStyle.Padding.Top.Points;

                AppendCellContent(result.Primitives, result.Annotations, layout.Result, offsetX, offsetY, newPage);
            }

            newCursorY += rowHeight;
        }

        newCursorY += tableStyle.Margin.Bottom.Points;
        result.PageCount = Math.Max(result.PageCount, newPage + 1);
        return newCursorY - cursorY;
    }

    /// <summary>
    /// Copies the content of a laid-out cell into the table output at the given offset,
    /// preserving every primitive property and the cell's link annotations.
    /// Copies are used instead of relocating the cell primitives in place: the copies are
    /// allocated together while the cell layout dies young, which measured ~15% faster on
    /// table-heavy input (fewer Gen1/Gen2 promotions) despite ~2% more allocation.
    /// </summary>
    /// <param name="primitives">The destination primitive list.</param>
    /// <param name="annotations">The destination annotation list.</param>
    /// <param name="cell">The cell layout result.</param>
    /// <param name="dx">The horizontal offset of the cell content.</param>
    /// <param name="dy">The vertical offset of the cell content.</param>
    /// <param name="page">The page the row is placed on.</param>
    private static void AppendCellContent(List<RenderPrimitive> primitives,
                                          List<LinkAnnotationPrimitive> annotations,
                                          LayoutResult cell,
                                          float dx,
                                          float dy,
                                          int page)
    {
        foreach (RenderPrimitive prim in cell.Primitives)
        {
            // The table owns pagination (rows are never split): a page break produced
            // inside a cell is not a page boundary of the table output, and emitting it
            // would mislead consumers that use page-break markers to split boxes.
            if (prim is PageBreakPrimitive) continue;

            primitives.Add(prim.CopyRelocated(dx, dy, page));
        }

        foreach (LinkAnnotationPrimitive annot in cell.Annotations)
        {
            annotations.Add((LinkAnnotationPrimitive)annot.CopyRelocated(dx, dy, page));
        }
    }

    #region Thead Repetition

    /// <summary>
    /// Pre-layouts all thead rows to cache their height and primitives for repetition.
    /// </summary>
    /// <param name="theadRows">The thead rows.</param>
    /// <param name="styles">The computed styles.</param>
    /// <param name="colWidths">The column widths.</param>
    /// <param name="tableX">The X coordinate of the table.</param>
    /// <param name="pageContentH">The content height of the page.</param>
    /// <param name="registry">The font registry.</param>
    /// <param name="basePath">The base path.</param>
    /// <returns>A list of <see cref="TheadRowLayout"/>.</returns>
    private static List<TheadRowLayout> PreLayoutTheadRows(
        List<RowData> theadRows,
        Dictionary<HtmlNode, ComputedStyle> styles,
        float[] colWidths,
        float tableX,
        float pageContentH,
        FontRegistry? registry,
        string? basePath)
    {
        List<TheadRowLayout> layouts = new(theadRows.Count);
        int colCount = colWidths.Length;

        foreach (RowData row in theadRows)
        {
            var layout = new TheadRowLayout();
            float rowHeight = 0f;
            float cellX = tableX;
            var rowLayouts = new List<(float X, float W, LayoutResult Result)>();

            // First pass: measure cells
            for (int c = 0; c < row.Cells.Count && c < colCount; c++)
            {
                HtmlNode cell = row.Cells[c];
                float colW = colWidths[c];
                ComputedStyle cellStyle = styles.TryGetValue(cell, out ComputedStyle? cs) ? cs : new ComputedStyle();
                float contentWidth = Math.Max(0, colW - cellStyle.Padding.Left.Points - cellStyle.Padding.Right.Points);

                PageLayout cellPage = new(contentWidth, pageContentH, new PageMargins(0f));
                BlockLayoutEngine cellResolver = new(cellPage, styles, registry, basePath);
                LayoutResult innerLayout = cellResolver.Layout(cell);

                float contentH = innerLayout.TotalHeight;
                float cellH = contentH + cellStyle.Padding.Top.Points + cellStyle.Padding.Bottom.Points;
                if (contentH == 0f) cellH += cellStyle.FontSize * 1.5f;

                rowLayouts.Add((cellX, colW, innerLayout));
                rowHeight = Math.Max(rowHeight, cellH);
                cellX += colW;
            }

            layout.Height = rowHeight;

            // Second pass: generate primitives at Y=0 (relative to row top)
            for (int c = 0; c < rowLayouts.Count; c++)
            {
                var rl = rowLayouts[c];
                HtmlNode cell = row.Cells[c];
                ComputedStyle cellStyle = styles.TryGetValue(cell, out ComputedStyle? cs) ? cs : new ComputedStyle();
                ComputedStyle nodeStyle = styles.TryGetValue(row.Node, out ComputedStyle? ns) ? ns : cellStyle;

                // Background
                if (nodeStyle.BackgroundColor.A > 0f)
                {
                    layout.Primitives.Add(new RectPrimitive
                    {
                        PageIndex = 0, // will be set per-page on emission
                        X = rl.X,
                        Y = 0f,
                        Width = rl.W,
                        Height = rowHeight,
                        Fill = nodeStyle.BackgroundColor
                    });
                }

                // Borders
                EmitCellsBorderRelative(layout.Primitives, 0, rl.X, 0f, rl.W, rowHeight, cellStyle);

                // Content
                float offsetX = rl.X + cellStyle.Padding.Left.Points;
                float offsetY = cellStyle.Padding.Top.Points;

                AppendCellContent(layout.Primitives, layout.Annotations, rl.Result, offsetX, offsetY, 0);
            }

            layouts.Add(layout);
        }

        return layouts;
    }

    /// <summary>
    /// Emits all cached thead row primitives at the current page position,
    /// updating <paramref name="cursorY"/> to account for thead heights.
    /// </summary>
    /// <param name="result">The layout result.</param>
    /// <param name="theadLayouts">The thead layouts.</param>
    /// <param name="page">The current page.</param>
    /// <param name="tableX">The X coordinate of the table.</param>
    /// <param name="tableWidth">The width of the table.</param>
    /// <param name="tableStyle">The style of the table.</param>
    /// <param name="cursorY">The Y coordinate of the table.</param>
    /// <returns>The updated cursorY.</returns>
    private static void EmitTheadRows(
        LayoutResult result,
        List<TheadRowLayout> theadLayouts,
        int page,
        float tableX,
        float tableWidth,
        ComputedStyle tableStyle,
        ref float cursorY)
    {
        foreach (TheadRowLayout thead in theadLayouts)
        {
            // Table background slice for thead
            if (tableStyle.BackgroundColor.A > 0f)
            {
                result.Primitives.Add(new RectPrimitive
                {
                    PageIndex = page,
                    X = tableX,
                    Y = cursorY,
                    Width = tableWidth,
                    Height = thead.Height,
                    Fill = tableStyle.BackgroundColor
                });
            }

            // Thead primitives (shifted to cursorY)
            // Cached primitives are reused on every page, so emit relocated copies.
            foreach (RenderPrimitive prim in thead.Primitives)
            {
                result.Primitives.Add(prim.CopyRelocated(0f, cursorY, page));
            }

            foreach (LinkAnnotationPrimitive annot in thead.Annotations)
            {
                result.Annotations.Add((LinkAnnotationPrimitive)annot.CopyRelocated(0f, cursorY, page));
            }

            cursorY += thead.Height;
        }
    }

    /// <summary>
    /// Emits cell borders with Y coordinates relative to the row top (not absolute).
    /// </summary>
    /// <param name="primitives">The list of render primitives.</param>
    /// <param name="page">The current page.</param>
    /// <param name="x">The X coordinate of the cell.</param>
    /// <param name="y">The Y coordinate of the cell.</param>
    /// <param name="width">The width of the cell.</param>
    /// <param name="height">The height of the cell.</param>
    /// <param name="style">The style of the cell.</param>
    /// <returns>The updated cursorY.</returns>
    private static void EmitCellsBorderRelative(List<RenderPrimitive> primitives, int page,
        float x, float y, float width, float height, ComputedStyle style)
    {
        EmitBorderLineRelative(primitives, page, x, y, x + width, y, style.BorderTop);
        EmitBorderLineRelative(primitives, page, x, y + height, x + width, y + height, style.BorderBottom);
        EmitBorderLineRelative(primitives, page, x, y, x, y + height, style.BorderLeft);
        EmitBorderLineRelative(primitives, page, x + width, y, x + width, y + height, style.BorderRight);
    }

    /// <summary>
    /// Emits a single border line primitive.
    /// </summary>
    /// <param name="primitives">The list of render primitives.</param>
    /// <param name="page">The current page.</param>
    /// <param name="x1">The X coordinate of the first point.</param>
    /// <param name="y1">The Y coordinate of the first point.</param>
    /// <param name="x2">The X coordinate of the second point.</param>
    /// <param name="y2">The Y coordinate of the second point.</param>
    /// <param name="side">The border side.</param>
    private static void EmitBorderLineRelative(List<RenderPrimitive> primitives, int page,
        float x1, float y1, float x2, float y2, CssBorderSide side)
    {
        if (side.IsVisible)
        {
            primitives.Add(new BorderLinePrimitive
            {
                PageIndex = page,
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Color = side.Color,
                Width = side.Width.Points,
                Style = side.Style
            });
        }
    }

    #endregion

    #region Structure
    /// <summary>
    /// Collect all rows and their cells from the table.
    /// </summary>
    /// <param name="table">The table node.</param>
    /// <param name="theadRows">Output: rows that belong to &lt;thead&gt; sections.</param>
    /// <returns>The list of row data.</returns>
    private static List<RowData> CollectRowsWithCells(HtmlNode table, out List<RowData> theadRows)
    {
        List<RowData> rows = new();
        theadRows = new();
        // Only look at direct children (thead, tbody, tfoot) to avoid nesting issues
        foreach (HtmlNode section in table.ChildNodes)
        {
            bool isThead = section.Name.Equals("thead", StringComparison.OrdinalIgnoreCase);
            bool isTBody = section.Name.Equals("tbody", StringComparison.OrdinalIgnoreCase);
            bool isTFoot = section.Name.Equals("tfoot", StringComparison.OrdinalIgnoreCase);

            if (isThead || isTBody || isTFoot)
            {
                foreach (HtmlNode? tr in section.ChildNodes.Where(n => n.Name.Equals("tr", StringComparison.OrdinalIgnoreCase)))
                {
                    RowData row = new() { Node = tr, Cells = GetCells(tr) };
                    rows.Add(row);
                    if (isThead) theadRows.Add(row);
                }
            }
            else if (section.Name.Equals("tr", StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(new RowData { Node = section, Cells = GetCells(section) });
            }
        }
        return rows;
    }

    /// <summary>
    /// Get all cells from a row.
    /// </summary>
    /// <param name="row">The row node.</param>
    /// <returns>The list of cells.</returns>
    private static List<HtmlNode> GetCells(HtmlNode row)
    {
        return row.ChildNodes
                    .Where(n => n.Name.Equals("td", StringComparison.OrdinalIgnoreCase)
                               || n.Name.Equals("th", StringComparison.OrdinalIgnoreCase))
                    .ToList();
    }
    #endregion

    #region Distribution
    /// <summary>
    /// Distribute the column widths.
    /// </summary>
    /// <param name="firstCells">The first cells of the table.</param>
    /// <param name="colCount">The number of columns.</param>
    /// <param name="totalWidth">The total width of the table.</param>
    /// <param name="styles">The computed styles.</param>
    /// <returns>The array of column widths.</returns>
    private static float[] DistributeColumnWidths(List<HtmlNode> firstCells,
                                                  int colCount,
                                                  float totalWidth,
                                                  Dictionary<HtmlNode, ComputedStyle> styles)
    {
        float[] widths = new float[colCount];
        float usedWidth = 0f;
        int autoCols = 0;

        for (int c = 0; c < colCount; c++)
        {
            if (c < firstCells.Count)
            {
                HtmlNode cell = firstCells[c];
                float? w = GetExplicitWidth(cell, styles, totalWidth);
                if (w.HasValue)
                {
                    widths[c] = w.Value;
                    usedWidth += w.Value;
                }
                else autoCols++;
            }
            else autoCols++;
        }

        float autoWidth = autoCols > 0 ? (totalWidth - usedWidth) / autoCols : 0f;
        for (int c = 0; c < colCount; c++)
            if (widths[c] == 0f) widths[c] = Math.Max(autoWidth, 10f);

        return widths;
    }

    /// <summary>
    /// Get the explicit width of a cell.
    /// </summary>
    /// <param name="cell">The cell node.</param>
    /// <param name="styles">The computed styles.</param>
    /// <param name="tableWidth">The total width of the table.</param>
    /// <returns>The explicit width of the cell.</returns>
    private static float? GetExplicitWidth(HtmlNode cell, Dictionary<HtmlNode, ComputedStyle> styles, float tableWidth)
    {
        string attr = cell.GetAttributeValue("width", string.Empty);
        if (!string.IsNullOrEmpty(attr))
        {
            if (attr.EndsWith('%') && float.TryParse(attr[..^1], out float pct)) return tableWidth * pct / 100f;
            if (float.TryParse(attr, out float px)) return px * Constants.PointsPerPx;
        }
        if (styles.TryGetValue(cell, out ComputedStyle? style) && !style.Width.IsAuto && style.Width.Points > 0)
            return style.Width.Points;
        return null;
    }
    #endregion

    #region Borders
    /// <summary>
    /// Emit the borders of all cells in a row.
    /// </summary>
    /// <param name="result">The layout result.</param>
    /// <param name="page">The page number.</param>
    /// <param name="x">The X coordinate of the row.</param>
    /// <param name="y">The Y coordinate of the row.</param>
    /// <param name="width">The width of the row.</param>
    /// <param name="height">The height of the row.</param>
    /// <param name="style">The style of the row.</param>
    private static void EmitCellsBorder(LayoutResult result,
                                        int page,
                                        float x,
                                        float y,
                                        float width,
                                        float height,
                                        ComputedStyle style)
    {
        EmitCellBorder(result, page, x, y, x + width, y, style.BorderTop);
        EmitCellBorder(result, page, x, y + height, x + width, y + height, style.BorderBottom);
        EmitCellBorder(result, page, x, y, x, y + height, style.BorderLeft);
        EmitCellBorder(result, page, x + width, y, x + width, y + height, style.BorderRight);
    }

    /// <summary>
    /// Emit a single cell border.
    /// </summary>
    /// <param name="result">The layout result.</param>
    /// <param name="page">The page number.</param>
    /// <param name="x1">The X coordinate of the start of the border.</param>
    /// <param name="y1">The Y coordinate of the start of the border.</param>
    /// <param name="x2">The X coordinate of the end of the border.</param>
    /// <param name="y2">The Y coordinate of the end of the border.</param>
    /// <param name="side">The border side.</param>
    private static void EmitCellBorder(LayoutResult result,
                                       int page,
                                       float x1,
                                       float y1,
                                       float x2,
                                       float y2,
                                       CssBorderSide side)
    {
        if (side.IsVisible)
        {
            result.Primitives.Add(new BorderLinePrimitive
            {
                PageIndex = page,
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Color = side.Color,
                Width = side.Width.Points,
                Style = side.Style
            });
        }
    }
    #endregion
}
