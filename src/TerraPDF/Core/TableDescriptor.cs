using TerraPDF.Elements;
using TerraPDF.Infra;

namespace TerraPDF.Core;

/// <summary>
/// Fluent API for configuring a <see cref="Table"/>.
/// Returned by <c>IContainer.Table(…)</c>.
/// </summary>
public sealed class TableDescriptor
{
    private readonly Table _element;
    private int _currentRow;

    internal TableDescriptor(Table element) => _element = element;

    // -- Column definitions ----------------------------------------

    /// <summary>Defines all columns at once via a builder.</summary>
    public TableDescriptor ColumnsDefinition(Action<ColumnsDefinitionDescriptor> define)
    {
        var builder = new ColumnsDefinitionDescriptor(_element);
        define(builder);
        return this;
    }

    // -- Rows ------------------------------------------------------

    /// <summary>
    /// Appends a header row to the table. Header rows are repeated at the top of
    /// every continuation page when the table is split across pages.
    /// Must be called before any <see cref="Row"/> calls.
    /// </summary>
    public TableDescriptor HeaderRow(Action<TableRowDescriptor> configure)
    {
        _element.HeaderRowCount++;
        return Row(configure);
    }

    /// <summary>
    /// Appends a new row to the table and configures its cells via <paramref name="configure"/>.
    /// Each call to <see cref="TableRowDescriptor.Cell"/> inside the action adds the next cell
    /// (left-to-right) and returns an <see cref="IContainer"/> that supports all decoration
    /// methods: <c>Background</c>, <c>Padding</c>, <c>AlignCenter</c>, <c>AlignRight</c>,
    /// <c>Border</c>, <c>Text</c>, and more.
    /// </summary>
    public TableDescriptor Row(Action<TableRowDescriptor> configure)
    {
        _currentRow++;
        var row = new TableRowDescriptor(_element, _currentRow);
        configure(row);
        return this;
    }

    /// <summary>Returns the underlying element for advanced configuration.</summary>
    internal Table Element => _element;
}

/// <summary>Fluent API for defining table columns.</summary>
public sealed class ColumnsDefinitionDescriptor
{
    private readonly Table _table;

    internal ColumnsDefinitionDescriptor(Table table) => _table = table;

    /// <summary>Adds a relative column (shares available width proportionally).</summary>
    public ColumnsDefinitionDescriptor RelativeColumn(double weight = 1)
    {
        _table.Columns.Add(new TableColumn
        {
            Type           = TableColumnType.Relative,
            RelativeWeight = weight,
        });
        return this;
    }

    /// <summary>Adds a fixed-width column in PDF points.</summary>
    public ColumnsDefinitionDescriptor ConstantColumn(double widthPt)
    {
        _table.Columns.Add(new TableColumn
        {
            Type          = TableColumnType.Constant,
            ConstantWidth = widthPt,
        });
        return this;
    }
}
