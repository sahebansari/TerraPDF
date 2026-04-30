using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Stacks child items vertically (top -> bottom) with optional spacing.</summary>
internal sealed class Column : Element
{
    internal List<Container> Items   { get; } = [];
    internal double              Spacing { get; set; }

    internal Container AddItem()
    {
        var slot = new Container();
        Items.Add(slot);
        return slot;
    }

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null)
    {
        double totalH   = 0;
        double maxWidth = 0;   // track natural content width for Row auto-sizing
        for (int i = 0; i < Items.Count; i++)
        {
            var sz = Items[i].Measure(w, Math.Max(0, h - totalH), defaultStyle);
            totalH += sz.Height;
            if (sz.Width > maxWidth) maxWidth = sz.Width;
            if (i < Items.Count - 1) totalH += Spacing;
        }
        // Return the widest item's natural width so that Row auto-sized slots receive
        // the correct intrinsic size.  Parents that need the full available width
        // (Column.Draw, Row relative items) always supply their own width at draw time
        // via ctx.Width, so nothing is clipped.
        return new ElementSize(maxWidth, totalH);
    }

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        double curY = ctx.Y;
        for (int i = 0; i < Items.Count; i++)
        {
            var    item = Items[i];
            // Remaining vertical space below the current cursor position
            double rem  = Math.Max(0, ctx.Height - (curY - ctx.Y));
            // Use ctx.DefaultTextStyle so item heights are consistent with the measure pass
            var    sz   = item.Measure(ctx.Width, rem, ctx.DefaultTextStyle);
            item.Draw(ctx.At(ctx.X, curY, ctx.Width, sz.Height));
            curY += sz.Height;
            if (i < Items.Count - 1) curY += Spacing;
        }
    }
}
