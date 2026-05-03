using TerraPDF.Core;

namespace TerraPDF.Infra;

/// <summary>
/// Represents a container that holds page descriptors.
/// Passed to <see cref="IDocument.Compose"/> and to <c>Document.Create</c>.
/// </summary>
public interface IDocumentContainer
{
    /// <summary>Adds a page to the document with the specified layout.</summary>
    void Page(Action<PageDescriptor> configure);

    /// <summary>
    /// Adds a bookmark (outline entry) that points to the specified page.
    /// </summary>
    /// <param name="title">The bookmark title displayed in the PDF viewer's outline pane.</param>
    /// <param name="pageNumber">The 1-based page number this bookmark targets.</param>
    void Bookmark(string title, int pageNumber);

    /// <summary>
    /// Adds a bookmark with an optional vertical position on the target page.
    /// </summary>
    /// <param name="title">The bookmark title.</param>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="top">Optional Y coordinate (in points from the top of the page) to position the view.</param>
    void Bookmark(string title, int pageNumber, double top);

    /// <summary>
    /// Adds a child bookmark under an existing parent bookmark.
    /// </summary>
    /// <param name="title">The bookmark title.</param>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="parentTitle">The title of an existing bookmark to serve as parent.</param>
    void Bookmark(string title, int pageNumber, string parentTitle);

    /// <summary>
    /// Adds a child bookmark with an optional vertical position under an existing parent.
    /// </summary>
    /// <param name="title">The bookmark title.</param>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="parentTitle">The title of an existing bookmark to serve as parent.</param>
    /// <param name="top">Optional Y coordinate (in points from the top of the page).</param>
    void Bookmark(string title, int pageNumber, string parentTitle, double top);
}
