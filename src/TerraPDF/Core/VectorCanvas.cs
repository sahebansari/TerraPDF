using TerraPDF.Helpers;

namespace TerraPDF.Core;

/// <summary>
/// Fluent vector-graphics canvas.  Returned by <c>IContainer.Canvas(…)</c>.
/// Coordinates are in PDF points with a <b>top-left origin</b> relative to the
/// element's bounding box, matching every other TerraPDF API.
///
/// <para>
/// Supported primitives:
/// <list type="bullet">
///   <item>Lines, polylines</item>
///   <item>Rectangles (filled / stroked / both)</item>
///   <item>Circles and ellipses</item>
///   <item>Rounded rectangles</item>
///   <item>Arbitrary paths with <c>MoveTo / LineTo / CurveTo / Close</c></item>
///   <item>Filled polygons</item>
///   <item>Grid helpers</item>
/// </list>
/// </para>
/// </summary>
public sealed class VectorCanvas
{
    // -----------------------------------------------------------------------
    //  Internal drawing command records
    // -----------------------------------------------------------------------

    // Each Draw* method appends one of these to _commands.
    // CanvasElement.Draw() replays them all onto PdfPage in order.

    internal abstract record DrawCommand;

    internal sealed record DrawLineCmd(
        double X1, double Y1, double X2, double Y2,
        string HexColor, double LineWidth) : DrawCommand;

    internal sealed record DrawRectCmd(
        double X, double Y, double W, double H,
        string? FillHex, string? StrokeHex, double LineWidth) : DrawCommand;

    internal sealed record DrawRoundedRectCmd(
        double X, double Y, double W, double H, double Radius,
        string? FillHex, string? StrokeHex, double LineWidth) : DrawCommand;

    internal sealed record DrawEllipseCmd(
        double Cx, double Cy, double Rx, double Ry,
        string? FillHex, string? StrokeHex, double LineWidth) : DrawCommand;

    internal sealed record DrawPathCmd(PathDescriptor Path) : DrawCommand;

    // -----------------------------------------------------------------------
    //  State
    // -----------------------------------------------------------------------

    internal readonly List<DrawCommand> Commands = [];

    // The bounding box is injected at render time by CanvasElement.
    internal double AllocatedWidth  { get; set; }
    internal double AllocatedHeight { get; set; }

    // -----------------------------------------------------------------------
    //  Line
    // -----------------------------------------------------------------------

    /// <summary>Draws a straight line from (<paramref name="x1"/>, <paramref name="y1"/>)
    /// to (<paramref name="x2"/>, <paramref name="y2"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative.</exception>
    public VectorCanvas Line(double x1, double y1, double x2, double y2,
        string hexColor = "#000000", double lineWidth = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        Commands.Add(new DrawLineCmd(x1, y1, x2, y2, hexColor, lineWidth));
        return this;
    }

    // -----------------------------------------------------------------------
    //  Rectangle
    // -----------------------------------------------------------------------

    /// <summary>Draws a filled rectangle.</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    public VectorCanvas FillRect(double x, double y, double width, double height,
        string hexColor = "#000000")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        Commands.Add(new DrawRectCmd(x, y, width, height, hexColor, null, 0));
        return this;
    }

    /// <summary>Draws a stroked (outline-only) rectangle.</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative.</exception>
    public VectorCanvas StrokeRect(double x, double y, double width, double height,
        string hexColor = "#000000", double lineWidth = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        Commands.Add(new DrawRectCmd(x, y, width, height, null, hexColor, lineWidth));
        return this;
    }

    /// <summary>Draws a filled and stroked rectangle.</summary>
    /// <exception cref="ArgumentException">Either color argument is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative.</exception>
    public VectorCanvas DrawRect(double x, double y, double width, double height,
        string fillHex = "#FFFFFF", string strokeHex = "#000000", double lineWidth = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        Commands.Add(new DrawRectCmd(x, y, width, height, fillHex, strokeHex, lineWidth));
        return this;
    }

    // -----------------------------------------------------------------------
    //  Rounded rectangle
    // -----------------------------------------------------------------------

    /// <summary>Draws a filled rounded rectangle.</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is zero or negative.</exception>
    public VectorCanvas FillRoundedRect(double x, double y, double width, double height,
        double radius, string hexColor = "#000000")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        Commands.Add(new DrawRoundedRectCmd(x, y, width, height, radius, hexColor, null, 0));
        return this;
    }

    /// <summary>Draws a stroked rounded rectangle.</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative.</exception>
    public VectorCanvas StrokeRoundedRect(double x, double y, double width, double height,
        double radius, string hexColor = "#000000", double lineWidth = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        Commands.Add(new DrawRoundedRectCmd(x, y, width, height, radius, null, hexColor, lineWidth));
        return this;
    }

    /// <summary>Draws a filled and stroked rounded rectangle.</summary>
    /// <exception cref="ArgumentException">Either color argument is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative.</exception>
    public VectorCanvas DrawRoundedRect(double x, double y, double width, double height,
        double radius, string fillHex = "#FFFFFF", string strokeHex = "#000000", double lineWidth = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        Commands.Add(new DrawRoundedRectCmd(x, y, width, height, radius, fillHex, strokeHex, lineWidth));
        return this;
    }

    // -----------------------------------------------------------------------
    //  Circle / Ellipse
    // -----------------------------------------------------------------------

    /// <summary>Draws a filled circle centred at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is zero or negative.</exception>
    public VectorCanvas FillCircle(double cx, double cy, double radius, string hexColor = "#000000")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        Commands.Add(new DrawEllipseCmd(cx, cy, radius, radius, hexColor, null, 0));
        return this;
    }

    /// <summary>Draws a stroked circle centred at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative.</exception>
    public VectorCanvas StrokeCircle(double cx, double cy, double radius,
        string hexColor = "#000000", double lineWidth = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        Commands.Add(new DrawEllipseCmd(cx, cy, radius, radius, null, hexColor, lineWidth));
        return this;
    }

    /// <summary>Draws a filled and stroked circle.</summary>
    /// <exception cref="ArgumentException">Either color argument is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative.</exception>
    public VectorCanvas DrawCircle(double cx, double cy, double radius,
        string fillHex = "#FFFFFF", string strokeHex = "#000000", double lineWidth = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        Commands.Add(new DrawEllipseCmd(cx, cy, radius, radius, fillHex, strokeHex, lineWidth));
        return this;
    }

    /// <summary>Draws a filled ellipse centred at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either radius is zero or negative.</exception>
    public VectorCanvas FillEllipse(double cx, double cy, double rx, double ry,
        string hexColor = "#000000")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ry);
        Commands.Add(new DrawEllipseCmd(cx, cy, rx, ry, hexColor, null, 0));
        return this;
    }

    /// <summary>Draws a stroked ellipse centred at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either radius or <paramref name="lineWidth"/> is zero or negative.</exception>
    public VectorCanvas StrokeEllipse(double cx, double cy, double rx, double ry,
        string hexColor = "#000000", double lineWidth = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ry);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        Commands.Add(new DrawEllipseCmd(cx, cy, rx, ry, null, hexColor, lineWidth));
        return this;
    }

    /// <summary>Draws a filled and stroked ellipse.</summary>
    /// <exception cref="ArgumentException">Either color argument is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Any radius or <paramref name="lineWidth"/> is zero or negative.</exception>
    public VectorCanvas DrawEllipse(double cx, double cy, double rx, double ry,
        string fillHex = "#FFFFFF", string strokeHex = "#000000", double lineWidth = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ry);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        Commands.Add(new DrawEllipseCmd(cx, cy, rx, ry, fillHex, strokeHex, lineWidth));
        return this;
    }

    // -----------------------------------------------------------------------
    //  Arbitrary path
    // -----------------------------------------------------------------------

    /// <summary>
    /// Adds an arbitrary vector path built via the <see cref="PathDescriptor"/> fluent API.
    /// Use <c>MoveTo</c>, <c>LineTo</c>, <c>CurveTo</c>, convenience shapes,
    /// and <c>Fill</c> / <c>Stroke</c> paint setters on the descriptor.
    /// </summary>
    /// <example>
    /// <code>
    /// canvas.Path(p => p
    ///     .MoveTo(10, 10)
    ///     .LineTo(90, 10)
    ///     .LineTo(50, 80)
    ///     .Close()
    ///     .Fill(Color.Blue.Medium)
    ///     .Stroke(Color.Blue.Darken2, 1.5));
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <c>null</c>.</exception>
    public VectorCanvas Path(Action<PathDescriptor> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var pd = new PathDescriptor();
        configure(pd);
        Commands.Add(new DrawPathCmd(pd));
        return this;
    }

    // -----------------------------------------------------------------------
    //  Grid helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Draws a rectangular grid of vertical and horizontal lines that fills the canvas area.
    /// </summary>
    /// <param name="cellWidth">Width of each cell in PDF points.</param>
    /// <param name="cellHeight">Height of each cell in PDF points. When <c>null</c> uses <paramref name="cellWidth"/> (square cells).</param>
    /// <param name="hexColor">Line colour. Defaults to light grey.</param>
    /// <param name="lineWidth">Stroke width. Defaults to 0.5 pt.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cellWidth"/> is zero or negative.</exception>
    public VectorCanvas Grid(double cellWidth, double? cellHeight = null,
        string hexColor = "#CCCCCC", double lineWidth = 0.5)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellWidth);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);

        double ch = cellHeight ?? cellWidth;

        // Vertical lines
        double x = cellWidth;
        while (x < AllocatedWidth)
        {
            Line(x, 0, x, AllocatedHeight, hexColor, lineWidth);
            x += cellWidth;
        }

        // Horizontal lines
        double y = ch;
        while (y < AllocatedHeight)
        {
            Line(0, y, AllocatedWidth, y, hexColor, lineWidth);
            y += ch;
        }

        return this;
    }
}
