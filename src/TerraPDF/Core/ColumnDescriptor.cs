using TerraPDF.Elements;
using TerraPDF.Infra;

namespace TerraPDF.Core;

/// <summary>
/// Fluent API for configuring a <see cref="Column"/>.
/// Returned by <c>IContainer.Column(â€¦)</c>.
/// </summary>
public sealed class ColumnDescriptor
{
    private readonly Column _element;
    private HorizontalAlignment _alignment = HorizontalAlignment.Left;

    internal ColumnDescriptor(Column element) => _element = element;

    /// <summary>Sets the vertical gap between items in PDF points.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public ColumnDescriptor Spacing(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        _element.Spacing = value;
        return this;
    }

    /// <summary>Aligns all subsequent items to the left (default).</summary>
    public ColumnDescriptor AlignItemsLeft()
    {
        _alignment = HorizontalAlignment.Left;
        return this;
    }

    /// <summary>Horizontally centres all subsequent items.</summary>
    public ColumnDescriptor AlignItemsCenter()
    {
        _alignment = HorizontalAlignment.Center;
        return this;
    }

    /// <summary>Aligns all subsequent items to the right.</summary>
    public ColumnDescriptor AlignItemsRight()
    {
        _alignment = HorizontalAlignment.Right;
        return this;
    }

    /// <summary>
    /// Adds a new item slot to the column and returns it for content composition.
    /// The item is horizontally aligned according to the last <c>AlignItems*</c> call.
    /// </summary>
    public IContainer Item()
    {
        if (_alignment == HorizontalAlignment.Left)
            return _element.AddItem();

        // Non-left alignment: wrap the new column slot inside an Alignment element
        // so the content is offset to the correct horizontal position within the slot.
        var wrapper = new Alignment { Horizontal = _alignment };
        var outer   = _element.AddItem();
        outer.Child = wrapper;
        return wrapper.Inner;
    }

    /// <summary>
    /// Inserts an explicit page-break marker into the column.
    /// The pagination engine will start a new PDF page when it reaches this marker.
    /// If the marker falls at the very start of a page it is silently skipped
    /// so no blank page is emitted.
    /// </summary>
    public ColumnDescriptor PageBreak()
    {
        var slot = _element.AddItem();
        slot.Child = new PageBreak();
        return this;
    }
}
