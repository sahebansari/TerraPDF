using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Abstract base for all layout and content elements.</summary>
internal abstract class Element
{
    /// <summary>
    /// Default placeholder used to measure page-number spans before the real
    /// page count is known (two digits, the common case).
    /// </summary>
    internal const int DefaultTotalPagesHint = 99;

    /// <summary>
    /// Returns the natural (desired) size of the element given the available bounds.
    /// <paramref name="totalPagesHint"/> is the value used to size
    /// <c>CurrentPageNumber()</c>/<c>TotalPages()</c> spans during measurement —
    /// container elements must forward it to their children so that reserved
    /// header/footer heights match what is drawn (the actual page number never
    /// has more digits than the total).
    /// </summary>
    internal abstract ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint);

    /// <summary>Renders the element within <paramref name="ctx"/>.</summary>
    internal abstract void Draw(DrawingContext ctx);

    /// <summary>
    /// Draws only this element's own visual (background fill, border stroke, …)
    /// without its children.  The pagination engine uses this to repeat decorator
    /// chrome on every page of a column that was split across pages.
    /// Default: no visual.
    /// </summary>
    internal virtual void DrawDecoration(DrawingContext ctx) { }

    /// <summary>
    /// True when <see cref="DrawDecoration"/> paints something.  Decorators that
    /// override <see cref="DrawDecoration"/> must also override this so the
    /// pagination engine emits their chrome on every page of a split column.
    /// </summary>
    internal virtual bool HasDecoration => false;

    /// <summary>
    /// The wrapped element when this element is layout-transparent for pagination
    /// (decorators such as Padding, Margin, Background, Border, Alignment), or
    /// <c>null</c> when the element is opaque (content, layouts).
    /// The pagination engine in <c>DocumentComposer</c> walks this chain to find
    /// the Column/Table to split across pages — every decorator MUST override it,
    /// otherwise content wrapped in that decorator silently stops paginating.
    /// Note: <c>Link</c>/<c>InternalLinkElement</c> deliberately do NOT override it;
    /// the splitter bypasses wrapper <c>Draw</c> calls, which would drop their annotations.
    /// </summary>
    internal virtual Element? PassthroughChild => null;

    /// <summary>
    /// Insets this passthrough decorator applies to its child's layout area
    /// (non-zero only for Padding and Margin).
    /// </summary>
    internal virtual (double Left, double Top, double Right, double Bottom) PassthroughInsets => default;
}

/// <summary>Natural (desired) size of an element.</summary>
internal readonly record struct ElementSize(double Width, double Height);