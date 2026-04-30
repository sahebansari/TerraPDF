using TerraPDF.Helpers;
using TerraPDF.Infra;

namespace TerraPDF.Elements;

/// <summary>
/// Transparent single-child container - the concrete type behind <see cref="IContainer"/>.
/// Extension methods create elements, attach them as the child, and return the appropriate slot
/// for further chaining.
/// </summary>
internal sealed class Container : Element, IContainer
{
    internal Element? Child { get; set; }

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null) =>
        Child?.Measure(w, h, defaultStyle) ?? new ElementSize(0, 0);

    internal override void Draw(DrawingContext ctx) => Child?.Draw(ctx);
}
