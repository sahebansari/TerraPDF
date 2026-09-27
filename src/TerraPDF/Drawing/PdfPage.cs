using System.Buffers;
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

    public double Width { get; }
    public double Height { get; }

    internal PdfPage(double width, double height)
    {
        Width = width;
        Height = height;
    }

    // --------------------------------------------------------------
    //  Text objects (BeginTextObject … ShowTextAt* … EndTextObject)
    // --------------------------------------------------------------

    // State inside the current text object. Font/colour are re-emitted only
    // when they change between ShowTextAt calls; the Td origin is tracked so
    // each positioning operator is a small relative move.
    private string? _textFontAlias;
    private double _textFontSize;
    private PdfColor? _textColor;
    private double _textTdX;
    private double _textTdY;

    /// <summary>
    /// Opens a text object (<c>BT</c>). All state is reset — the PDF text
    /// matrix resets at BT, and resetting font/colour too keeps the tracker
    /// immune to graphics-state changes made by non-text operators in between.
    /// </summary>
    internal void BeginTextObject()
    {
        _ops.Append("BT\n");
        _textFontAlias = null;
        _textFontSize = 0;
        _textColor = null;
        _textTdX = 0;
        _textTdY = 0;
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
        _ops.Append('(');
        AppendEscapedPdfString(_ops, text);
        _ops.Append(") Tj\n");
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
        AppendIdentityHHex(text, variant);
        _ops.Append(" Tj\n");
    }

    /// <summary>Shows text at a baseline rotated clockwise in the caller's top-left coordinate system.</summary>
    internal void ShowTextAtRotated(string text, double x, double y, double fontSize,
        PdfColor color, double angle, PdfFontFamily family = PdfFontFamily.Helvetica,
        bool bold = false, bool italic = false)
    {
        string fontAlias = PdfFonts.Alias(family, bold, italic);
        EmitFontAndColor(fontAlias, fontSize, color);
        double radians = angle * Math.PI / 180.0;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        double pdfX = Math.Round(x, 2);
        double pdfY = Math.Round(Height - y, 2);
        _ops.Append(CultureInfo.InvariantCulture,
            $"{M(cos)} {M(-sin)} {M(sin)} {M(cos)} {PdfReal.F2(pdfX)} {PdfReal.F2(pdfY)} Tm\n");
        _ops.Append('(');
        AppendEscapedPdfString(_ops, text);
        _ops.Append(") Tj\n");
    }

    internal void ShowTextAtRotated(string text, double x, double y, double fontSize,
        PdfColor color, double angle, TrueType.CustomFontVariant variant)
    {
        string fontAlias = GetOrAddCustomFontAlias(variant);
        EmitFontAndColor(fontAlias, fontSize, color);
        double radians = angle * Math.PI / 180.0;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        double pdfX = Math.Round(x, 2);
        double pdfY = Math.Round(Height - y, 2);
        _ops.Append(CultureInfo.InvariantCulture,
            $"{M(cos)} {M(-sin)} {M(sin)} {M(cos)} {PdfReal.F2(pdfX)} {PdfReal.F2(pdfY)} Tm\n");
        AppendIdentityHHex(text, variant);
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
        EmitFontAndColor(fontAlias, fontSize, color);

        // Td moves relative to the previous text-line origin. Deltas are taken
        // between rounded absolute positions so rounding never accumulates.
        double pdfX = Math.Round(x, 2);
        double pdfY = Math.Round(Height - y, 2);
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(pdfX - _textTdX)} {PdfReal.F2(pdfY - _textTdY)} Td\n");
        _textTdX = pdfX;
        _textTdY = pdfY;
    }

    private void EmitFontAndColor(string fontAlias, double fontSize, PdfColor color)
    {
        if (_textColor is null || !_textColor.Value.Equals(color))
        {
            _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(color.R)} {PdfReal.F4(color.G)} {PdfReal.F4(color.B)} rg\n");
            _textColor = color;
        }

        if (fontAlias != _textFontAlias || fontSize != _textFontSize)
        {
            _ops.Append(CultureInfo.InvariantCulture, $"/{fontAlias} {PdfReal.F2(fontSize)} Tf\n");
            _textFontAlias = fontAlias;
            _textFontSize = fontSize;
        }
    }

    /// <summary>
    /// Appends <paramref name="text"/> as an Identity-H hex string token, e.g. <c>&lt;0003001A&gt;</c>.
    /// Codepoints are decoded and Devanagari-reordered via <see cref="TrueType.DevanagariReordering.DecodeAndReorder"/>,
    /// then mapped to glyphs (with conjunct-ligature substitution) via
    /// <see cref="TrueType.DevanagariConjuncts.MapToGlyphs"/>, so glyph order here always
    /// matches what <see cref="TrueType.CustomFontVariant.MeasureWidth"/> measured.
    /// </summary>
    private void AppendIdentityHHex(string text, TrueType.CustomFontVariant variant)
    {
        _ops.Append('<');
        if (!TrueType.CustomFontVariant.NeedsShaping(text))
        {
            // Fast path mirroring CustomFontVariant.MeasureWidth: one glyph per scalar.
            for (int i = 0; i < text.Length; i++)
            {
                int codepoint = TrueType.CustomFontVariant.NextScalar(text, ref i);
                AppendGlyph(variant.Font.GetGlyphId(codepoint), codepoint);
            }
        }
        else
        {
            var codepoints = TrueType.DevanagariReordering.DecodeAndReorder(text);
            foreach (var (gid, codepoint) in TrueType.DevanagariConjuncts.MapToGlyphs(codepoints, variant.Font))
                AppendGlyph(gid, codepoint);
        }
        _ops.Append('>');

        void AppendGlyph(ushort gid, int codepoint)
        {
            RecordCustomGlyphUsage(variant, gid, codepoint);
            _ops.Append(HexDigits[gid >> 12]).Append(HexDigits[(gid >> 8) & 0xF])
                .Append(HexDigits[(gid >> 4) & 0xF]).Append(HexDigits[gid & 0xF]);
        }
    }

    private const string HexDigits = "0123456789ABCDEF";

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
    //  Graphics state (constant alpha) resources
    // --------------------------------------------------------------

    private readonly Dictionary<double, string> _extGStateAliasByOpacity = new();

    /// <summary>Constant-alpha ExtGState resources used on this page: page-local alias → opacity (sets both /ca and /CA).</summary>
    internal Dictionary<string, double> ExtGStateObjects { get; } = new();

    /// <summary>
    /// Returns the page-local alias for an <c>/ExtGState</c> resource setting both
    /// <c>/ca</c> and <c>/CA</c> to <paramref name="opacity"/>, reusing an existing
    /// alias for the same (3-decimal-rounded) value.
    /// </summary>
    private string GetOrAddExtGStateAlias(double opacity)
    {
        double rounded = Math.Round(opacity, 3);
        if (_extGStateAliasByOpacity.TryGetValue(rounded, out string? existing))
            return existing;

        string alias = $"GS{_extGStateAliasByOpacity.Count + 1}";
        _extGStateAliasByOpacity[rounded] = alias;
        ExtGStateObjects[alias] = rounded;
        return alias;
    }

    /// <summary>
    /// Opens a <c>q</c>/<c>/GSx gs</c> scope when <paramref name="opacity"/> is below
    /// full opacity, so the caller's paint operators draw translucent without leaking
    /// that state into whatever draws after — mirrors the <c>q</c>/<c>Q</c> scoping
    /// <see cref="DrawImage"/> already uses. Returns whether a scope was opened, so the
    /// caller knows whether to close it with a matching <c>Q</c>.
    /// </summary>
    internal bool BeginOpacityScope(double opacity)
    {
        if (opacity >= 1) return false;
        _ops.Append("q\n");
        _ops.Append(CultureInfo.InvariantCulture, $"/{GetOrAddExtGStateAlias(opacity)} gs\n");
        return true;
    }

    internal void EndOpacityScope(bool opened)
    {
        if (opened) _ops.Append("Q\n");
    }

    internal bool BeginDashScope(double[]? dashPattern, double dashPhase)
    {
        if (dashPattern is null) return false;
        _ops.Append("q\n[");
        for (var index = 0; index < dashPattern.Length; index++)
        {
            if (index > 0) _ops.Append(' ');
            _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(dashPattern[index])}");
        }
        _ops.Append(CultureInfo.InvariantCulture, $"] {PdfReal.F2(dashPhase)} d\n");
        return true;
    }

    internal void EndDashScope(bool opened)
    {
        if (opened) _ops.Append("Q\n");
    }

    // --------------------------------------------------------------
    //  Drawing operations (primitive overloads - used by new API)
    // --------------------------------------------------------------

    internal void AddLine(double x1, double y1, double x2, double y2,
        PdfColor color, double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        bool dashScope = BeginDashScope(dashPattern, dashPhase);
        bool scope = BeginOpacityScope(opacity);
        // Flip both endpoints from top-left to bottom-left origin
        double pdfY1 = Height - y1;
        double pdfY2 = Height - y2;
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(color.R)} {PdfReal.F4(color.G)} {PdfReal.F4(color.B)} RG\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(x1)} {PdfReal.F2(pdfY1)} m\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(x2)} {PdfReal.F2(pdfY2)} l\n");
        _ops.Append("S\n");
        EndOpacityScope(scope);
        EndDashScope(dashScope);
    }

    /// <summary>Draws a filled rectangle (no border).</summary>
    internal void AddFilledRect(double x, double y, double w, double h, PdfColor fillColor, double opacity = 1)
    {
        bool scope = BeginOpacityScope(opacity);
        // PDF rect origin is bottom-left corner, so shift by h after flipping Y
        double pdfY = Height - y - h;
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(fillColor.R)} {PdfReal.F4(fillColor.G)} {PdfReal.F4(fillColor.B)} rg\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(x)} {PdfReal.F2(pdfY)} {PdfReal.F2(w)} {PdfReal.F2(h)} re\n");
        _ops.Append("f\n");
        EndOpacityScope(scope);
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
        bool wroteAny = false;
        foreach (var (x, y, w, h) in rects)
        {
            if (w <= 0 || h <= 0) continue;
            if (!wroteColor)
            {
                _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(fillColor.R)} {PdfReal.F4(fillColor.G)} {PdfReal.F4(fillColor.B)} rg\n");
                wroteColor = true;
            }
            double pdfY = Height - y - h;
            _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(x)} {PdfReal.F2(pdfY)} {PdfReal.F2(w)} {PdfReal.F2(h)} re\n");
            wroteAny = true;
        }
        if (wroteAny) _ops.Append("f\n");
    }

    /// <summary>Draws a stroked (outline-only) rectangle.</summary>
    internal void AddStrokedRect(double x, double y, double w, double h,
        PdfColor strokeColor, double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        bool dashScope = BeginDashScope(dashPattern, dashPhase);
        bool scope = BeginOpacityScope(opacity);
        double pdfY = Height - y - h;
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(strokeColor.R)} {PdfReal.F4(strokeColor.G)} {PdfReal.F4(strokeColor.B)} RG\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(x)} {PdfReal.F2(pdfY)} {PdfReal.F2(w)} {PdfReal.F2(h)} re\n");
        _ops.Append("S\n");
        EndOpacityScope(scope);
        EndDashScope(dashScope);
    }

    /// <summary>Draws a filled-and-stroked rectangle.</summary>
    internal void AddRect(double x, double y, double w, double h,
        PdfColor fillColor, PdfColor strokeColor, double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        bool dashScope = BeginDashScope(dashPattern, dashPhase);
        bool scope = BeginOpacityScope(opacity);
        double pdfY = Height - y - h;
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(strokeColor.R)} {PdfReal.F4(strokeColor.G)} {PdfReal.F4(strokeColor.B)} RG\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(fillColor.R)} {PdfReal.F4(fillColor.G)} {PdfReal.F4(fillColor.B)} rg\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(x)} {PdfReal.F2(pdfY)} {PdfReal.F2(w)} {PdfReal.F2(h)} re\n");
        _ops.Append("B\n");
        EndOpacityScope(scope);
        EndDashScope(dashScope);
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
        double b = Height - y - h;   // bottom Y in PDF coords
        double t = Height - y;       // top    Y in PDF coords
        double k = r * _bezierArcK;

        // Start at top-left corner, just right of the top-left arc
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(x + r)} {PdfReal.F2(t)} m\n");

        // Top edge → top-right arc
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(x + w - r)} {PdfReal.F2(t)} l\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{PdfReal.F2(x + w - r + k)} {PdfReal.F2(t)} {PdfReal.F2(x + w)} {PdfReal.F2(t - r + k)} {PdfReal.F2(x + w)} {PdfReal.F2(t - r)} c\n");

        // Right edge → bottom-right arc
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(x + w)} {PdfReal.F2(b + r)} l\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{PdfReal.F2(x + w)} {PdfReal.F2(b + r - k)} {PdfReal.F2(x + w - r + k)} {PdfReal.F2(b)} {PdfReal.F2(x + w - r)} {PdfReal.F2(b)} c\n");

        // Bottom edge → bottom-left arc
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(x + r)} {PdfReal.F2(b)} l\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{PdfReal.F2(x + r - k)} {PdfReal.F2(b)} {PdfReal.F2(x)} {PdfReal.F2(b + r - k)} {PdfReal.F2(x)} {PdfReal.F2(b + r)} c\n");

        // Left edge → top-left arc → close
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(x)} {PdfReal.F2(t - r)} l\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{PdfReal.F2(x)} {PdfReal.F2(t - r + k)} {PdfReal.F2(x + r - k)} {PdfReal.F2(t)} {PdfReal.F2(x + r)} {PdfReal.F2(t)} c\n");

        _ops.Append("h\n");
    }

    /// <summary>Draws a stroked rounded rectangle.</summary>
    internal void AddRoundedRect(double x, double y, double w, double h,
        double radius, PdfColor strokeColor, double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        bool dashScope = BeginDashScope(dashPattern, dashPhase);
        bool scope = BeginOpacityScope(opacity);
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(strokeColor.R)} {PdfReal.F4(strokeColor.G)} {PdfReal.F4(strokeColor.B)} RG\n");
        AppendRoundedRectPath(x, y, w, h, radius);
        _ops.Append("S\n");
        EndOpacityScope(scope);
        EndDashScope(dashScope);
    }

    /// <summary>Draws a filled rounded rectangle (no border).</summary>
    internal void AddFilledRoundedRect(double x, double y, double w, double h,
        double radius, PdfColor fillColor, double opacity = 1)
    {
        bool scope = BeginOpacityScope(opacity);
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(fillColor.R)} {PdfReal.F4(fillColor.G)} {PdfReal.F4(fillColor.B)} rg\n");
        AppendRoundedRectPath(x, y, w, h, radius);
        _ops.Append("f\n");
        EndOpacityScope(scope);
    }

    /// <summary>Draws a filled-and-stroked rounded rectangle.</summary>
    internal void AddFilledAndStrokedRoundedRect(double x, double y, double w, double h,
        double radius, PdfColor fillColor, PdfColor strokeColor, double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        bool dashScope = BeginDashScope(dashPattern, dashPhase);
        bool scope = BeginOpacityScope(opacity);
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(strokeColor.R)} {PdfReal.F4(strokeColor.G)} {PdfReal.F4(strokeColor.B)} RG\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(fillColor.R)} {PdfReal.F4(fillColor.G)} {PdfReal.F4(fillColor.B)} rg\n");
        AppendRoundedRectPath(x, y, w, h, radius);
        _ops.Append("B\n");
        EndOpacityScope(scope);
        EndDashScope(dashScope);
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

        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(cx + rx)} {PdfReal.F2(pdfCy)} m\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{PdfReal.F2(cx + rx)} {PdfReal.F2(pdfCy + ky)} {PdfReal.F2(cx + kx)} {PdfReal.F2(pdfCy + ry)} {PdfReal.F2(cx)} {PdfReal.F2(pdfCy + ry)} c\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{PdfReal.F2(cx - kx)} {PdfReal.F2(pdfCy + ry)} {PdfReal.F2(cx - rx)} {PdfReal.F2(pdfCy + ky)} {PdfReal.F2(cx - rx)} {PdfReal.F2(pdfCy)} c\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{PdfReal.F2(cx - rx)} {PdfReal.F2(pdfCy - ky)} {PdfReal.F2(cx - kx)} {PdfReal.F2(pdfCy - ry)} {PdfReal.F2(cx)} {PdfReal.F2(pdfCy - ry)} c\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{PdfReal.F2(cx + kx)} {PdfReal.F2(pdfCy - ry)} {PdfReal.F2(cx + rx)} {PdfReal.F2(pdfCy - ky)} {PdfReal.F2(cx + rx)} {PdfReal.F2(pdfCy)} c\n");
        _ops.Append("h\n");
    }

    /// <summary>Draws a stroked ellipse.</summary>
    internal void AddStrokedEllipse(double cx, double cy, double rx, double ry,
        PdfColor strokeColor, double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        bool dashScope = BeginDashScope(dashPattern, dashPhase);
        bool scope = BeginOpacityScope(opacity);
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(strokeColor.R)} {PdfReal.F4(strokeColor.G)} {PdfReal.F4(strokeColor.B)} RG\n");
        AppendEllipsePath(cx, cy, rx, ry);
        _ops.Append("S\n");
        EndOpacityScope(scope);
        EndDashScope(dashScope);
    }

    /// <summary>Draws a filled ellipse (no border).</summary>
    internal void AddFilledEllipse(double cx, double cy, double rx, double ry,
        PdfColor fillColor, double opacity = 1)
    {
        bool scope = BeginOpacityScope(opacity);
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(fillColor.R)} {PdfReal.F4(fillColor.G)} {PdfReal.F4(fillColor.B)} rg\n");
        AppendEllipsePath(cx, cy, rx, ry);
        _ops.Append("f\n");
        EndOpacityScope(scope);
    }

    /// <summary>Draws a filled and stroked ellipse.</summary>
    internal void AddFilledAndStrokedEllipse(double cx, double cy, double rx, double ry,
        PdfColor fillColor, PdfColor strokeColor, double lineWidth = 1, double opacity = 1,
        double[]? dashPattern = null, double dashPhase = 0)
    {
        bool dashScope = BeginDashScope(dashPattern, dashPhase);
        bool scope = BeginOpacityScope(opacity);
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(lineWidth)} w\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(strokeColor.R)} {PdfReal.F4(strokeColor.G)} {PdfReal.F4(strokeColor.B)} RG\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(fillColor.R)} {PdfReal.F4(fillColor.G)} {PdfReal.F4(fillColor.B)} rg\n");
        AppendEllipsePath(cx, cy, rx, ry);
        _ops.Append("B\n");
        EndOpacityScope(scope);
        EndDashScope(dashScope);
    }

    // --------------------------------------------------------------
    //  Arbitrary path primitives (used by CanvasElement / PathDescriptor)
    // --------------------------------------------------------------

    /// <summary>
    /// Begins a new path sequence.  Sets stroke/fill colours and line width
    /// if the corresponding paint is requested. Returns whether an opacity
    /// scope was opened (<paramref name="opacity"/> &lt; 1) — pass that
    /// return value as the matching <see cref="EndPath"/> call's
    /// <c>opacityScopeOpen</c> argument so it closes the scope.
    /// </summary>
    internal bool BeginPath(
        PdfColor? fillColor, PdfColor? strokeColor, double lineWidth, bool evenOdd, double opacity = 1)
    {
        bool scope = BeginOpacityScope(opacity);
        if (strokeColor.HasValue)
        {
            _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(lineWidth)} w\n");
            var sc = strokeColor.Value;
            _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(sc.R)} {PdfReal.F4(sc.G)} {PdfReal.F4(sc.B)} RG\n");
        }
        if (fillColor.HasValue)
        {
            var fc = fillColor.Value;
            _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F4(fc.R)} {PdfReal.F4(fc.G)} {PdfReal.F4(fc.B)} rg\n");
        }
        return scope;
    }

    /// <summary>Appends a moveto operator (top-left origin, Y flipped internally).</summary>
    internal void PathMoveTo(double x, double y)
    {
        double pdfY = Height - y;
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(x)} {PdfReal.F2(pdfY)} m\n");
    }

    /// <summary>Appends a lineto operator.</summary>
    internal void PathLineTo(double x, double y)
    {
        double pdfY = Height - y;
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(x)} {PdfReal.F2(pdfY)} l\n");
    }

    /// <summary>Appends a cubic Bézier curveto operator.</summary>
    internal void PathCurveTo(
        double cx1, double cy1,
        double cx2, double cy2,
        double x, double y)
    {
        double pdfCy1 = Height - cy1;
        double pdfCy2 = Height - cy2;
        double pdfY = Height - y;
        _ops.Append(CultureInfo.InvariantCulture,
            $"{PdfReal.F2(cx1)} {PdfReal.F2(pdfCy1)} {PdfReal.F2(cx2)} {PdfReal.F2(pdfCy2)} {PdfReal.F2(x)} {PdfReal.F2(pdfY)} c\n");
    }

    /// <summary>Closes the current subpath.</summary>
    internal void PathClose() => _ops.Append("h\n");

    /// <summary>
    /// Ends a path sequence by emitting the appropriate paint operator
    /// (fill, stroke, fill+stroke, or no-op if neither is set).
    /// </summary>
    internal void EndPath(PdfColor? fillColor, PdfColor? strokeColor, bool evenOdd, bool opacityScopeOpen = false)
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
        EndOpacityScope(opacityScopeOpen);
    }

    // --------------------------------------------------------------
    //  Gradient (shading) resources
    // --------------------------------------------------------------

    /// <summary>
    /// A two-stop shading in PDF user space (bottom-left origin). Axial (type 2) uses
    /// (X0,Y0)-(X1,Y1); radial (type 3) uses circles (X0,Y0,R0) and (X1,Y1,R1).
    /// </summary>
    internal readonly record struct ShadingSpec(
        bool Radial, PdfColor From, PdfColor To,
        double X0, double Y0, double R0, double X1, double Y1, double R1);

    /// <summary>Shading resources used on this page: page-local alias → definition.</summary>
    internal Dictionary<string, ShadingSpec> ShadingObjects { get; } = new();

    /// <summary>
    /// Paints a two-stop gradient over the current clipping path (<c>sh</c>). The
    /// caller has already appended the path and clipped it with <c>W n</c>, inside a
    /// <c>q</c>…<c>Q</c> pair. Coordinates are top-left origin, flipped here.
    /// </summary>
    internal void PaintShading(bool radial, PdfColor from, PdfColor to,
        double x0, double y0, double r0, double x1, double y1, double r1)
    {
        string alias = $"Sh{ShadingObjects.Count + 1}";
        ShadingObjects[alias] = new ShadingSpec(radial, from, to, x0, Height - y0, r0, x1, Height - y1, r1);
        _ops.Append(CultureInfo.InvariantCulture, $"/{alias} sh\n");
    }

    /// <summary>Opens a <c>q</c> scope, so a clipping path can be applied and later restored.</summary>
    internal void BeginClipScope() => _ops.Append("q\n");

    /// <summary>Clips to the path built so far (<c>W n</c>, or <c>W* n</c> for even-odd).</summary>
    internal void ClipToPath(bool evenOdd) => _ops.Append(evenOdd ? "W* n\n" : "W n\n");

    /// <summary>Closes a scope opened with <see cref="BeginClipScope"/>.</summary>
    internal void EndClipScope() => _ops.Append("Q\n");

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
    /// Images registered for this page, keyed by alias. Each value carries the original
    /// PNG/JPEG file bytes; <see cref="PdfDocument"/> decodes PNGs once per distinct image.
    /// Populated by <see cref="DrawImage"/> and consumed by <see cref="PdfDocument"/>.
    /// </summary>
    internal readonly Dictionary<string, ImageSource> ImageObjects = new();

    /// <summary>
    /// Registers the image under <paramref name="alias"/> (if not already present) and
    /// emits PDF content-stream operators that paint the image at the given position.
    /// </summary>
    /// <param name="alias">Resource name used in the page's /XObject dictionary (e.g. "Im1").</param>
    /// <param name="image">The image to paint.</param>
    /// <param name="x">Left edge in caller coordinates (top-left origin).</param>
    /// <param name="y">Top edge in caller coordinates (top-left origin).</param>
    /// <param name="drawW">Rendered width in PDF points.</param>
    /// <param name="drawH">Rendered height in PDF points.</param>
    internal void DrawImage(string alias, ImageSource image,
        double x, double y, double drawW, double drawH)
    {
        // Register the image once per alias per page
        ImageObjects.TryAdd(alias, image);

        // PDF images are placed via a Current Transformation Matrix (CTM):
        //   [scaleX 0 0 scaleY originX originY] cm
        // Then /AliasName Do paints a 1x1 unit image into that space.
        // Flip Y: PDF bottom-left origin means the image top = pageHeight - callerY,
        // and the rect origin sits at the bottom of the drawn area (top - drawH).
        double pdfY = Height - y - drawH;
        _ops.Append("q\n");
        _ops.Append(CultureInfo.InvariantCulture, $"{PdfReal.F2(drawW)} 0 0 {PdfReal.F2(drawH)} {PdfReal.F2(x)} {PdfReal.F2(pdfY)} cm\n");
        _ops.Append(CultureInfo.InvariantCulture, $"/{alias} Do\n");
        _ops.Append("Q\n");
    }

    internal void BeginClip(double x, double y, double width, double height)
    {
        double pdfY = Height - y - height;
        _ops.Append("q\n");
        _ops.Append(CultureInfo.InvariantCulture,
            $"{PdfReal.F2(x)} {PdfReal.F2(pdfY)} {PdfReal.F2(width)} {PdfReal.F2(height)} re W n\n");
    }

    internal void EndClip() => _ops.Append("Q\n");

    // --------------------------------------------------------------
    //  Serialization
    // --------------------------------------------------------------

    /// <summary>Length of the content stream in characters (= bytes once encoded).</summary>
    internal int ContentLength => _ops.Length;

    /// <summary>
    /// Writes the content stream to <paramref name="output"/> as Latin-1 bytes, encoding
    /// the operator buffer chunk by chunk instead of materialising it as one string and
    /// then one byte array.
    /// </summary>
    internal void WriteContentStream(Stream output)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            foreach (var chunk in _ops.GetChunks())
            {
                var chars = chunk.Span;
                while (!chars.IsEmpty)
                {
                    int count = Math.Min(chars.Length, buffer.Length); // Latin-1: one byte per char
                    int bytes = Encoding.Latin1.GetBytes(chars[..count], buffer);
                    output.Write(buffer, 0, bytes);
                    chars = chars[count..];
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // --------------------------------------------------------------
    //  Helpers
    // --------------------------------------------------------------

    // Coordinates and sizes are written as 2-decimal PDF reals ("72.00") and colour
    // components as 4-decimal ones ("0.5020"), formatted in place with {PdfReal.F2(v)} /
    // {PdfReal.F4(v)} inside _ops.Append(CultureInfo.InvariantCulture, $"...") — no
    // temporary strings, and exactly the text "F2"/"F4" would produce.

    /// <summary>
    /// Formats a text-matrix coefficient. Rotation cosines and sines need far more
    /// precision than the two decimals (F2) given to positions: at F2 a 0.3°
    /// rotation rounds to 0.57°, anything under ~0.3° collapses to no rotation at all,
    /// and the rounded (cos, sin) pair also rescales glyphs by up to ~0.5%.
    /// </summary>
    private static string M(double d)
    {
        double rounded = Math.Round(d, 6);
        // sin(180°) is ~1.2e-16, which would otherwise print as "-0.000000".
        if (rounded == 0) rounded = 0;
        return rounded.ToString("F6", CultureInfo.InvariantCulture);
    }

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
        AppendEscapedPdfString(sb, s);
        return sb.ToString();
    }

    /// <summary>
    /// Appends <paramref name="s"/> encoded as by <see cref="EscapeForPdfString"/>
    /// directly to <paramref name="sb"/>, without an intermediate string.
    /// </summary>
    private static void AppendEscapedPdfString(StringBuilder sb, string s)
    {
        foreach (char c in s)
        {
            if (WinAnsiEncoding.TryGetByte(c, out byte b))
            {
                if (b == (byte)'\\') { sb.Append("\\\\"); }
                else if (b == (byte)'(') { sb.Append("\\("); }
                else if (b == (byte)')') { sb.Append("\\)"); }
                else if (b >= 0x20 && b <= 0x7E) { sb.Append((char)b); }      // printable ASCII — emit directly
                else
                {
                    // octal escape, always three digits (\nnn)
                    sb.Append('\\')
                      .Append((char)('0' + (b >> 6)))
                      .Append((char)('0' + ((b >> 3) & 7)))
                      .Append((char)('0' + (b & 7)));
                }
            }
            else
            {
                sb.Append('?'); // no WinAnsi representation — substitution glyph
            }
        }
    }
}
