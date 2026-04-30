using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Abstract base for all layout and content elements.</summary>
internal abstract class Element
{
    /// <summary>Returns the natural (desired) size of the element given the available bounds.</summary>
    internal abstract ElementSize Measure(double w, double h, TextStyle? defaultStyle = null);

    /// <summary>Renders the element within <paramref name="ctx"/>.</summary>
    internal abstract void Draw(DrawingContext ctx);
}

/// <summary>Natural (desired) size of an element.</summary>
internal readonly record struct ElementSize(double Width, double Height);