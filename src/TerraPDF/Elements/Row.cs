using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Item sizing policy used by <see cref="Row"/>.</summary>
internal enum RowItemType { Auto, Relative, ConstantWidth }

/// <summary>One slot in a <see cref="Row"/>.</summary>
internal sealed class RowItem
{
    internal Container  Slot           { get; } = new();
    internal RowItemType    Type           { get; set; } = RowItemType.Auto;
    /// <summary>Relative weight (for <see cref="RowItemType.Relative"/> items).</summary>
    internal double         RelativeWeight { get; set; } = 1;
    /// <summary>Fixed width in points (for <see cref="RowItemType.ConstantWidth"/> items).</summary>
    internal double         ConstantWidth  { get; set; }
}

/// <summary>Arranges child items horizontally (left -> right) with optional spacing.</summary>
internal sealed class Row : Element
{
    internal List<RowItem> Items   { get; } = [];
    internal double        Spacing { get; set; }

    internal RowItem AddItem(RowItemType type = RowItemType.Auto,
                             double relativeWeight = 1,
                             double constantWidth  = 0)
    {
        var item = new RowItem { Type = type, RelativeWeight = relativeWeight, ConstantWidth = constantWidth };
        Items.Add(item);
        return item;
    }

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null)
    {
        double[] widths = CalculateWidths(w, defaultStyle);
        double maxH = 0;
        for (int i = 0; i < Items.Count; i++)
        {
            var sz = Items[i].Slot.Measure(widths[i], h, defaultStyle);
            if (sz.Height > maxH) maxH = sz.Height;
        }
        return new ElementSize(w, maxH);
    }

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        double[] widths = CalculateWidths(ctx.Width, ctx.DefaultTextStyle);
        double curX = ctx.X;
        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            item.Slot.Draw(ctx.At(curX, ctx.Y, widths[i], ctx.Height));
            curX += widths[i];
            if (i < Items.Count - 1) curX += Spacing;
        }
    }

    // -- Helpers ---------------------------------------------------

    private double[] CalculateWidths(double available, TextStyle? defaultStyle = null)
    {
        double totalSpacing = Spacing * Math.Max(0, Items.Count - 1);
        double remaining    = available - totalSpacing;

        // Pass 1: measure constant-width and auto-sized items
        var widths = new double[Items.Count];
        double autoTotal    = 0;
        double constTotal   = 0;
        double relWeightSum = 0;

        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            if (item.Type == RowItemType.ConstantWidth)
            {
                widths[i]  = item.ConstantWidth;
                constTotal += item.ConstantWidth;
            }
            else if (item.Type == RowItemType.Auto)
            {
                var sz    = item.Slot.Measure(remaining, double.MaxValue, defaultStyle);
                widths[i] = sz.Width;
                autoTotal += sz.Width;
            }
            else // Relative — weight summed now, width assigned in pass 2
            {
                relWeightSum += item.RelativeWeight;
            }
        }

        // Pass 2: distribute leftover space among relative items by weight
        double relSpace = Math.Max(0, remaining - autoTotal - constTotal);

        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Type == RowItemType.Relative)
                widths[i] = relWeightSum > 0
                    ? relSpace * (Items[i].RelativeWeight / relWeightSum)
                    : 0;
        }

        return widths;
    }
}
