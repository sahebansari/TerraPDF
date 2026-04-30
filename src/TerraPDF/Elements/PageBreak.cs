using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>
/// A zero-size sentinel element that instructs the pagination engine to begin a
/// new PDF page immediately when it is encountered inside a <see cref="Column"/>.
/// <para>
/// If the page break falls at the very start of a page (nothing has been drawn
/// yet on that page) it is silently ignored — no blank page is emitted.
/// </para>
/// <para>
/// Place via the convenience helper <c>col.PageBreak()</c> on a
/// <see cref="TerraPDF.Core.ColumnDescriptor"/>, or via
/// <c>col.Item().PageBreak()</c> using the <see cref="TerraPDF.Core.ContainerExtensions"/>
/// extension method.  Decorators should not be applied to a page-break slot.
/// </para>
/// </summary>
internal sealed class PageBreak : Element
{
    /// <summary>A page-break occupies no space in the layout.</summary>
    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null) =>
        new(0, 0);

    /// <summary>A page-break renders nothing; the actual page transition is
    /// handled by <see cref="TerraPDF.Core.DocumentComposer"/>.</summary>
    internal override void Draw(DrawingContext ctx) { }
}
