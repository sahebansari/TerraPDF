using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Adds padding around its inner content.</summary>
internal sealed class Padding : Element
{
    internal double Top    { get; set; }
    internal double Right  { get; set; }
    internal double Bottom { get; set; }
    internal double Left   { get; set; }

    /// <summary>The inner container slot - attach child content here.</summary>
    internal Container Inner { get; } = new();

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null)
    {
        double iw = Math.Max(0, w - Left - Right);
        double ih = Math.Max(0, h - Top  - Bottom);
        var    sz = Inner.Measure(iw, ih, defaultStyle);
        return new ElementSize(sz.Width + Left + Right, sz.Height + Top + Bottom);
    }

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx) =>
        Inner.Draw(ctx.At(
            ctx.X + Left,
            ctx.Y + Top,
            Math.Max(0, ctx.Width  - Left - Right),
            Math.Max(0, ctx.Height - Top  - Bottom)));
}
