namespace TerraPDF.Core;

// ---------------------------------------------------------------------------
//  Internal discriminated union for vector path commands.
//  Produced by PathDescriptor and consumed by VectorCanvas when rendering.
// ---------------------------------------------------------------------------

internal abstract record VectorPathCommand;

/// <summary>Begins a new subpath at the given point.</summary>
internal sealed record MoveToCmd(double X, double Y) : VectorPathCommand;

/// <summary>Appends a straight line from the current point to the given point.</summary>
internal sealed record LineToCmd(double X, double Y) : VectorPathCommand;

/// <summary>
/// Appends a cubic Bézier curve from the current point to (<paramref name="X"/>, <paramref name="Y"/>)
/// using (<paramref name="Cx1"/>, <paramref name="Cy1"/>) and (<paramref name="Cx2"/>, <paramref name="Cy2"/>)
/// as control points.
/// </summary>
internal sealed record CurveToCmd(
    double Cx1, double Cy1,
    double Cx2, double Cy2,
    double X,   double Y) : VectorPathCommand;

/// <summary>Closes the current subpath by appending a straight line back to the start point.</summary>
internal sealed record ClosePathCmd : VectorPathCommand;
