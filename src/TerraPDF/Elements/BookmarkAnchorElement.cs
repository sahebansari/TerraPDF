using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>
/// Marks its child content as a bookmark (PDF outline) destination.  The page
/// number and vertical position are recorded automatically when the element is
/// drawn, so callers never have to predict physical page numbers.
/// <para>
/// Deliberately not layout-transparent (no <see cref="Element.PassthroughChild"/>):
/// the pagination splitter bypasses wrapper <c>Draw</c> calls, which would skip
/// the recording — so wrap individual items, not a whole paginating column.
/// </para>
/// </summary>
internal sealed class BookmarkAnchorElement : Element
{
    /// <summary>Bookmark title shown in the viewer's outline pane.</summary>
    internal required string Title { get; init; }

    /// <summary>Optional parent bookmark title for hierarchical nesting.</summary>
    internal string? ParentTitle { get; init; }

    /// <summary>The inner container slot — attach child content here.</summary>
    internal Container Inner { get; } = new();

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint) =>
        Inner.Measure(w, h, defaultStyle, totalPagesHint);

    internal override void Draw(DrawingContext ctx)
    {
        ctx.BookmarkRecorder?.Invoke(Title, ParentTitle, ctx.PageNumber, ctx.Y);
        Inner.Draw(ctx);
    }
}
