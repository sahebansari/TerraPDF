using System.Collections.Generic;

namespace TerraPDF.Core;

/// <summary>
/// Internal representation of a bookmark (PDF outline) node.
/// Used to build the hierarchical outline tree that is written to the PDF.
/// </summary>
internal sealed class BookmarkInfo
{
    /// <summary>The bookmark title shown in the viewer's outline pane.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The 1-based logical page number this bookmark targets.</summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Optional Y coordinate (in points from the top of the page) where the view
    /// should be positioned when the bookmark is activated. If null, a /Fit destination is used.
    /// </summary>
    public double? Top { get; set; }

    /// <summary>Parent bookmark (null for top-level entries).</summary>
    public BookmarkInfo? Parent { get; set; }

    /// <summary>Child bookmarks (nested under this entry).</summary>
    public List<BookmarkInfo> Children { get; } = new();

    /// <summary>Next sibling bookmark (for linked traversal).</summary>
    public BookmarkInfo? Next { get; set; }

    /// <summary>Previous sibling bookmark.</summary>
    public BookmarkInfo? Prev { get; set; }
}
