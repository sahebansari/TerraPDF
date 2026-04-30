using TerraPDF.Elements;
using TerraPDF.Infra;

namespace TerraPDF.Core;

/// <summary>
/// Fluent API for adding cells within a table row.
/// Returned by <c>TableDescriptor.Row(â€¦)</c>.
/// </summary>
public sealed class TableRowDescriptor
{
    private readonly Table _element;
    private readonly int _row;
    private int _currentCol;

    internal TableRowDescriptor(Table element, int row)
    {
        _element = element;
        _row     = row;
    }

    /// <summary>
    /// Adds the next cell in this row and returns its container for content composition.
    /// The container supports all decoration methods: <c>Background</c>, <c>Padding</c>,
    /// <c>AlignCenter</c>, <c>AlignRight</c>, <c>Border</c>, <c>Text</c>, etc.
    /// </summary>
    public IContainer Cell(int columnSpan = 1, int rowSpan = 1)
    {
        _currentCol++;
        return _element.AddCell(_currentCol, _row, columnSpan, rowSpan).Slot;
    }
}
