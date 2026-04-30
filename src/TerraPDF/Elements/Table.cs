using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Column sizing policy for <see cref="Table"/>.</summary>
internal enum TableColumnType { Relative, Constant }

internal sealed class TableColumn
{
    internal TableColumnType Type           { get; set; } = TableColumnType.Relative;
    internal double          RelativeWeight { get; set; } = 1;
    internal double          ConstantWidth  { get; set; }
}

internal sealed class TableCell
{
    internal int           Column      { get; set; }   // 1-based
    internal int           Row         { get; set; }   // 1-based
    internal int           ColumnSpan  { get; set; } = 1;
    internal int           RowSpan     { get; set; } = 1;
    internal Container Slot        { get; }       = new();
}

/// <summary>Lays out content in a grid of rows and columns.</summary>
internal sealed class Table : Element
{
    internal List<TableColumn> Columns        { get; } = [];
    internal List<TableCell>   Cells          { get; } = [];
    /// <summary>
    /// Number of leading rows treated as the table header.
    /// These rows are repeated at the top of every continuation page when the
    /// table is split by <see cref="TerraPDF.Core.DocumentComposer"/>.
    /// </summary>
    internal int HeaderRowCount { get; set; }

    internal TableCell AddCell(int column, int row, int columnSpan = 1, int rowSpan = 1)
    {
        var cell = new TableCell { Column = column, Row = row, ColumnSpan = columnSpan, RowSpan = rowSpan };
        Cells.Add(cell);
        return cell;
    }

    // -- Column widths ---------------------------------------------

    internal double[] GetColumnWidths(double available)
    {
        if (Columns.Count == 0) return [];

        var    widths       = new double[Columns.Count];
        double constTotal   = 0;
        double relWeightSum = 0;

        for (int i = 0; i < Columns.Count; i++)
        {
            var col = Columns[i];
            if (col.Type == TableColumnType.Constant)
            {
                widths[i]  = col.ConstantWidth;
                constTotal += col.ConstantWidth;
            }
            else
            {
                relWeightSum += col.RelativeWeight;
            }
        }

        double relSpace = Math.Max(0, available - constTotal);
        for (int i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].Type == TableColumnType.Relative)
                widths[i] = relWeightSum > 0
                    ? relSpace * (Columns[i].RelativeWeight / relWeightSum)
                    : 0;
        }

        return widths;
    }

    // -- Row heights -----------------------------------------------

    internal double[] GetRowHeights(double[] colWidths, TextStyle? defaultStyle = null)
    {
        int rowCount = Cells.Count > 0
            ? Cells.Max(c => c.Row + c.RowSpan - 1)
            : 0;
        var heights = new double[rowCount];

        foreach (var cell in Cells)
        {
            if (cell.RowSpan > 1) continue;  // span cells are sized by the rows they cover, not measured here

            double cellWidth = 0;
            for (int c = cell.Column - 1; c < cell.Column - 1 + cell.ColumnSpan && c < colWidths.Length; c++)
                cellWidth += colWidths[c];

            var sz = cell.Slot.Measure(cellWidth, double.MaxValue, defaultStyle);
            int r  = cell.Row - 1;
            if (r < heights.Length && sz.Height > heights[r])
                heights[r] = sz.Height;
        }

        return heights;
    }

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null)
    {
        var colWidths  = GetColumnWidths(w);
        var rowHeights = GetRowHeights(colWidths, defaultStyle);
        return new ElementSize(w, rowHeights.Sum());
    }

    // -- Draw (full table) -----------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        var colWidths  = GetColumnWidths(ctx.Width);
        var rowHeights = GetRowHeights(colWidths);
        var allRows    = Enumerable.Range(0, rowHeights.Length).ToList();
        DrawRows(ctx, colWidths, rowHeights, allRows);
    }

    // -- Draw (row subset) -----------------------------------------

    /// <summary>
    /// Draws only the rows identified by <paramref name="rowIndices"/> (0-based),
    /// stacking them from <c>ctx.Y</c> downward.  Used by the page-break splitter
    /// to render header rows + a page-sized slice of data rows per page.
    /// </summary>
    internal void DrawRows(DrawingContext ctx, double[] colWidths, double[] rowHeights,
                           List<int> rowIndices)
    {
        if (rowIndices.Count == 0) return;

        // Pre-compute column X positions
        var colX = new double[colWidths.Length];
        double cx = ctx.X;
        for (int i = 0; i < colWidths.Length; i++) { colX[i] = cx; cx += colWidths[i]; }

        // Map each drawn row index → its Y position (stacked from ctx.Y)
        var rowY = new Dictionary<int, double>(rowIndices.Count);
        double y = ctx.Y;
        foreach (int ri in rowIndices)
        {
            if (ri >= 0 && ri < rowHeights.Length)
            {
                rowY[ri] = y;
                y += rowHeights[ri];
            }
        }

        foreach (var cell in Cells)
        {
            int ri = cell.Row    - 1;  // 0-based
            int ci = cell.Column - 1;  // 0-based

            if (!rowY.TryGetValue(ri, out double cellY)) continue;
            if (ci < 0 || ci >= colWidths.Length || ri >= rowHeights.Length) continue;

            double cellX = colX[ci];
            double cellW = 0;
            for (int c = ci; c < ci + cell.ColumnSpan && c < colWidths.Length; c++)
                cellW += colWidths[c];

            // Height: sum spanned rows that are present in the drawn set; truncate at page boundary
            double cellH = 0;
            for (int r = ri; r < ri + cell.RowSpan && r < rowHeights.Length; r++)
            {
                if (rowY.ContainsKey(r)) cellH += rowHeights[r];
                else break;  // row not on this page — truncate span
            }
            if (cellH <= 0) cellH = rowHeights[ri];

            cell.Slot.Draw(ctx.At(cellX, cellY, cellW, cellH));
        }
    }
}
