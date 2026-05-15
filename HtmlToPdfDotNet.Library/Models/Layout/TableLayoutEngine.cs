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

        // 1. Structural collection: Collect rows and their cells once.
        List<RowData> rows = CollectRowsWithCells(tableNode);
        if (rows.Count == 0) return 0f;

        // Detect max columns
        int colCount = rows.Max(r => r.Cells.Count);
        if (colCount == 0) return 0f;

        // 2. Column distribution
        float tableWidth = availableWidth - tableStyle.Margin.Left.Points - tableStyle.Margin.Right.Points;
        float[] colWidths = DistributeColumnWidths(rows[0].Cells, colCount, tableWidth, styles);

        // 3. Layout for each row
        foreach (RowData row in rows)
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

                foreach (RenderPrimitive prim in layout.Result.Primitives)
                {
                    if (prim is TextPrimitive tp)
                    {
                        result.Primitives.Add(new TextPrimitive
                        {
                            PageIndex = newPage,
                            X = offsetX + tp.X,
                            Y = offsetY + tp.Y,
                            Text = tp.Text,
                            FontName = tp.FontName,
                            FontSize = tp.FontSize,
                            Bold = tp.Bold,
                            Italic = tp.Italic,
                            Color = tp.Color,
                            EmbeddedFont = tp.EmbeddedFont
                        });
                    }
                    else if (prim is ImagePrimitive ip)
                    {
                        result.Primitives.Add(new ImagePrimitive
                        {
                            PageIndex = newPage,
                            X = offsetX + ip.X,
                            Y = offsetY + ip.Y,
                            Width = ip.Width,
                            Height = ip.Height,
                            ImageData = ip.ImageData,
                            XObjectAlias = ip.XObjectAlias
                        });
                    }
                    else if (prim is RectPrimitive rp)
                    {
                        result.Primitives.Add(new RectPrimitive
                        {
                            PageIndex = newPage,
                            X = offsetX + rp.X,
                            Y = offsetY + rp.Y,
                            Width = rp.Width,
                            Height = rp.Height,
                            Fill = rp.Fill
                        });
                    }
                    else if (prim is BorderLinePrimitive blp)
                    {
                        result.Primitives.Add(new BorderLinePrimitive
                        {
                            PageIndex = newPage,
                            X1 = offsetX + blp.X1,
                            Y1 = offsetY + blp.Y1,
                            X2 = offsetX + blp.X2,
                            Y2 = offsetY + blp.Y2,
                            Width = blp.Width,
                            Color = blp.Color,
                            Style = blp.Style
                        });
                    }
                }
            }

            newCursorY += rowHeight;
        }

        newCursorY += tableStyle.Margin.Bottom.Points;
        result.PageCount = Math.Max(result.PageCount, newPage + 1);
        return newCursorY - cursorY;
    }

    #region Structure
    /// <summary>
    /// Collect all rows and their cells from the table.
    /// </summary>
    /// <param name="table">The table node.</param>
    /// <returns>The list of row data.</returns>
    private static List<RowData> CollectRowsWithCells(HtmlNode table)
    {
        List<RowData> rows = new();
        // Only look at direct children (thead, tbody, tfoot) to avoid nesting issues
        foreach (HtmlNode section in table.ChildNodes)
        {
            if (section.Name.Equals("thead", StringComparison.OrdinalIgnoreCase) ||
                section.Name.Equals("tbody", StringComparison.OrdinalIgnoreCase) ||
                section.Name.Equals("tfoot", StringComparison.OrdinalIgnoreCase))
            {
                foreach (HtmlNode? tr in section.ChildNodes.Where(n => n.Name.Equals("tr", StringComparison.OrdinalIgnoreCase)))
                {
                    rows.Add(new RowData { Node = tr, Cells = GetCells(tr) });
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