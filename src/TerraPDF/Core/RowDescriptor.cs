using TerraPDF.Elements;
using TerraPDF.Infra;

namespace TerraPDF.Core;

/// <summary>
/// Fluent API for configuring a <see cref="Row"/>.
/// Returned by <c>IContainer.Row(â€¦)</c>.
/// </summary>
public sealed class RowDescriptor
{
    private readonly Row _element;

    internal RowDescriptor(Row element) => _element = element;

    /// <summary>Sets the horizontal gap between items in PDF points.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public RowDescriptor Spacing(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        _element.Spacing = value;
        return this;
    }

    /// <summary>
    /// Adds an auto-sized item (takes natural content width).
    /// </summary>
    public IContainer AutoItem()
    {
        var item = _element.AddItem(RowItemType.Auto);
        return item.Slot;
    }

    /// <summary>
    /// Adds a relative item that shares remaining space proportionally.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> is zero or negative.</exception>
    public IContainer RelativeItem(double weight = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(weight);
        var item = _element.AddItem(RowItemType.Relative, relativeWeight: weight);
        return item.Slot;
    }

    /// <summary>
    /// Adds a fixed-width item (in PDF points).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="widthPt"/> is zero or negative.</exception>
    public IContainer ConstantItem(double widthPt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthPt);
        var item = _element.AddItem(RowItemType.ConstantWidth, constantWidth: widthPt);
        return item.Slot;
    }
}
