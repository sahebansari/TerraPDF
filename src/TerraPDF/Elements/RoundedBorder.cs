using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>
/// Draws a rounded-corner border around its child content.
/// The corner radius is automatically clamped to half the shorter side of the
/// box so the arcs can never overlap.
/// </summary>
internal sealed class RoundedBorder : Element
{
    /// <summary>Corner radius in PDF points.</summary>
    internal double    Radius    { get; set; } = 8;
    internal double    LineWidth { get; set; } = 1;
    internal PdfColor  Color     { get; set; } = PdfColor.FromHex("#000000");

    /// <summary>Optional fill colour. When null the interior is transparent.</summary>
    internal PdfColor? FillColor { get; set; }

    /// <summary>The inner container slot — attach child content here.</summary>
    internal Container Inner { get; } = new();

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null) =>
        Inner.Measure(w, h, defaultStyle);

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        if (FillColor.HasValue)
        {
            ctx.Page.AddFilledAndStrokedRoundedRect(
                ctx.X, ctx.Y, ctx.Width, ctx.Height,
                Radius, FillColor.Value, Color, LineWidth);
        }
        else
        {
            ctx.Page.AddRoundedRect(
                ctx.X, ctx.Y, ctx.Width, ctx.Height,
                Radius, Color, LineWidth);
        }

        Inner.Draw(ctx);
    }
}
