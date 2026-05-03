using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Wraps child content in a clickable internal link (GoTo) that navigates to a specific page and optional vertical position.</summary>
internal sealed class InternalLinkElement : Element
{
    internal int TargetPage { get; set; }          // 1-based page number
    internal double? TargetTop { get; set; }       // Y position from top of page (optional)
    internal Container Inner { get; } = new();

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null) =>
        Inner.Measure(w, h, defaultStyle);

    internal override void Draw(DrawingContext ctx)
    {
        var size = Inner.Measure(ctx.Width, ctx.Height, ctx.DefaultTextStyle);
        ctx.Page.AddInternalLinkAnnotation(ctx.X, ctx.Y, size.Width, size.Height, TargetPage, TargetTop);
        Inner.Draw(ctx);
    }
}
