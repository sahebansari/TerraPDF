using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>
/// Draws one or more individual edges (top, right, bottom, left) around its child.
/// Unlike <see cref="Border"/> which always draws all four sides, this element lets
/// callers choose exactly which sides are drawn and at what width and colour.
/// Typical use: table cell borders where only certain edges should be visible.
/// </summary>
internal sealed class PartialBorder : Element
{
    internal double   TopWidth    { get; set; }
    internal double   RightWidth  { get; set; }
    internal double   BottomWidth { get; set; }
    internal double   LeftWidth   { get; set; }

    internal PdfColor TopColor    { get; set; } = PdfColor.FromHex("#000000");
    internal PdfColor RightColor  { get; set; } = PdfColor.FromHex("#000000");
    internal PdfColor BottomColor { get; set; } = PdfColor.FromHex("#000000");
    internal PdfColor LeftColor   { get; set; } = PdfColor.FromHex("#000000");

    /// <summary>The inner container slot — attach child content here.</summary>
    internal Container Inner { get; } = new();

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null) =>
        Inner.Measure(w, h, defaultStyle);

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        double x = ctx.X;
        double y = ctx.Y;
        double w = ctx.Width;
        double h = ctx.Height;

        if (TopWidth    > 0) ctx.Page.AddLine(x,     y,     x + w, y,     TopColor,    TopWidth);
        if (BottomWidth > 0) ctx.Page.AddLine(x,     y + h, x + w, y + h, BottomColor, BottomWidth);
        if (LeftWidth   > 0) ctx.Page.AddLine(x,     y,     x,     y + h, LeftColor,   LeftWidth);
        if (RightWidth  > 0) ctx.Page.AddLine(x + w, y,     x + w, y + h, RightColor,  RightWidth);

        Inner.Draw(ctx);
    }
}
