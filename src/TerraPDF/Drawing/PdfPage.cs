using System.Globalization;
using System.Text;
using TerraPDF.Helpers;

namespace TerraPDF.Drawing;

/// <summary>Built-in PDF fonts available for rendering.</summary>
internal enum StandardFont
{
    Helvetica,        // F1 - normal
    TimesBold,        // F2 - bold
    Courier,          // F3 - monospace
    HelveticaOblique, // F4 - italic (non-bold)
    TimesBoldItalic,  // F5 - bold + italic
    TimesItalic,      // F6 - italic (non-bold, Times face)
}

/// <summary>
/// Represents a single page and accumulates PDF content-stream operators.
/// All public coordinates use a top-left origin (Y increases downward).
/// </summary>
internal sealed class PdfPage
{
    private readonly StringBuilder _ops = new();

    public double Width  { get; }
    public double Height { get; }

    internal PdfPage(double width, double height)
    {
        Width  = width;
        Height = height;
    }

    // --------------------------------------------------------------
    //  Drawing operations (primitive overloads - used by new API)
    // --------------------------------------------------------------

    internal void AddText(string text, double x, double y, double fontSize,
        PdfColor color, StandardFont font = StandardFont.Helvetica)
    {
        string fontAlias = font switch
        {
            StandardFont.TimesBold        => "F2",
            StandardFont.Courier          => "F3",
            StandardFont.HelveticaOblique => "F4",
            StandardFont.TimesBoldItalic  => "F5",
            StandardFont.TimesItalic      => "F6",
            _                             => "F1"
        };
        // PDF uses a bottom-left origin, so flip Y: pdfY = pageHeight - callerY
        double pdfY = Height - y;
        _ops.Append("BT\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{C(color.R)} {C(color.G)} {C(color.B)} rg\n");
        _ops.Append(CultureInfo.InvariantCulture, $"/{fontAlias} {F(fontSize)} Tf\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{F(x)} {F(pdfY)} Td\n");
        _ops.Append(CultureInfo.InvariantCulture, $"({Escape(text)}) Tj\n");
        _ops.Append("ET\n");
    }

    internal void AddLine(double x1, double y1, double x2, double y2,
        PdfColor color, double lineWidth = 1)
    {
        // Flip both endpoints from top-left to bottom-left origin
        double pdfY1 = Height - y1;
        double pdfY2 = Height - y2;
        _ops.Append(CultureInfo.InvariantCulture, $"{F(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{C(color.R)} {C(color.G)} {C(color.B)} RG\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{F(x1)} {F(pdfY1)} m\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{F(x2)} {F(pdfY2)} l\n");
        _ops.Append("S\n");
    }

    /// <summary>Draws a filled rectangle (no border).</summary>
    internal void AddFilledRect(double x, double y, double w, double h, PdfColor fillColor)
    {
        // PDF rect origin is bottom-left corner, so shift by h after flipping Y
        double pdfY = Height - y - h;
        _ops.Append(CultureInfo.InvariantCulture, $"{C(fillColor.R)} {C(fillColor.G)} {C(fillColor.B)} rg\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{F(x)} {F(pdfY)} {F(w)} {F(h)} re\n");
        _ops.Append("f\n");
    }

    /// <summary>Draws a stroked (outline-only) rectangle.</summary>
    internal void AddStrokedRect(double x, double y, double w, double h,
        PdfColor strokeColor, double lineWidth = 1)
    {
        double pdfY = Height - y - h;
        _ops.Append(CultureInfo.InvariantCulture, $"{F(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{C(strokeColor.R)} {C(strokeColor.G)} {C(strokeColor.B)} RG\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{F(x)} {F(pdfY)} {F(w)} {F(h)} re\n");
        _ops.Append("S\n");
    }

    /// <summary>Draws a filled-and-stroked rectangle.</summary>
    internal void AddRect(double x, double y, double w, double h,
        PdfColor fillColor, PdfColor strokeColor, double lineWidth = 1)
    {
        double pdfY = Height - y - h;
        _ops.Append(CultureInfo.InvariantCulture, $"{F(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{C(strokeColor.R)} {C(strokeColor.G)} {C(strokeColor.B)} RG\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{C(fillColor.R)} {C(fillColor.G)} {C(fillColor.B)} rg\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{F(x)} {F(pdfY)} {F(w)} {F(h)} re\n");
        _ops.Append("B\n");
    }

    // --------------------------------------------------------------
    //  Rounded rectangle primitives
    // --------------------------------------------------------------

    // Cubic Bézier approximation constant for a quarter-circle arc.
    // Control point offset = radius × k gives a visually accurate circular corner.
    private const double _bezierArcK = 0.5523;

    /// <summary>
    /// Appends a closed rounded-rectangle path to the content stream.
    /// <paramref name="r"/> is automatically clamped to half the shorter side
    /// so the corners never overlap.
    /// Coordinates use the caller's top-left origin; Y is flipped internally.
    /// </summary>
    private void AppendRoundedRectPath(double x, double y, double w, double h, double r)
    {
        // Clamp radius so corners never exceed half the shorter dimension
        r = Math.Min(r, Math.Min(w, h) / 2.0);

        // All coordinates in PDF bottom-left origin
        double b  = Height - y - h;   // bottom Y in PDF coords
        double t  = Height - y;       // top    Y in PDF coords
        double k  = r * _bezierArcK;

        // Start at top-left corner, just right of the top-left arc
        _ops.Append(CultureInfo.InvariantCulture, $"{F(x + r)} {F(t)} m\n");

        // Top edge → top-right arc
        _ops.Append(CultureInfo.InvariantCulture, $"{F(x + w - r)} {F(t)} l\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{F(x + w - r + k)} {F(t)} {F(x + w)} {F(t - r + k)} {F(x + w)} {F(t - r)} c\n");

        // Right edge → bottom-right arc
        _ops.Append(CultureInfo.InvariantCulture, $"{F(x + w)} {F(b + r)} l\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{F(x + w)} {F(b + r - k)} {F(x + w - r + k)} {F(b)} {F(x + w - r)} {F(b)} c\n");

        // Bottom edge → bottom-left arc
        _ops.Append(CultureInfo.InvariantCulture, $"{F(x + r)} {F(b)} l\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{F(x + r - k)} {F(b)} {F(x)} {F(b + r - k)} {F(x)} {F(b + r)} c\n");

        // Left edge → top-left arc → close
        _ops.Append(CultureInfo.InvariantCulture, $"{F(x)} {F(t - r)} l\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{F(x)} {F(t - r + k)} {F(x + r - k)} {F(t)} {F(x + r)} {F(t)} c\n");

        _ops.Append("h\n");
    }

    /// <summary>Draws a stroked rounded rectangle.</summary>
    internal void AddRoundedRect(double x, double y, double w, double h,
        double radius, PdfColor strokeColor, double lineWidth = 1)
    {
        _ops.Append(CultureInfo.InvariantCulture, $"{F(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{C(strokeColor.R)} {C(strokeColor.G)} {C(strokeColor.B)} RG\n");
        AppendRoundedRectPath(x, y, w, h, radius);
        _ops.Append("S\n");
    }

    /// <summary>Draws a filled rounded rectangle (no border).</summary>
    internal void AddFilledRoundedRect(double x, double y, double w, double h,
        double radius, PdfColor fillColor)
    {
        _ops.Append(CultureInfo.InvariantCulture, $"{C(fillColor.R)} {C(fillColor.G)} {C(fillColor.B)} rg\n");
        AppendRoundedRectPath(x, y, w, h, radius);
        _ops.Append("f\n");
    }

    /// <summary>Draws a filled-and-stroked rounded rectangle.</summary>
    internal void AddFilledAndStrokedRoundedRect(double x, double y, double w, double h,
        double radius, PdfColor fillColor, PdfColor strokeColor, double lineWidth = 1)
    {
        _ops.Append(CultureInfo.InvariantCulture, $"{F(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{C(strokeColor.R)} {C(strokeColor.G)} {C(strokeColor.B)} RG\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{C(fillColor.R)} {C(fillColor.G)} {C(fillColor.B)} rg\n");
        AppendRoundedRectPath(x, y, w, h, radius);
        _ops.Append("B\n");
    }

    // --------------------------------------------------------------
    //  Link annotations
    // --------------------------------------------------------------

    /// <summary>A clickable URI annotation rectangle in top-left-origin coordinates.</summary>
    internal readonly record struct LinkAnnotation(double X, double Y, double Width, double Height, string Url);

    /// <summary>Link annotations registered on this page by <see cref="Elements.Link"/> elements.</summary>
    internal List<LinkAnnotation> LinkAnnotations { get; } = [];

    /// <summary>Registers a URI annotation that covers the given bounding box.</summary>
    internal void AddLinkAnnotation(double x, double y, double width, double height, string url) =>
        LinkAnnotations.Add(new LinkAnnotation(x, y, width, height, url));

    // --------------------------------------------------------------
    //  Image XObjects
    // --------------------------------------------------------------

    /// <summary>
    /// Images registered for this page, keyed by alias.
    /// <list type="bullet">
    ///   <item>PNG  - <c>IsJpeg = false</c>, <c>Data</c> = flat decoded RGB bytes, <c>Components = 3</c>.</item>
    ///   <item>JPEG - <c>IsJpeg = true</c>,  <c>Data</c> = raw JPEG file bytes (no decoding needed).</item>
    /// </list>
    /// Populated by <see cref="DrawImage"/> and consumed by <see cref="PdfDocument"/>.
    /// </summary>
    internal readonly Dictionary<string, (byte[] Data, int Width, int Height, bool IsJpeg, int Components)> ImageObjects = new();

    /// <summary>
    /// Registers the image data under <paramref name="alias"/> (if not already present) and
    /// emits PDF content-stream operators that paint the image at the given position.
    /// </summary>
    /// <param name="alias">Resource name used in the page's /XObject dictionary (e.g. "Im1").</param>
    /// <param name="data">Raw image bytes: decoded RGB for PNG, or the original JPEG file bytes.</param>
    /// <param name="imgW">Source image width in pixels.</param>
    /// <param name="imgH">Source image height in pixels.</param>
    /// <param name="x">Left edge in caller coordinates (top-left origin).</param>
    /// <param name="y">Top edge in caller coordinates (top-left origin).</param>
    /// <param name="drawW">Rendered width in PDF points.</param>
    /// <param name="drawH">Rendered height in PDF points.</param>
    /// <param name="isJpeg">True for JPEG (raw file bytes, DCTDecode); false for PNG (decoded RGB, FlateDecode).</param>
    /// <param name="components">Colour component count: 1 = grayscale, 3 = RGB, 4 = CMYK.</param>
    internal void DrawImage(string alias, byte[] data, int imgW, int imgH,
        double x, double y, double drawW, double drawH,
        bool isJpeg = false, int components = 3)
    {
        // Register image data once per alias per page
        ImageObjects.TryAdd(alias, (data, imgW, imgH, isJpeg, components));

        // PDF images are placed via a Current Transformation Matrix (CTM):
        //   [scaleX 0 0 scaleY originX originY] cm
        // Then /AliasName Do paints a 1x1 unit image into that space.
        // Flip Y: PDF bottom-left origin means the image top = pageHeight - callerY,
        // and the rect origin sits at the bottom of the drawn area (top - drawH).
        double pdfY = Height - y - drawH;
        _ops.Append("q\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{F(drawW)} 0 0 {F(drawH)} {F(x)} {F(pdfY)} cm\n");
        _ops.Append(CultureInfo.InvariantCulture, $"/{alias} Do\n");
        _ops.Append("Q\n");
    }

    // --------------------------------------------------------------
    //  Serialization
    // --------------------------------------------------------------

    internal string BuildContentStream() => _ops.ToString();

    // --------------------------------------------------------------
    //  Helpers
    // --------------------------------------------------------------

    // Formats a coordinate / size as a 2-decimal PDF real (e.g. "72.00")
    private static string F(double d) => d.ToString("F2", CultureInfo.InvariantCulture);
    // Formats a colour component [0,1] as a 4-decimal PDF real (e.g. "0.5020")
    private static string C(double d) => d.ToString("F4", CultureInfo.InvariantCulture);

    // Escapes backslash, '(' and ')' which are syntax characters in PDF string literals
    private static string Escape(string s) =>
        s.Replace("\\", "\\\\")
         .Replace("(", "\\(")
         .Replace(")", "\\)");
}
