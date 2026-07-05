using TerraPDF.Elements;

namespace TerraPDF.Core;

/// <summary>
/// One placed element on one physical page, in absolute page coordinates.
/// When <see cref="DecorationOnly"/> is set the renderer calls
/// <see cref="Element.DrawDecoration"/> instead of <see cref="Element.Draw"/> —
/// used to repeat decorator chrome (background fill, border stroke) on every
/// page of a column that was split across pages.
/// </summary>
internal readonly record struct PlacedItem(
    Element Element,
    double X, double Y, double W, double H,
    bool DecorationOnly = false);

/// <summary>
/// One physical PDF page produced by laying out a <see cref="PageDescriptor"/>.
/// Produced by <see cref="DocumentComposer"/>'s layout pass and consumed by both
/// page counting (<c>fragments.Count</c>) and rendering, so the two can never
/// disagree.
/// </summary>
internal sealed class PageFragment
{
    /// <summary>True for the first page of its descriptor (header-on-first-page logic).</summary>
    internal bool IsFirstOfDescriptor { get; init; }

    /// <summary>Placed content items in draw order (decorations first, so chrome sits under content).</summary>
    internal List<PlacedItem> Items { get; } = [];
}
