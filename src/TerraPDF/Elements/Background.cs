using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Fills its bounds with a solid color before rendering its child.</summary>
internal sealed class Background : Element
{
    internal PdfColor Color { get; set; }

    /// <summary>The inner container slot - attach child content here.</summary>
    internal Container Inner { get; } = new();

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null) => Inner.Measure(w, h, defaultStyle);

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        ctx.Page.AddFilledRect(ctx.X, ctx.Y, ctx.Width, ctx.Height, Color);
        Inner.Draw(ctx);
    }
}
