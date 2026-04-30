using TerraPDF.Helpers;

namespace TerraPDF.Elements;

internal enum HorizontalAlignment { Left, Center, Right }
internal enum VerticalAlignment   { Top,  Middle, Bottom }

/// <summary>Positions its child within the available bounds according to the specified alignment.</summary>
internal sealed class Alignment : Element
{
    internal HorizontalAlignment Horizontal { get; set; } = HorizontalAlignment.Left;
    internal VerticalAlignment   Vertical   { get; set; } = VerticalAlignment.Top;

    /// <summary>The inner container slot - attach child content here.</summary>
    internal Container Inner { get; } = new();

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null)
    {
        // Report the child's natural content width so that Row auto-sized slots
        // receive the correct intrinsic size.  Parents that need the full available
        // width (Column, Row relative items) always supply their own width at draw
        // time via ctx.Width, so alignment offsets remain correct.
        var childSize = Inner.Measure(w, h, defaultStyle);
        return new ElementSize(childSize.Width, childSize.Height);
    }

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        var childSize = Inner.Measure(ctx.Width, ctx.Height, ctx.DefaultTextStyle);

        double x = Horizontal switch
        {
            HorizontalAlignment.Center => ctx.X + (ctx.Width  - childSize.Width)  / 2,
            HorizontalAlignment.Right  => ctx.X +  ctx.Width  - childSize.Width,
            _                          => ctx.X,
        };

        double y = Vertical switch
        {
            VerticalAlignment.Middle => ctx.Y + (ctx.Height - childSize.Height) / 2,
            VerticalAlignment.Bottom => ctx.Y +  ctx.Height - childSize.Height,
            _                        => ctx.Y,
        };

        // Shift the draw origin to the aligned position and give the child exactly
        // its measured width, so text-level alignment inside the child has the right boundary.
        Inner.Draw(ctx.At(x, y, childSize.Width, childSize.Height));
    }
}
