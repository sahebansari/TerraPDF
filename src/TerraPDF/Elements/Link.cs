using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>
/// Wraps its child content in a clickable PDF URI annotation.
/// When the generated PDF is opened in a viewer, clicking anywhere over the
/// rendered child area navigates the browser or PDF viewer to <see cref="Url"/>.
/// </summary>
internal sealed class Link : Element
{
    /// <summary>The destination URI (e.g. "https://example.com").</summary>
    internal required string Url { get; init; }

    /// <summary>The inner container slot — attach child content here.</summary>
    internal Container Inner { get; } = new();

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null) =>
        Inner.Measure(w, h, defaultStyle);

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        // Measure the child to get the exact rendered size for the annotation rect.
        var size = Inner.Measure(ctx.Width, ctx.Height, ctx.DefaultTextStyle);

        // Register the clickable annotation on the current page.
        ctx.Page.AddLinkAnnotation(ctx.X, ctx.Y, size.Width, size.Height, Url);

        // Draw the child content on top.
        Inner.Draw(ctx);
    }
}
