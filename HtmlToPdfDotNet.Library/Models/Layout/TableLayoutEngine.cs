using HtmlAgilityPack;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Library.Models.Layout;

public static class TableLayoutEngine
{
    /// <summary>
    /// Layout the table.
    /// </summary>
    /// <param name="tableNode">The HTML table node.</param>
    /// <param name="tableStyle">The computed style of the table.</param>
    /// <param name="styles">The computed styles of the table cells.</param>
    /// <param name="x">The X coordinate of the table.</param>
    /// <param name="availableWidth">The available width for the table.</param>
    /// <param name="result">The layout result to add primitives to.</param>
    /// <param name="currentPage">The current page number.</param>
    /// <param name="cursorY">The Y coordinate of the table.</param>
    /// <param name="pageContentH">The height of the content area.</param>
    /// <param name="newCursorY">The new Y coordinate after the table is laid out.</param>
    /// <param name="newPage">The new page number after the table is laid out.</param>
    /// <returns>The height of the table.</returns>
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
                               out int newPage)
    {
        newCursorY = cursorY;
        newPage = currentPage;

        // Apply margin to table
        newCursorY += tableStyle.Margin.Top.Points;
        float tableX = x + tableStyle.Margin.Left.Points;

        // Collect rows
        List<HtmlNode> rows = CollectRows(tableNode);


        // Detect number of columns
        int colCount = rows.Max(r => CountCells(r));

        if (rows.Count == 0 || colCount == 0) return 0f;

        // Available width for table
        float tableWidth = availableWidth - tableStyle.Margin.Left.Points - tableStyle.Margin.Right.Points;

        // Distribute columns width
        float[] colWidths = DistributeColumnWidths(rows, colCount, tableWidth, styles);
        // Layout for each row
        foreach (HtmlNode row in rows)
        {
            List<HtmlNode> cells = GetCells(row);
            float rowHeight = 0f;
            float cellX = tableX;

            // First pass: calculate required height by each cell
            List<(float X, float W, float H, List<RenderPrimitive> Prims)> cellLayouts = [];

            for (int c = 0; c < cells.Count; c++)
            {
                HtmlNode cell = cells[c];
                ComputedStyle cellStyle = styles.TryGetValue(cell, out ComputedStyle? cs) ? cs : new ComputedStyle();
                float colW = colWidths[c];

                // Mini-Layout for cell content
                LayoutResult cellResult = new();
                BlockLayoutEngine cellResolver = new(new PageLayout(colW, pageContentH, new PageMargins(0f)),
                                                     styles);
                LayoutResult innerLayout = cellResolver.Layout(cell);
                float cellH = innerLayout.Primitives.OfType<TextPrimitive>().Select(t => t.Y).DefaultIfEmpty(0f).Max() +
                              cellStyle.FontSize * 1.5f +
                              cellStyle.Padding.Top.Points +
                              cellStyle.Padding.Bottom.Points;
                cellLayouts.Add((cellX, colW, cellH, innerLayout.Primitives.ToList()));
                rowHeight = Math.Max(rowHeight, cellH);
                cellX += colW;
            }

            // Check if the row fits in the current page
            if (newCursorY + rowHeight > pageContentH)
            {
                newPage++;
                newCursorY = 0f;
                result.Primitives.Add(new PageBreakPrimitive { PageIndex = newPage - 1 });
                result.PageCount = newPage + 1;
            }

            // Second pass: Emit primitives fixed to row position
            cellX = tableX;
            for (int c = 0; c < cells.Count; c++)
            {
                (float cx, float cw, float ch, List<RenderPrimitive> prims) = cellLayouts[c];
                HtmlNode cell = cells[c];
                ComputedStyle cellStyle = styles.TryGetValue(cell, out ComputedStyle? cs) ? cs : new ComputedStyle();

                // Cell's background
                if (cellStyle.BackgroundColor.A > 0f)
                {
                    result.Primitives.Add(new RectPrimitive
                    {
                        PageIndex = newPage,
                        X = cellX,
                        Y = newCursorY,
                        Width = cw,
                        Height = rowHeight,
                        Fill = cellStyle.BackgroundColor
                    });
                }

                // Cell's borders
                EmitCellsBolder(result, newPage, cx, newCursorY, cw, rowHeight, cellStyle);

                // Text: Primitive's offset Y of mini-layout
                foreach (RenderPrimitive prim in prims)
                {
                    prim.PageIndex = newPage;
                    if (prim is TextPrimitive tp)
                    {
                        result.Primitives.Add(new TextPrimitive
                        {
                            PageIndex = newPage,
                            X = cx + cellStyle.Padding.Left.Points + tp.X,
                            Y = newCursorY + cellStyle.Padding.Top.Points + tp.Y,
                            Text = tp.Text,
                            FontName = tp.FontName,
                            FontSize = tp.FontSize,
                            Bold = tp.Bold,
                            Italic = tp.Italic,
                            Color = tp.Color
                        });
                    }
                }

            }

            newCursorY += rowHeight;
        }

        newCursorY += tableStyle.Margin.Bottom.Points;
        return newCursorY - cursorY;
    }

    #region Structure
    /// <summary>
    /// Collect rows from a table.
    /// </summary>
    /// <param name="table">The table node.</param>
    /// <returns>A list of table rows.</returns>
    private static List<HtmlNode> CollectRows(HtmlNode table)
    {
        List<HtmlNode> rows = new();
        CollectRowsRecursive(table, rows);
        return rows;
    }

    /// <summary>
    /// Collect rows recursively from a node.
    /// </summary>
    /// <param name="node">The node to collect rows from.</param>
    /// <param name="rows">The list to add rows to.</param>
    private static void CollectRowsRecursive(HtmlNode node, List<HtmlNode> rows)
    {
        foreach (HtmlNode child in node.ChildNodes)
        {
            if (child.Name.Equals("tr", StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(child);
            }
            else
            {
                CollectRowsRecursive(child, rows);
            }
        }
    }

    /// <summary>
    /// Get cells from a row.
    /// </summary>
    /// <param name="row">The row node.</param>
    /// <returns>A list of cell nodes.</returns>
    private static List<HtmlNode> GetCells(HtmlNode row)
    {
        return row.ChildNodes
                    .Where(n => n.Name.Equals("td", StringComparison.OrdinalIgnoreCase)
                               || n.Name.Equals("th", StringComparison.OrdinalIgnoreCase))
                    .ToList();
    }

    /// <summary>
    /// Count cells in a row.
    /// </summary>
    /// <param name="row">The row node.</param>
    /// <returns>The number of cells in the row.</returns>
    private static int CountCells(HtmlNode row) => GetCells(row).Count;
    #endregion

    #region Distribution of columns widths
    /// <summary>
    /// Distribute column widths for a table.
    /// </summary>
    /// <param name="rows">The list of table rows.</param>
    /// <param name="colCount">The number of columns.</param>
    /// <param name="totalWidth">The total width available for the table.</param>
    /// <param name="styles">The dictionary of computed styles.</param>
    /// <returns>An array of column widths.</returns>
    private static float[] DistributeColumnWidths(List<HtmlNode> rows,
                                                  int colCount,
                                                  float totalWidth,
                                                  Dictionary<HtmlNode, ComputedStyle> styles)
    {
        float[] widths = new float[colCount];

        // Try reading explicit widths of the first row (width or style attribute)
        List<HtmlNode> firstCells = GetCells(rows[0]);
        float usedWidth = 0f;
        int autoCols = 0;

        for (int c = 0; c < colCount; c++)
        {
            if (c < firstCells.Count)
            {
                var cell = firstCells[c];
                float? w = GetExplicitWidth(cell, styles, totalWidth);
                if (w.HasValue)
                {
                    widths[c] = w.Value;
                    usedWidth += w.Value;
                }
                else
                {
                    autoCols++;
                }
            }
            else autoCols++;
        }

        // Distribute the remaining width among the automatic columns
        float autoWidth = autoCols > 0 ? (totalWidth - usedWidth) / autoCols : 0f;
        for (int c = 0; c < colCount; c++)
            if (widths[c] == 0f) widths[c] = Math.Max(autoWidth, 10f);

        return widths;

    }

    /// <summary>
    /// Get explicit width of a cell.
    /// </summary>
    /// <param name="cell">The cell node.</param>
    /// <param name="styles">The dictionary of computed styles.</param>
    /// <param name="tableWidth">The total width available for the table.</param>
    /// <returns>The explicit width of the cell.</returns>
    private static float? GetExplicitWidth(HtmlNode cell, Dictionary<HtmlNode, ComputedStyle> styles, float tableWidth)
    {
        string attr = cell.GetAttributeValue("width", string.Empty);
        if (!string.IsNullOrEmpty(attr))
        {
            if (attr.EndsWith('%') && float.TryParse(attr[..^1], out float pct))
            {
                return tableWidth * pct / 100f;
            }
            if (float.TryParse(attr, out float px))
            {
                return px * Constants.PointsPerPx;
            }
        }

        // Computed style
        if (styles.TryGetValue(cell, out ComputedStyle? style) && !style.Width.IsAuto && style.Width.Points > 0)
        {
            return style.Width.Points;
        }

        return null;
    }
    #endregion

    #region Cell's borders
    /// <summary>
    /// Emit all borders for a table cell.
    /// </summary>
    /// <param name="result">The layout result to add primitives to.</param>
    /// <param name="page">The page number.</param>
    /// <param name="x">The X coordinate of the cell.</param>
    /// <param name="y">The Y coordinate of the cell.</param>
    /// <param name="width">The width of the cell.</param>
    /// <param name="height">The height of the cell.</param>
    /// <param name="style">The computed style of the cell.</param>
    private static void EmitCellsBolder(LayoutResult result,
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
    /// Emit a single border for a table cell.
    /// </summary>
    /// <param name="result">The layout result to add primitives to.</param>
    /// <param name="page">The page number.</param>
    /// <param name="x1">The X coordinate of the start of the border.</param>
    /// <param name="y1">The Y coordinate of the start of the border.</param>
    /// <param name="x2">The X coordinate of the end of the border.</param>
    /// <param name="y2">The Y coordinate of the end of the border.</param>
    /// <param name="side">The border side to emit.</param>
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