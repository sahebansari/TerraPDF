using TerraPDF.Helpers;

namespace TerraPDF.Core;

/// <summary>
/// Fluent builder for a vector path used inside a <see cref="VectorCanvas"/>.
/// All coordinates are in PDF points relative to the top-left corner of the canvas element.
/// </summary>
public sealed class PathDescriptor
{
    // Commands accumulated by the fluent calls
    internal readonly List<VectorPathCommand> Commands = [];

    // Paint state
    internal PdfColor? FillColor   { get; private set; }
    internal PdfColor? StrokeColor { get; private set; }
    internal double    LineWidth   { get; private set; } = 1;
    internal bool      EvenOddFill { get; private set; }

    // ── Move / Line ─────────────────────────────────────────────────────────

    /// <summary>Moves the current point to (<paramref name="x"/>, <paramref name="y"/>) without drawing.</summary>
    public PathDescriptor MoveTo(double x, double y)
    {
        Commands.Add(new MoveToCmd(x, y));
        return this;
    }

    /// <summary>Draws a straight line from the current point to (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public PathDescriptor LineTo(double x, double y)
    {
        Commands.Add(new LineToCmd(x, y));
        return this;
    }

    // ── Cubic Bézier ────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a cubic Bézier curve to (<paramref name="x"/>, <paramref name="y"/>)
    /// using two control points.
    /// </summary>
    public PathDescriptor CurveTo(
        double cx1, double cy1,
        double cx2, double cy2,
        double x,   double y)
    {
        Commands.Add(new CurveToCmd(cx1, cy1, cx2, cy2, x, y));
        return this;
    }

    // ── Close ───────────────────────────────────────────────────────────────

    /// <summary>Closes the current subpath with a straight line back to its start point.</summary>
    public PathDescriptor Close()
    {
        Commands.Add(new ClosePathCmd());
        return this;
    }

    // ── Convenience shapes ──────────────────────────────────────────────────

    /// <summary>Appends a rectangle subpath (counter-clockwise, top-left origin).</summary>
    public PathDescriptor Rect(double x, double y, double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return MoveTo(x, y)
              .LineTo(x + width, y)
              .LineTo(x + width, y + height)
              .LineTo(x,         y + height)
              .Close();
    }

    /// <summary>
    /// Appends an ellipse subpath centred at (<paramref name="cx"/>, <paramref name="cy"/>)
    /// with the given horizontal and vertical radii.
    /// </summary>
    public PathDescriptor Ellipse(double cx, double cy, double rx, double ry)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ry);

        // Cubic Bézier approximation constant for a quarter-circle arc
        const double k = 0.5523;
        double kx = rx * k;
        double ky = ry * k;

        return MoveTo(cx + rx, cy)
              .CurveTo(cx + rx, cy - ky, cx + kx, cy - ry, cx,      cy - ry)
              .CurveTo(cx - kx, cy - ry, cx - rx, cy - ky, cx - rx, cy)
              .CurveTo(cx - rx, cy + ky, cx - kx, cy + ry, cx,      cy + ry)
              .CurveTo(cx + kx, cy + ry, cx + rx, cy + ky, cx + rx, cy)
              .Close();
    }

    /// <summary>
    /// Appends a circle subpath centred at (<paramref name="cx"/>, <paramref name="cy"/>)
    /// with the given <paramref name="radius"/>.
    /// </summary>
    public PathDescriptor Circle(double cx, double cy, double radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        return Ellipse(cx, cy, radius, radius);
    }

    /// <summary>
    /// Appends a polyline (open path) through all <paramref name="points"/> in order.
    /// Points are (x, y) pairs.
    /// </summary>
    /// <exception cref="ArgumentException">Fewer than two points supplied.</exception>
    public PathDescriptor Polyline(params (double X, double Y)[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 2)
            throw new ArgumentException("Polyline requires at least two points.", nameof(points));

        MoveTo(points[0].X, points[0].Y);
        for (int i = 1; i < points.Length; i++)
            LineTo(points[i].X, points[i].Y);
        return this;
    }

    /// <summary>
    /// Appends a closed polygon through all <paramref name="points"/> in order.
    /// The path is automatically closed after the last point.
    /// </summary>
    /// <exception cref="ArgumentException">Fewer than three points supplied.</exception>
    public PathDescriptor Polygon(params (double X, double Y)[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 3)
            throw new ArgumentException("Polygon requires at least three points.", nameof(points));

        MoveTo(points[0].X, points[0].Y);
        for (int i = 1; i < points.Length; i++)
            LineTo(points[i].X, points[i].Y);
        return Close();
    }

    // ── Paint ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the fill colour for this path.
    /// When combined with <see cref="Stroke"/> the path is both filled and stroked.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    public PathDescriptor Fill(string hexColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        FillColor = PdfColor.FromHex(hexColor);
        return this;
    }

    /// <summary>
    /// Sets the stroke colour and optional line width for this path.
    /// When combined with <see cref="Fill"/> the path is both filled and stroked.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative.</exception>
    public PathDescriptor Stroke(string hexColor, double lineWidth = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        StrokeColor = PdfColor.FromHex(hexColor);
        LineWidth   = lineWidth;
        return this;
    }

    /// <summary>
    /// Uses the even-odd rule instead of the non-zero winding rule when filling.
    /// Useful for shapes with holes (e.g. a donut).
    /// </summary>
    public PathDescriptor UseEvenOddFill()
    {
        EvenOddFill = true;
        return this;
    }
}
