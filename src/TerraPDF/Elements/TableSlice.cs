using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>
/// A row subset of a <see cref="Table"/> packaged as an ordinary placeable
/// element, so the pagination engine can treat per-page table slices (header
/// rows + a batch of data rows) like any other placed item.
/// Column widths and row heights are computed once by the layout pass and
/// shared across all slices of the same table.
/// </summary>
internal sealed class TableSlice : Element
{
    private readonly Table     _table;
    private readonly double[]  _colWidths;
    private readonly double[]  _rowHeights;
    private readonly List<int> _rowIndices;

    internal TableSlice(Table table, double[] colWidths, double[] rowHeights, List<int> rowIndices)
    {
        _table      = table;
        _colWidths  = colWidths;
        _rowHeights = rowHeights;
        _rowIndices = rowIndices;
    }

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint)
    {
        double height = 0;
        foreach (int ri in _rowIndices)
        {
            if (ri >= 0 && ri < _rowHeights.Length)
                height += _rowHeights[ri];
        }
        return new ElementSize(w, height);
    }

    internal override void Draw(DrawingContext ctx) =>
        _table.DrawRows(ctx, _colWidths, _rowHeights, _rowIndices);
}
