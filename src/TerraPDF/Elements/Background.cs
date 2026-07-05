using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Fills its bounds with a solid color before rendering its child.</summary>
internal sealed class Background : Element
{
    internal PdfColor Color { get; set; }

    /// <summary>The inner container slot - attach child content here.</summary>
    internal Container Inner { get; } = new();

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint) =>
        Inner.Measure(w, h, defaultStyle, totalPagesHint);

    // -- Draw ------------------------------------------------------

    internal override void DrawDecoration(DrawingContext ctx) =>
        ctx.Page.AddFilledRect(ctx.X, ctx.Y, ctx.Width, ctx.Height, Color);

    internal override void Draw(DrawingContext ctx)
    {
        DrawDecoration(ctx);
        Inner.Draw(ctx);
    }

    internal override Element? PassthroughChild => Inner;
    internal override bool HasDecoration => true;
}
