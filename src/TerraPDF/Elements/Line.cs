using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Draws a horizontal or vertical rule line.</summary>
internal sealed class Line : Element
{
    internal bool      Vertical  { get; set; }
    internal PdfColor  Color     { get; set; } = PdfColor.FromHex("#000000");
    internal double    LineWidth { get; set; } = 1;

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null) =>
        Vertical ? new ElementSize(LineWidth, h) : new ElementSize(w, LineWidth);

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        if (Vertical)
        {
            double cx = ctx.X + ctx.Width / 2;
            ctx.Page.AddLine(cx, ctx.Y, cx, ctx.Y + ctx.Height, Color, LineWidth);
        }
        else
        {
            double cy = ctx.Y + ctx.Height / 2;
            ctx.Page.AddLine(ctx.X, cy, ctx.X + ctx.Width, cy, Color, LineWidth);
        }
    }
}
