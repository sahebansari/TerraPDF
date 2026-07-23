using System.Globalization;
using System.Text;
using TerraPDF.Helpers;

namespace TerraPDF.Drawing;

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
    //  Text objects (BeginTextObject … ShowTextAt* … EndTextObject)
    // --------------------------------------------------------------

    // State inside the current text object. Font/colour are re-emitted only
    // when they change between ShowTextAt calls; the Td origin is tracked so
    // each positioning operator is a small relative move.
    private string?   _textFontAlias;
    private double    _textFontSize;
    private PdfColor? _textColor;
    private double    _textTdX;
    private double    _textTdY;

    /// <summary>
    /// Opens a text object (<c>BT</c>). All state is reset — the PDF text
    /// matrix resets at BT, and resetting font/colour too keeps the tracker
    /// immune to graphics-state changes made by non-text operators in between.
    /// </summary>
    internal void BeginTextObject()
    {
        _ops.Append("BT\n");
        _textFontAlias = null;
        _textFontSize  = 0;
        _textColor     = null;
        _textTdX       = 0;
        _textTdY       = 0;
    }

    /// <summary>
    /// Shows <paramref name="text"/> with its baseline at (<paramref name="x"/>,
    /// <paramref name="y"/>) in top-left-origin coordinates.  Must be called
    /// between <see cref="BeginTextObject"/> and <see cref="EndTextObject"/>.
    /// Font and colour operators are emitted only when they differ from the
    /// previous call in the same text object.
    /// </summary>
    internal void ShowTextAt(string text, double x, double y, double fontSize,
        PdfColor color, PdfFontFamily family = PdfFontFamily.Helvetica,
        bool bold = false, bool italic = false)
    {
        string fontAlias = PdfFonts.Alias(family, bold, italic);
        EmitFontColorAndPosition(fontAlias, fontSize, color, x, y);
        _ops.Append(CultureInfo.InvariantCulture, $"({EscapeForPdfString(text)}) Tj\n");
    }

    /// <summary>
    /// Shows <paramref name="text"/> set in a registered custom (embedded) font.
    /// Uses <c>Identity-H</c> encoding: each Unicode scalar value (surrogate pairs
    /// kept intact) is mapped through the font's own <c>cmap</c> to a glyph ID and
    /// emitted as a 2-byte big-endian hex code, recording the glyph as used so
    /// <see cref="PdfDocument"/> can build a minimal <c>/W</c> width array and
    /// <c>/ToUnicode</c> CMap for just the glyphs this document actually shows.
    /// </summary>
    internal void ShowTextAtCustomFont(string text, double x, double y, double fontSize,
        PdfColor color, TrueType.CustomFontVariant variant)
    {
        string fontAlias = GetOrAddCustomFontAlias(variant);
        EmitFontColorAndPosition(fontAlias, fontSize, color, x, y);
        _ops.Append(EncodeIdentityHHex(text, variant));
        _ops.Append(" Tj\n");
    }

    /// <summary>
    /// Emits the colour (<c>rg</c>), font (<c>Tf</c>), and position (<c>Td</c>) operators
    /// shared by every text-showing call, re-emitting colour/font only when they differ
    /// from the previous call in the same text object. Shared by <see cref="ShowTextAt"/>
    /// and <see cref="ShowTextAtCustomFont"/> so the two paths stay byte-identical for the
    /// operators they have in common.
    /// </summary>
    private void EmitFontColorAndPosition(string fontAlias, double fontSize, PdfColor color, double x, double y)
    {
        if (_textColor is null || !_textColor.Value.Equals(color))
        {
            _ops.Append(CultureInfo.InvariantCulture, $"{C(color.R)} {C(color.G)} {C(color.B)} rg\n");
            _textColor = color;
        }

        if (fontAlias != _textFontAlias || fontSize != _textFontSize)
        {
            _ops.Append(CultureInfo.InvariantCulture, $"/{fontAlias} {F(fontSize)} Tf\n");
            _textFontAlias = fontAlias;
            _textFontSize  = fontSize;
        }

        // Td moves relative to the previous text-line origin. Deltas are taken
        // between rounded absolute positions so rounding never accumulates.
        double pdfX = Math.Round(x, 2);
        double pdfY = Math.Round(Height - y, 2);
        _ops.Append(CultureInfo.InvariantCulture, $"{F(pdfX - _textTdX)} {F(pdfY - _textTdY)} Td\n");
        _textTdX = pdfX;
        _textTdY = pdfY;
    }

    /// <summary>
    /// Encodes <paramref name="text"/> as an Identity-H hex string token, e.g. <c>&lt;0003001A&gt;</c>.
    /// Codepoints are decoded and Devanagari-reordered via <see cref="TrueType.DevanagariReordering.DecodeAndReorder"/>,
    /// then mapped to glyphs (with conjunct-ligature substitution) via
    /// <see cref="TrueType.DevanagariConjuncts.MapToGlyphs"/>, so glyph order here always
    /// matches what <see cref="TrueType.CustomFontVariant.MeasureWidth"/> measured.
    /// </summary>
    private string EncodeIdentityHHex(string text, TrueType.CustomFontVariant variant)
    {
        var codepoints = TrueType.DevanagariReordering.DecodeAndReorder(text);
        var glyphs = TrueType.DevanagariConjuncts.MapToGlyphs(codepoints, variant.Font);
        var sb = new StringBuilder(glyphs.Count * 4 + 2);
        sb.Append('<');
        foreach (var (gid, codepoint) in glyphs)
        {
            RecordCustomGlyphUsage(variant, gid, codepoint);
            sb.Append(gid.ToString("X4", CultureInfo.InvariantCulture));
        }
        sb.Append('>');
        return sb.ToString();
    }

    /// <summary>Closes the current text object (<c>ET</c>).</summary>
    internal void EndTextObject() => _ops.Append("ET\n");

    // --------------------------------------------------------------
    //  Custom (embedded) font resources
    // --------------------------------------------------------------

    private readonly Dictionary<TrueType.CustomFontVariant, string> _customFontAliasByVariant = new();

    /// <summary>Custom font resources used on this page: page-local alias → variant.</summary>
    internal Dictionary<string, TrueType.CustomFontVariant> CustomFontObjects { get; } = new();

    /// <summary>Glyph IDs shown on this page per custom font variant, each mapped to a representative Unicode codepoint (for <c>/ToUnicode</c>).</summary>
    internal Dictionary<TrueType.CustomFontVariant, Dictionary<ushort, int>> CustomGlyphUsage { get; } = new();

    private string GetOrAddCustomFontAlias(TrueType.CustomFontVariant variant)
    {
        if (_customFontAliasByVariant.TryGetValue(variant, out string? existing))
            return existing;

        string alias = $"Cf{_customFontAliasByVariant.Count + 1}";
        _customFontAliasByVariant[variant] = alias;
        CustomFontObjects[alias] = variant;
        return alias;
    }

    private void RecordCustomGlyphUsage(TrueType.CustomFontVariant variant, ushort glyphId, int codepoint)
    {
        if (!CustomGlyphUsage.TryGetValue(variant, out var map))
            CustomGlyphUsage[variant] = map = new Dictionary<ushort, int>();
        map.TryAdd(glyphId, codepoint);
    }

    // --------------------------------------------------------------
    //  Drawing operations (primitive overloads - used by new API)
    // --------------------------------------------------------------

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

    /// <summary>
    /// Fills many same-colour rectangles as a single path: one colour operator,
    /// one <c>re</c> per rectangle, one trailing <c>f</c>. Used by barcode/QR
    /// rendering where a symbol can have thousands of same-colour modules —
    /// avoids emitting a redundant colour-set + fill pair per module.
    /// Rectangles with non-positive width or height are skipped. No-op when
    /// <paramref name="rects"/> is empty.
    /// </summary>
    internal void AddFilledRects(IEnumerable<(double X, double Y, double W, double H)> rects, PdfColor fillColor)
    {
        bool wroteColor = false;
        bool wroteAny   = false;
        foreach (var (x, y, w, h) in rects)
        {
            if (w <= 0 || h <= 0) continue;
            if (!wroteColor)
            {
                _ops.Append(CultureInfo.InvariantCulture, $"{C(fillColor.R)} {C(fillColor.G)} {C(fillColor.B)} rg\n");
                wroteColor = true;
            }
            double pdfY = Height - y - h;
            _ops.Append(CultureInfo.InvariantCulture, $"{F(x)} {F(pdfY)} {F(w)} {F(h)} re\n");
            wroteAny = true;
        }
        if (wroteAny) _ops.Append("f\n");
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
    //  Ellipse primitives
    // --------------------------------------------------------------

    // Appends a closed ellipse path using four cubic Bézier arcs.
    // cx, cy are the centre in caller (top-left) coordinates.
    private void AppendEllipsePath(double cx, double cy, double rx, double ry)
    {
        // Convert centre from top-left to PDF bottom-left origin
        double pdfCy = Height - cy;
        double kx = rx * _bezierArcK;
        double ky = ry * _bezierArcK;

        _ops.Append(CultureInfo.InvariantCulture, $"{F(cx + rx)} {F(pdfCy)} m\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{F(cx + rx)} {F(pdfCy + ky)} {F(cx + kx)} {F(pdfCy + ry)} {F(cx)} {F(pdfCy + ry)} c\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{F(cx - kx)} {F(pdfCy + ry)} {F(cx - rx)} {F(pdfCy + ky)} {F(cx - rx)} {F(pdfCy)} c\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{F(cx - rx)} {F(pdfCy - ky)} {F(cx - kx)} {F(pdfCy - ry)} {F(cx)} {F(pdfCy - ry)} c\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{F(cx + kx)} {F(pdfCy - ry)} {F(cx + rx)} {F(pdfCy - ky)} {F(cx + rx)} {F(pdfCy)} c\n");
        _ops.Append("h\n");
    }

    /// <summary>Draws a stroked ellipse.</summary>
    internal void AddStrokedEllipse(double cx, double cy, double rx, double ry,
        PdfColor strokeColor, double lineWidth = 1)
    {
        _ops.Append(CultureInfo.InvariantCulture, $"{F(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{C(strokeColor.R)} {C(strokeColor.G)} {C(strokeColor.B)} RG\n");
        AppendEllipsePath(cx, cy, rx, ry);
        _ops.Append("S\n");
    }

    /// <summary>Draws a filled ellipse (no border).</summary>
    internal void AddFilledEllipse(double cx, double cy, double rx, double ry,
        PdfColor fillColor)
    {
        _ops.Append(CultureInfo.InvariantCulture, $"{C(fillColor.R)} {C(fillColor.G)} {C(fillColor.B)} rg\n");
        AppendEllipsePath(cx, cy, rx, ry);
        _ops.Append("f\n");
    }

    /// <summary>Draws a filled and stroked ellipse.</summary>
    internal void AddFilledAndStrokedEllipse(double cx, double cy, double rx, double ry,
        PdfColor fillColor, PdfColor strokeColor, double lineWidth = 1)
    {
        _ops.Append(CultureInfo.InvariantCulture, $"{F(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{C(strokeColor.R)} {C(strokeColor.G)} {C(strokeColor.B)} RG\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{C(fillColor.R)} {C(fillColor.G)} {C(fillColor.B)} rg\n");
        AppendEllipsePath(cx, cy, rx, ry);
        _ops.Append("B\n");
    }

    // --------------------------------------------------------------
    //  Arbitrary path primitives (used by CanvasElement / PathDescriptor)
    // --------------------------------------------------------------

    /// <summary>
    /// Begins a new path sequence.  Sets stroke/fill colours and line width
    /// if the corresponding paint is requested.
    /// </summary>
    internal void BeginPath(
        PdfColor? fillColor, PdfColor? strokeColor, double lineWidth, bool evenOdd)
    {
        if (strokeColor.HasValue)
        {
            _ops.Append(CultureInfo.InvariantCulture, $"{F(lineWidth)} w\n");
            var sc = strokeColor.Value;
            _ops.Append(CultureInfo.InvariantCulture, $"{C(sc.R)} {C(sc.G)} {C(sc.B)} RG\n");
        }
        if (fillColor.HasValue)
        {
            var fc = fillColor.Value;
            _ops.Append(CultureInfo.InvariantCulture, $"{C(fc.R)} {C(fc.G)} {C(fc.B)} rg\n");
        }
    }

    /// <summary>Appends a moveto operator (top-left origin, Y flipped internally).</summary>
    internal void PathMoveTo(double x, double y)
    {
        double pdfY = Height - y;
        _ops.Append(CultureInfo.InvariantCulture, $"{F(x)} {F(pdfY)} m\n");
    }

    /// <summary>Appends a lineto operator.</summary>
    internal void PathLineTo(double x, double y)
    {
        double pdfY = Height - y;
        _ops.Append(CultureInfo.InvariantCulture, $"{F(x)} {F(pdfY)} l\n");
    }

    /// <summary>Appends a cubic Bézier curveto operator.</summary>
    internal void PathCurveTo(
        double cx1, double cy1,
        double cx2, double cy2,
        double x,   double y)
    {
        double pdfCy1 = Height - cy1;
        double pdfCy2 = Height - cy2;
        double pdfY   = Height - y;
        _ops.Append(CultureInfo.InvariantCulture,
            $"{F(cx1)} {F(pdfCy1)} {F(cx2)} {F(pdfCy2)} {F(x)} {F(pdfY)} c\n");
    }

    /// <summary>Closes the current subpath.</summary>
    internal void PathClose() => _ops.Append("h\n");

    /// <summary>
    /// Ends a path sequence by emitting the appropriate paint operator
    /// (fill, stroke, fill+stroke, or no-op if neither is set).
    /// </summary>
    internal void EndPath(PdfColor? fillColor, PdfColor? strokeColor, bool evenOdd)
    {
        if (fillColor.HasValue && strokeColor.HasValue)
            _ops.Append(evenOdd ? "B*\n" : "B\n");
        else if (fillColor.HasValue)
            _ops.Append(evenOdd ? "f*\n" : "f\n");
        else if (strokeColor.HasValue)
            _ops.Append("S\n");
        // else: path with no paint — just discard with n (no-op)
        else
            _ops.Append("n\n");
    }

    // --------------------------------------------------------------
    //  Link annotations
    // --------------------------------------------------------------

    /// <summary>A clickable URI annotation rectangle in top-left-origin coordinates.</summary>
    internal readonly record struct LinkAnnotation(double X, double Y, double Width, double Height, string Url);

    /// <summary>A clickable internal link (GoTo) annotation rectangle in top-left-origin coordinates.</summary>
    internal readonly record struct InternalLinkAnnotation(double X, double Y, double Width, double Height, int PageNumber, double? Top);

    /// <summary>Link annotations registered on this page by <see cref="Elements.Link"/> elements.</summary>
    internal List<LinkAnnotation> LinkAnnotations { get; } = [];

    /// <summary>Internal link annotations (GoTo) registered on this page by <see cref="Elements.InternalLinkElement"/>.</summary>
    internal List<InternalLinkAnnotation> InternalLinkAnnotations { get; } = [];

    /// <summary>Registers a URI annotation that covers the given bounding box.</summary>
    internal void AddLinkAnnotation(double x, double y, double width, double height, string url) =>
        LinkAnnotations.Add(new LinkAnnotation(x, y, width, height, url));

    /// <summary>Registers an internal link annotation that jumps to a specific page and position.</summary>
    internal void AddInternalLinkAnnotation(double x, double y, double width, double height, int pageNumber, double? top) =>
        InternalLinkAnnotations.Add(new InternalLinkAnnotation(x, y, width, height, pageNumber, top));

    // --------------------------------------------------------------
    //  Image XObjects
    // --------------------------------------------------------------

    /// <summary>
    /// Images registered for this page, keyed by alias.
    /// <list type="bullet">
    ///   <item>PNG  - <c>IsJpeg = false</c>, <c>Data</c> = flat decoded RGB bytes, <c>Components = 3</c>,
    ///          <c>Alpha</c> = optional 8-bit alpha channel for a /SMask.</item>
    ///   <item>JPEG - <c>IsJpeg = true</c>,  <c>Data</c> = raw JPEG file bytes (no decoding needed).</item>
    /// </list>
    /// Populated by <see cref="DrawImage"/> and consumed by <see cref="PdfDocument"/>.
    /// </summary>
    internal readonly Dictionary<string, (byte[] Data, int Width, int Height, bool IsJpeg, int Components, byte[]? Alpha)> ImageObjects = new();

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
    /// <param name="alpha">Optional 8-bit alpha channel (one byte per pixel) emitted as a /SMask.</param>
    internal void DrawImage(string alias, byte[] data, int imgW, int imgH,
        double x, double y, double drawW, double drawH,
        bool isJpeg = false, int components = 3, byte[]? alpha = null)
    {
        // Register image data once per alias per page
        ImageObjects.TryAdd(alias, (data, imgW, imgH, isJpeg, components, alpha));

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

    /// <summary>
    /// Encodes a string for use inside a PDF literal string  ( … )  in the content stream.
    /// <list type="bullet">
    ///   <item>Backslash, '(' and ')' are backslash-escaped as per PDF spec §7.3.4.2.</item>
    ///   <item>
    ///     Characters with a WinAnsiEncoding byte value are emitted as octal escapes
    ///     (<c>\nnn</c>) for any byte outside printable ASCII (0x20–0x7E), ensuring the
    ///     content stream stays pure ASCII while letting the reader look up the correct
    ///     glyph via the font's /WinAnsiEncoding.
    ///   </item>
    ///   <item>
    ///     Characters with no WinAnsiEncoding representation (CJK, Arabic, etc.) are
    ///     substituted with a question mark glyph (<c>?</c>).
    ///   </item>
    /// </list>
    /// </summary>
    internal static string EscapeForPdfString(string s)
    {
        var sb = new StringBuilder(s.Length * 2);
        foreach (char c in s)
        {
            if (WinAnsiEncoding.TryGetByte(c, out byte b))
            {
                if (b == (byte)'\\') { sb.Append("\\\\"); }
                else if (b == (byte)'(') { sb.Append("\\("); }
                else if (b == (byte)')') { sb.Append("\\)"); }
                else if (b >= 0x20 && b <= 0x7E) { sb.Append((char)b); }      // printable ASCII — emit directly
                else { sb.Append('\\'); sb.Append(Convert.ToString(b, 8).PadLeft(3, '0')); } // octal escape
            }
            else
            {
                sb.Append('?'); // no WinAnsi representation — substitution glyph
            }
        }
        return sb.ToString();
    }
}
