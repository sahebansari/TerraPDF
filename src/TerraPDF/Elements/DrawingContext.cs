using TerraPDF.Drawing;
using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Rendering context passed down through the element tree.</summary>
internal sealed class DrawingContext
{
    internal required PdfPage Page        { get; init; }
    internal double X                     { get; init; }
    internal double Y                     { get; init; }
    internal double Width                 { get; init; }
    internal double Height                { get; init; }
    internal TextStyle DefaultTextStyle   { get; init; } = TextStyle.Default;
    internal int PageNumber               { get; init; } = 1;
    internal int TotalPages               { get; init; } = 1;

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
    };
}
