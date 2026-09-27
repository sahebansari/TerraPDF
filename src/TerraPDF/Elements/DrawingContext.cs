using TerraPDF.Drawing;
using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Rendering context passed down through the element tree.</summary>
/// <remarks>
/// A struct: every decorator and cell derives a repositioned context with <see cref="At"/>,
/// and as a class that was one heap allocation per element per page drawn.
/// </remarks>
internal readonly struct DrawingContext
{
    // Declared so `new DrawingContext { … }` applies the property defaults below.
    public DrawingContext() { }

    internal required PdfPage Page        { get; init; }
    internal double X                     { get; init; }
    internal double Y                     { get; init; }
    internal double Width                 { get; init; }
    internal double Height                { get; init; }
    internal TextStyle DefaultTextStyle   { get; init; } = TextStyle.Default;
    internal int PageNumber               { get; init; } = 1;
    internal int TotalPages               { get; init; } = 1;
    internal Action<HeadingElement, int, double>? HeadingRecorder { get; init; }

    /// <summary>
    /// Invoked by <see cref="BookmarkAnchorElement"/> during the final render with
    /// (title, parentTitle, pageNumber, topY) so anchors resolve their own positions.
    /// </summary>
    internal Action<string, string?, int, double>? BookmarkRecorder { get; init; }

    /// <summary>Returns a context repositioned to the given bounds.</summary>
    internal DrawingContext At(double x, double y, double width, double height) => new()
    {
        Page             = Page,
        X                = x,
        Y                = y,
        Width            = width,
        Height           = height,
        DefaultTextStyle = DefaultTextStyle,
        PageNumber       = PageNumber,
        TotalPages       = TotalPages,
        HeadingRecorder  = HeadingRecorder,
        BookmarkRecorder = BookmarkRecorder,
    };
}
