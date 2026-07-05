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

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint) =>
        Inner.Measure(w, h, defaultStyle, totalPagesHint);

    // -- Draw ------------------------------------------------------

    internal override void DrawDecoration(DrawingContext ctx) =>
        ctx.Page.AddStrokedRect(ctx.X, ctx.Y, ctx.Width, ctx.Height, Color, LineWidth);

    internal override void Draw(DrawingContext ctx)
    {
        DrawDecoration(ctx);
        Inner.Draw(ctx);
    }

    internal override Element? PassthroughChild => Inner;
    internal override bool HasDecoration => true;
}
