using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Draws a rectangular border around its child.</summary>
internal sealed class Border : Element
{
    internal double    LineWidth { get; set; } = 1;
    internal PdfColor  Color     { get; set; } = PdfColor.FromHex("#000000");

    /// <summary>The inner container slot - attach child content here.</summary>
    internal Container Inner { get; } = new();

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null) => Inner.Measure(w, h, defaultStyle);

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        ctx.Page.AddStrokedRect(ctx.X, ctx.Y, ctx.Width, ctx.Height, Color, LineWidth);
        Inner.Draw(ctx);
    }
}
