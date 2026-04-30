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
}
