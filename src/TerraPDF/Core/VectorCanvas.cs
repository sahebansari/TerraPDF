using TerraPDF.Barcodes;
using TerraPDF.Drawing;
using TerraPDF.Elements;
using TerraPDF.Helpers;

namespace TerraPDF.Core;

/// <summary>Controls how a canvas image is fitted into its target rectangle.</summary>
public enum ImageFit
{
    /// <summary>Scales the image independently on each axis to fill the target rectangle.</summary>
    Stretch,

    /// <summary>Preserves the aspect ratio, centres the image, and keeps it entirely inside the target rectangle.</summary>
    Contain,

    /// <summary>Preserves the aspect ratio, centres the image, fills the target rectangle, and clips overflow.</summary>
    Cover,

    /// <summary>Preserves the aspect ratio, anchors the image at the top-left, fills the target rectangle, and clips overflow.</summary>
    CoverTopLeft,

    /// <summary>Draws the image at its natural 96-DPI size from the top-left and clips it to the target rectangle.</summary>
    CropTopLeft,
}

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
///   <item>Text labels, standard or via a registered custom font</item>
///   <item>Grid helpers</item>
/// </list>
/// </para>
/// </summary>
public sealed class VectorCanvas
{
    /// <summary>Returns an image's natural size in PDF points, assuming 96 DPI for pixel data.</summary>
    /// <param name="imageData">Raw PNG or JPEG file bytes.</param>
    /// <returns>The image width and height in PDF points.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="imageData"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The data is empty or the format is unsupported.</exception>
    public static (double Width, double Height) GetImageSizeInPoints(byte[] imageData)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        // Validated here so unsupported data raises the documented ArgumentException,
        // matching Image(byte[]); the ImageElement constructor raises
        // NotSupportedException instead.
        Elements.ImageElement.ValidateFormat(imageData);
        var image = new ImageElement(imageData);
        return (image.PixelSize.Width * 72d / 96d, image.PixelSize.Height * 72d / 96d);
    }

    // -----------------------------------------------------------------------
    //  Internal drawing command records
    // -----------------------------------------------------------------------

    // Each Draw* method appends one of these to _commands.
    // CanvasElement.Draw() replays them all onto PdfPage in order.

    internal abstract record DrawCommand;

    internal sealed record DrawLineCmd(
        double X1, double Y1, double X2, double Y2,
        string HexColor, double LineWidth, double Opacity,
        double[]? DashPattern, double DashPhase) : DrawCommand;

    internal sealed record DrawRectCmd(
        double X, double Y, double W, double H,
        string? FillHex, string? StrokeHex, double LineWidth, double Opacity,
        double[]? DashPattern, double DashPhase) : DrawCommand;

    internal sealed record DrawRoundedRectCmd(
        double X, double Y, double W, double H, double Radius,
        string? FillHex, string? StrokeHex, double LineWidth, double Opacity,
        double[]? DashPattern = null, double DashPhase = 0) : DrawCommand;

    internal sealed record DrawEllipseCmd(
        double Cx, double Cy, double Rx, double Ry,
        string? FillHex, string? StrokeHex, double LineWidth, double Opacity,
        double[]? DashPattern = null, double DashPhase = 0) : DrawCommand;

    internal sealed record DrawLinkCmd(double X, double Y, double W, double H, string Url) : DrawCommand;

    internal sealed record DrawInternalLinkCmd(
        double X, double Y, double W, double H, int PageNumber, double? Top) : DrawCommand;

    internal sealed record DrawBookmarkCmd(string Title, string? ParentTitle, double Y) : DrawCommand;

    internal sealed record DrawQrCodeCmd(
        string Data, double X, double Y, double Size, QrErrorCorrectionLevel Level,
        string Hex, string? BackgroundHex, int QuietZoneModules) : DrawCommand
    {
        /// <summary>
        /// The encoded symbol, generated once when the command is recorded (which also
        /// validates the data) and reused on every replay, so a canvas repeated across
        /// pages does not re-encode its QR code per page.
        /// </summary>
        internal TerraPDF.Barcodes.QrCode.QrCode? Symbol { get; init; }
    }

    internal sealed record DrawPathCmd(PathDescriptor Path) : DrawCommand;

    internal sealed record DrawImageCmd(
        byte[] Data, double X, double Y, double W, double H, ImageFit Fit) : DrawCommand
    {
        /// <summary>
        /// The image element, populated by <c>CanvasElement</c> on first draw and reused
        /// on every later replay of this command so a canvas repeated across pages
        /// parses and hashes its image once rather than once per page. Excluded from record equality
        /// and value semantics on purpose — it is a cache, not part of the command.
        /// </summary>
        internal Elements.ImageElement? Decoded { get; set; }
    }

    // Expanded into lines at draw time, when the canvas size is known: the
    // configure callback runs before layout, so the size is not known when Grid() is called.
    internal sealed record DrawGridCmd(
        double CellWidth, double CellHeight, string HexColor, double LineWidth) : DrawCommand;

    internal sealed record DrawTextCmd(
        double X, double Y, string Text, string HexColor, double FontSize,
        string? FontFamily, bool Bold, bool Italic, double Opacity, double Angle) : DrawCommand;

    // -----------------------------------------------------------------------
    //  State
    // -----------------------------------------------------------------------

    internal readonly List<DrawCommand> Commands = [];

    // The bounding box is injected at render time by CanvasElement.
    internal double AllocatedWidth { get; set; }
    internal double AllocatedHeight { get; set; }

    // -----------------------------------------------------------------------
    //  Opacity
    // -----------------------------------------------------------------------

    /// <summary>
    /// Validates that an <c>opacity</c> argument is a legal constant-alpha value.
    /// PDF's <c>/ExtGState</c> <c>/ca</c>/<c>/CA</c> range is [0, 1]; 1 (fully
    /// opaque, the default on every shape method) emits no <c>/ExtGState</c> at all.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="opacity"/> is outside [0, 1].</exception>
    private static void ValidateOpacity(double opacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(opacity, 0.0, nameof(opacity));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(opacity, 1.0, nameof(opacity));
    }

    private static double[]? PrepareDashPattern(double[]? dashPattern, double dashPhase)
    {
        if (dashPattern is null) return null;
        if (dashPattern.Length == 0 || dashPattern.Any(value => value < 0 || double.IsNaN(value) || double.IsInfinity(value)) || dashPattern.All(value => value == 0))
            throw new ArgumentException("Dash patterns must contain at least one positive, finite value.", nameof(dashPattern));
        // A negative phase is rejected by the PDF spec (ISO 32000-1 §8.4.3.6), which
        // requires the dash phase to be a nonnegative number of user-space units.
        if (!double.IsFinite(dashPhase) || dashPhase < 0)
            throw new ArgumentOutOfRangeException(nameof(dashPhase), dashPhase,
                "Dash phase must be a nonnegative, finite number.");
        return dashPattern.ToArray();
    }

    // -----------------------------------------------------------------------
    //  Line
    // -----------------------------------------------------------------------

    /// <summary>Draws a straight line from (<paramref name="x1"/>, <paramref name="y1"/>)
    /// to (<paramref name="x2"/>, <paramref name="y2"/>).</summary>
    /// <remarks><paramref name="dashPattern"/> contains alternating dash and gap lengths in points and is copied; <see langword="null"/> produces a solid line. <paramref name="dashPhase"/> offsets the start within that pattern.</remarks>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace, or <paramref name="dashPattern"/> is empty, contains an invalid value, or contains only zeros.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative, <paramref name="opacity"/> is outside [0, 1], or <paramref name="dashPhase"/> is negative or not finite.</exception>
    public VectorCanvas Line(double x1, double y1, double x2, double y2,
        string hexColor = "#000000", double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        dashPattern = PrepareDashPattern(dashPattern, dashPhase);
        Commands.Add(new DrawLineCmd(x1, y1, x2, y2, hexColor, lineWidth, opacity, dashPattern, dashPhase));
        return this;
    }

    // -----------------------------------------------------------------------
    //  Rectangle
    // -----------------------------------------------------------------------

    /// <summary>Draws a filled rectangle.</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas FillRect(double x, double y, double width, double height,
        string hexColor = "#000000", double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ValidateOpacity(opacity);
        Commands.Add(new DrawRectCmd(x, y, width, height, hexColor, null, 0, opacity, null, 0));
        return this;
    }

    /// <summary>Draws a stroked (outline-only) rectangle.</summary>
    /// <remarks><paramref name="dashPattern"/> contains alternating dash and gap lengths in points and is copied; <see langword="null"/> produces a solid outline. <paramref name="dashPhase"/> offsets the start within that pattern.</remarks>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace, or <paramref name="dashPattern"/> is empty, contains an invalid value, or contains only zeros.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative, <paramref name="opacity"/> is outside [0, 1], or <paramref name="dashPhase"/> is negative or not finite.</exception>
    public VectorCanvas StrokeRect(double x, double y, double width, double height,
        string hexColor = "#000000", double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawRectCmd(x, y, width, height, null, hexColor, lineWidth, opacity,
            PrepareDashPattern(dashPattern, dashPhase), dashPhase));
        return this;
    }

    /// <summary>Draws a filled and stroked rectangle.</summary>
    /// <remarks><paramref name="dashPattern"/> contains alternating dash and gap lengths in points and is copied; <see langword="null"/> produces a solid outline. <paramref name="dashPhase"/> offsets the start within that pattern.</remarks>
    /// <exception cref="ArgumentException">Either color argument is null or whitespace, or <paramref name="dashPattern"/> is empty, contains an invalid value, or contains only zeros.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative, <paramref name="opacity"/> is outside [0, 1], or <paramref name="dashPhase"/> is negative or not finite.</exception>
    public VectorCanvas DrawRect(double x, double y, double width, double height,
        string fillHex = "#FFFFFF", string strokeHex = "#000000", double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawRectCmd(x, y, width, height, fillHex, strokeHex, lineWidth, opacity,
            PrepareDashPattern(dashPattern, dashPhase), dashPhase));
        return this;
    }

    // -----------------------------------------------------------------------
    //  Rounded rectangle
    // -----------------------------------------------------------------------

    /// <summary>Draws a filled rounded rectangle.</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas FillRoundedRect(double x, double y, double width, double height,
        double radius, string hexColor = "#000000", double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ValidateOpacity(opacity);
        Commands.Add(new DrawRoundedRectCmd(x, y, width, height, radius, hexColor, null, 0, opacity));
        return this;
    }

    /// <summary>Draws a stroked rounded rectangle.</summary>
    /// <remarks><paramref name="dashPattern"/> contains alternating dash and gap lengths in points and is copied; <see langword="null"/> produces a solid outline. <paramref name="dashPhase"/> offsets the start within that pattern.</remarks>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace, or <paramref name="dashPattern"/> is empty, contains an invalid value, or contains only zeros.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative, <paramref name="opacity"/> is outside [0, 1], or <paramref name="dashPhase"/> is negative or not finite.</exception>
    public VectorCanvas StrokeRoundedRect(double x, double y, double width, double height,
        double radius, string hexColor = "#000000", double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawRoundedRectCmd(x, y, width, height, radius, null, hexColor, lineWidth, opacity,
            PrepareDashPattern(dashPattern, dashPhase), dashPhase));
        return this;
    }

    /// <summary>Draws a filled and stroked rounded rectangle.</summary>
    /// <remarks><paramref name="dashPattern"/> contains alternating dash and gap lengths in points and is copied; <see langword="null"/> produces a solid outline. <paramref name="dashPhase"/> offsets the start within that pattern.</remarks>
    /// <exception cref="ArgumentException">Either color argument is null or whitespace, or <paramref name="dashPattern"/> is empty, contains an invalid value, or contains only zeros.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative, <paramref name="opacity"/> is outside [0, 1], or <paramref name="dashPhase"/> is negative or not finite.</exception>
    public VectorCanvas DrawRoundedRect(double x, double y, double width, double height,
        double radius, string fillHex = "#FFFFFF", string strokeHex = "#000000", double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawRoundedRectCmd(x, y, width, height, radius, fillHex, strokeHex, lineWidth, opacity,
            PrepareDashPattern(dashPattern, dashPhase), dashPhase));
        return this;
    }

    // -----------------------------------------------------------------------
    //  Circle / Ellipse
    // -----------------------------------------------------------------------

    /// <summary>Draws a filled circle centred at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas FillCircle(double cx, double cy, double radius, string hexColor = "#000000", double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ValidateOpacity(opacity);
        Commands.Add(new DrawEllipseCmd(cx, cy, radius, radius, hexColor, null, 0, opacity));
        return this;
    }

    /// <summary>Draws a stroked circle centred at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas StrokeCircle(double cx, double cy, double radius,
        string hexColor = "#000000", double lineWidth = 1, double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawEllipseCmd(cx, cy, radius, radius, null, hexColor, lineWidth, opacity));
        return this;
    }

    /// <summary>Draws a filled and stroked circle.</summary>
    /// <exception cref="ArgumentException">Either color argument is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas DrawCircle(double cx, double cy, double radius,
        string fillHex = "#FFFFFF", string strokeHex = "#000000", double lineWidth = 1, double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawEllipseCmd(cx, cy, radius, radius, fillHex, strokeHex, lineWidth, opacity));
        return this;
    }

    /// <summary>Draws a filled ellipse centred at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either radius is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas FillEllipse(double cx, double cy, double rx, double ry,
        string hexColor = "#000000", double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ry);
        ValidateOpacity(opacity);
        Commands.Add(new DrawEllipseCmd(cx, cy, rx, ry, hexColor, null, 0, opacity));
        return this;
    }

    /// <summary>Draws a stroked ellipse centred at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    /// <remarks><paramref name="dashPattern"/> contains alternating dash and gap lengths in points and is copied; <see langword="null"/> produces a solid outline. <paramref name="dashPhase"/> offsets the start within that pattern.</remarks>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace, or <paramref name="dashPattern"/> is empty, contains an invalid value, or contains only zeros.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either radius or <paramref name="lineWidth"/> is zero or negative, <paramref name="opacity"/> is outside [0, 1], or <paramref name="dashPhase"/> is negative or not finite.</exception>
    public VectorCanvas StrokeEllipse(double cx, double cy, double rx, double ry,
        string hexColor = "#000000", double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ry);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawEllipseCmd(cx, cy, rx, ry, null, hexColor, lineWidth, opacity,
            PrepareDashPattern(dashPattern, dashPhase), dashPhase));
        return this;
    }

    /// <summary>Draws a filled and stroked ellipse.</summary>
    /// <remarks><paramref name="dashPattern"/> contains alternating dash and gap lengths in points and is copied; <see langword="null"/> produces a solid outline. <paramref name="dashPhase"/> offsets the start within that pattern.</remarks>
    /// <exception cref="ArgumentException">Either color argument is null or whitespace, or <paramref name="dashPattern"/> is empty, contains an invalid value, or contains only zeros.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Any radius or <paramref name="lineWidth"/> is zero or negative, <paramref name="opacity"/> is outside [0, 1], or <paramref name="dashPhase"/> is negative or not finite.</exception>
    public VectorCanvas DrawEllipse(double cx, double cy, double rx, double ry,
        string fillHex = "#FFFFFF", string strokeHex = "#000000", double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ry);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawEllipseCmd(cx, cy, rx, ry, fillHex, strokeHex, lineWidth, opacity,
            PrepareDashPattern(dashPattern, dashPhase), dashPhase));
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
    //  Text
    // -----------------------------------------------------------------------

    /// <summary>
    /// Draws a single line of text with its <b>baseline</b> at (<paramref name="x"/>,
    /// <paramref name="y"/>) — not its top-left corner, since a text baseline (not a
    /// bounding-box corner) is what lets a label sit flush against a line, chart axis,
    /// or shape it annotates. Renders through a registered custom font when
    /// <paramref name="fontFamily"/> names one (see
    /// <see cref="Helpers.FontFamily.Register(string, string, bool, bool)"/>),
    /// otherwise through the standard-14 family <paramref name="fontFamily"/> resolves
    /// to (Helvetica/Times/Courier — see <see cref="PdfFonts.Resolve"/>; a
    /// <see langword="null"/>/unrecognised name defaults to Helvetica). Does not wrap
    /// or measure-and-fit — use <see cref="MeasureTextWidth"/> to size or centre it
    /// yourself first.
    /// </summary>
    /// <remarks><paramref name="angle"/> rotates clockwise in degrees around the baseline point (<paramref name="x"/>, <paramref name="y"/>). Negative values and values outside one revolution are accepted.</remarks>
    /// <exception cref="ArgumentException"><paramref name="text"/> or <paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fontSize"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas Text(string text, double x, double y,
        string hexColor = "#000000", double fontSize = 12,
        string? fontFamily = null, bool bold = false, bool italic = false, double opacity = 1,
        double angle = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        ValidateOpacity(opacity);
        Commands.Add(new DrawTextCmd(x, y, text, hexColor, fontSize, fontFamily, bold, italic, opacity, angle));
        return this;
    }

    /// <summary>Draws a PNG or JPEG image at an absolute canvas position.</summary>
    /// <param name="imageData">Raw PNG or JPEG file bytes. The data is copied when this method is called.</param>
    /// <param name="x">Left edge in points relative to the canvas.</param>
    /// <param name="y">Top edge in points relative to the canvas.</param>
    /// <param name="width">Rendered width in points.</param>
    /// <param name="height">Rendered height in points.</param>
    /// <param name="fit">How the source image is fitted into the target rectangle.</param>
    /// <exception cref="ArgumentException">The data is empty or the format is unsupported.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is zero or negative.</exception>
    public VectorCanvas Image(byte[] imageData, double x, double y, double width, double height,
        ImageFit fit = ImageFit.Stretch)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        if (imageData.Length == 0)
            throw new ArgumentException("Image data cannot be empty.", nameof(imageData));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Elements.ImageElement.ValidateFormat(imageData);
        Commands.Add(new DrawImageCmd(imageData.ToArray(), x, y, width, height, fit));
        return this;
    }

    /// <summary>Draws a PNG or JPEG file at an absolute canvas position.</summary>
    /// <param name="filePath">Path to a PNG or JPEG file. The format is detected from magic bytes rather than the extension.</param>
    /// <param name="x">Left edge in points relative to the canvas.</param>
    /// <param name="y">Top edge in points relative to the canvas.</param>
    /// <param name="width">Rendered width in points.</param>
    /// <param name="height">Rendered height in points.</param>
    /// <param name="fit">How the source image is fitted into the target rectangle.</param>
    /// <exception cref="ArgumentException">The path is invalid, the file is empty, or the format is unsupported.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is zero or negative.</exception>
    public VectorCanvas Image(string filePath, double x, double y, double width, double height,
        ImageFit fit = ImageFit.Stretch) =>
        Image(File.ReadAllBytes(filePath), x, y, width, height, fit);

    /// <summary>Draws a PNG or JPEG stream at an absolute canvas position.</summary>
    /// <param name="imageStream">Readable PNG or JPEG stream. The remaining bytes are copied and the stream is left open.</param>
    /// <param name="x">Left edge in points relative to the canvas.</param>
    /// <param name="y">Top edge in points relative to the canvas.</param>
    /// <param name="width">Rendered width in points.</param>
    /// <param name="height">Rendered height in points.</param>
    /// <param name="fit">How the source image is fitted into the target rectangle.</param>
    /// <remarks>The stream is read from its current position but is not disposed by this method.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="imageStream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The remaining stream data is empty or the format is unsupported.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is zero or negative.</exception>
    public VectorCanvas Image(Stream imageStream, double x, double y, double width, double height,
        ImageFit fit = ImageFit.Stretch)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        using var buffer = new MemoryStream();
        imageStream.CopyTo(buffer);
        return Image(buffer.ToArray(), x, y, width, height, fit);
    }

    /// <summary>Draws a filled elliptical sector inside the specified rectangle.</summary>
    /// <remarks><paramref name="startAngle"/> is measured clockwise from the right-hand point. Negative <paramref name="sweepAngle"/> values sweep counter-clockwise, zero draws nothing, and values beyond one revolution are preserved.</remarks>
    /// <exception cref="ArgumentException"><paramref name="fillHex"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas FillPie(double x, double y, double width, double height,
        double startAngle, double sweepAngle, string fillHex = "#000000", double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ValidateOpacity(opacity);
        return Path(path => path.Sector(x + width / 2, y + height / 2, width / 2, height / 2,
            startAngle, sweepAngle).Fill(fillHex).Opacity(opacity));
    }

    /// <summary>Draws the outline of an elliptical sector inside the specified rectangle.</summary>
    /// <remarks><paramref name="startAngle"/> is measured clockwise from the right-hand point. Negative <paramref name="sweepAngle"/> values sweep counter-clockwise, zero draws nothing, and values beyond one revolution are preserved. <paramref name="dashPattern"/> contains alternating dash and gap lengths in points and is copied; <see langword="null"/> produces a solid outline. <paramref name="dashPhase"/> offsets the start within that pattern.</remarks>
    /// <exception cref="ArgumentException"><paramref name="strokeHex"/> is null or whitespace, or <paramref name="dashPattern"/> is empty, contains an invalid value, or contains only zeros.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension or <paramref name="lineWidth"/> is zero or negative, <paramref name="opacity"/> is outside [0, 1], or <paramref name="dashPhase"/> is negative or not finite.</exception>
    public VectorCanvas StrokePie(double x, double y, double width, double height,
        double startAngle, double sweepAngle, string strokeHex = "#000000",
        double lineWidth = 1, double opacity = 1, double[]? dashPattern = null, double dashPhase = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        dashPattern = PrepareDashPattern(dashPattern, dashPhase);
        return Path(path =>
        {
            path.Sector(x + width / 2, y + height / 2, width / 2, height / 2,
                startAngle, sweepAngle).Stroke(strokeHex, lineWidth).Opacity(opacity);
            if (dashPattern is not null) path.Dash(dashPattern, dashPhase);
        });
    }

    /// <summary>Draws a filled and stroked elliptical sector inside the specified rectangle.</summary>
    /// <remarks><paramref name="startAngle"/> is measured clockwise from the right-hand point. Negative <paramref name="sweepAngle"/> values sweep counter-clockwise, zero draws nothing, and values beyond one revolution are preserved. <paramref name="dashPattern"/> contains alternating dash and gap lengths in points and is copied; <see langword="null"/> produces a solid outline. <paramref name="dashPhase"/> offsets the start within that pattern.</remarks>
    /// <exception cref="ArgumentException">Either color argument is null or whitespace, or <paramref name="dashPattern"/> is empty, contains an invalid value, or contains only zeros.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension or <paramref name="lineWidth"/> is zero or negative, <paramref name="opacity"/> is outside [0, 1], or <paramref name="dashPhase"/> is negative or not finite.</exception>
    public VectorCanvas DrawPie(double x, double y, double width, double height,
        double startAngle, double sweepAngle, string fillHex = "#FFFFFF",
        string strokeHex = "#000000", double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        dashPattern = PrepareDashPattern(dashPattern, dashPhase);
        return Path(path =>
        {
            path.Sector(x + width / 2, y + height / 2, width / 2, height / 2,
                startAngle, sweepAngle).Fill(fillHex).Stroke(strokeHex, lineWidth).Opacity(opacity);
            if (dashPattern is not null) path.Dash(dashPattern, dashPhase);
        });
    }

    /// <summary>
    /// Total advance width <paramref name="text"/> would occupy at <paramref name="fontSize"/>
    /// in the same font <see cref="Text"/> would render it in — use this to right-align or
    /// centre a label before placing it, since <see cref="Text"/> itself does not.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fontSize"/> is zero or negative.</exception>
    public static double MeasureTextWidth(string text, double fontSize,
        string? fontFamily = null, bool bold = false, bool italic = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        var font = PdfFonts.ResolveFont(fontFamily, bold, italic);
        return FontMetrics.MeasureWidth(text, fontSize, font, bold, italic);
    }

    // -----------------------------------------------------------------------
    //  Links and bookmarks
    // -----------------------------------------------------------------------

    /// <summary>Adds a clickable rectangle that opens <paramref name="url"/> (a URI link annotation).</summary>
    /// <param name="x">Left edge in points relative to the canvas.</param>
    /// <param name="y">Top edge in points relative to the canvas.</param>
    /// <param name="width">Width of the clickable area in points.</param>
    /// <param name="height">Height of the clickable area in points.</param>
    /// <param name="url">Destination URI.</param>
    /// <exception cref="ArgumentException"><paramref name="url"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is zero or negative.</exception>
    public VectorCanvas Link(double x, double y, double width, double height, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Commands.Add(new DrawLinkCmd(x, y, width, height, url));
        return this;
    }

    /// <summary>
    /// Adds a clickable rectangle that jumps to a page of the same document (a GoTo link annotation).
    /// </summary>
    /// <param name="x">Left edge in points relative to the canvas.</param>
    /// <param name="y">Top edge in points relative to the canvas.</param>
    /// <param name="width">Width of the clickable area in points.</param>
    /// <param name="height">Height of the clickable area in points.</param>
    /// <param name="pageNumber">1-based physical page number. Rendering throws if the document has fewer pages.</param>
    /// <param name="top">Optional distance from the top of the target page to scroll to; <see langword="null"/> fits the page.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size or <paramref name="pageNumber"/> is zero or negative, or <paramref name="top"/> is negative.</exception>
    public VectorCanvas InternalLink(double x, double y, double width, double height,
        int pageNumber, double? top = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
        if (top is < 0) throw new ArgumentOutOfRangeException(nameof(top), top, "Top must not be negative.");
        Commands.Add(new DrawInternalLinkCmd(x, y, width, height, pageNumber, top));
        return this;
    }

    /// <summary>
    /// Adds an outline (bookmark) entry pointing at the vertical position <paramref name="y"/> of
    /// the page this canvas is drawn on.
    /// </summary>
    /// <param name="title">Text shown in the viewer's outline pane.</param>
    /// <param name="y">Distance from the top of the page to scroll to when the entry is activated.</param>
    /// <param name="parentTitle">Title of an earlier bookmark to nest under; <see langword="null"/> for a top-level entry.</param>
    /// <remarks>Entries are identified by (title, parent): a repeated pair is recorded once. A negative <paramref name="y"/> is clamped to 0.</remarks>
    /// <exception cref="ArgumentException"><paramref name="title"/> is null or whitespace, or <paramref name="parentTitle"/> is empty or whitespace.</exception>
    public VectorCanvas Bookmark(string title, double y = 0, string? parentTitle = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (parentTitle is not null) ArgumentException.ThrowIfNullOrWhiteSpace(parentTitle);
        Commands.Add(new DrawBookmarkCmd(title, parentTitle, Math.Max(0, y)));
        return this;
    }

    // -----------------------------------------------------------------------
    //  QR code
    // -----------------------------------------------------------------------

    /// <summary>
    /// Draws a QR code (ISO/IEC 18004, byte mode, smallest version that fits) as a single
    /// vector path, inside the square (<paramref name="x"/>, <paramref name="y"/>, <paramref name="size"/>).
    /// </summary>
    /// <param name="data">Text to encode, as UTF-8.</param>
    /// <param name="x">Left edge in points relative to the canvas.</param>
    /// <param name="y">Top edge in points relative to the canvas.</param>
    /// <param name="size">Side length in points, including the quiet zone.</param>
    /// <param name="level">Error correction level; higher levels survive more damage but need a bigger symbol.</param>
    /// <param name="hexColor">Color of the dark modules.</param>
    /// <param name="backgroundHex">Color of the square behind the modules, or <see langword="null"/> for transparent.</param>
    /// <param name="quietZoneModules">Empty modules around the symbol (the spec asks for 4).</param>
    /// <exception cref="ArgumentException"><paramref name="data"/> or <paramref name="hexColor"/> is null or whitespace, or <paramref name="backgroundHex"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is zero or negative, or <paramref name="quietZoneModules"/> is negative.</exception>
    /// <exception cref="NotSupportedException">The data does not fit in a version-40 symbol at this level.</exception>
    public VectorCanvas QrCode(string data, double x, double y, double size,
        QrErrorCorrectionLevel level = QrErrorCorrectionLevel.M, string hexColor = "#000000",
        string? backgroundHex = null, int quietZoneModules = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        if (backgroundHex is not null) ArgumentException.ThrowIfNullOrWhiteSpace(backgroundHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        ArgumentOutOfRangeException.ThrowIfNegative(quietZoneModules);
        // Fails now, at the call site, rather than later when the page is rendered.
        var symbol = TerraPDF.Barcodes.QrCode.QrCodeGenerator.Generate(data, level);
        Commands.Add(new DrawQrCodeCmd(data, x, y, size, level, hexColor, backgroundHex, quietZoneModules)
        {
            Symbol = symbol,
        });
        return this;
    }

    // -----------------------------------------------------------------------
    //  Grid helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Draws a rectangular grid of vertical and horizontal lines that fills the canvas area.
    /// The grid is sized when the canvas is drawn, so it always matches the width the canvas
    /// receives from the layout, and it is drawn in call order relative to other commands.
    /// </summary>
    /// <param name="cellWidth">Width of each cell in PDF points.</param>
    /// <param name="cellHeight">Height of each cell in PDF points. When <c>null</c> uses <paramref name="cellWidth"/> (square cells).</param>
    /// <param name="hexColor">Line colour. Defaults to light grey.</param>
    /// <param name="lineWidth">Stroke width. Defaults to 0.5 pt.</param>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cellWidth"/>, <paramref name="cellHeight"/>, or <paramref name="lineWidth"/> is zero or negative.</exception>
    public VectorCanvas Grid(double cellWidth, double? cellHeight = null,
        string hexColor = "#CCCCCC", double lineWidth = 0.5)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellWidth);
        if (cellHeight.HasValue) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellHeight.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);

        Commands.Add(new DrawGridCmd(cellWidth, cellHeight ?? cellWidth, hexColor, lineWidth));
        return this;
    }
}
