using TerraPDF.Drawing;
using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>
/// Renders a raster image from PNG or JPEG data (file path, byte array, or stream).
/// The format is detected from the data's magic bytes, not the file extension.
/// <list type="bullet">
///   <item><b>PNG</b>  — decoded to raw RGB and re-compressed with FlateDecode inside the PDF.
///                       RGBA transparency is preserved via a /SMask soft mask.</item>
///   <item><b>JPEG</b> — raw file bytes are embedded verbatim using PDF's native DCTDecode filter;
///                       no pixel decoding is performed, so it is fast and lossless.</item>
/// </list>
/// The image scales to fill the available width while preserving its aspect ratio;
/// when the available height is the binding constraint, both axes are scaled down
/// together so the image is never distorted.
/// </summary>
internal sealed class ImageElement : Element
{
    // Static counter used to generate a unique PDF resource alias per ImageElement instance
    private static int _aliasCounter;

    private readonly byte[]  _data;        // PNG: decoded RGB pixels | JPEG: raw file bytes
    private readonly byte[]? _alpha;       // PNG RGBA: 8-bit alpha channel (null when opaque / JPEG)
    private readonly int     _imgWidth;
    private readonly int     _imgHeight;
    private readonly string  _alias;       // PDF XObject resource name, e.g. "Im3"
    private readonly bool    _isJpeg;
    private readonly int     _components;  // colour component count (used to choose ColorSpace)
    private readonly double? _maxWidth;    // optional width cap in PDF points; null = fill available width

    /// <param name="filePath">Path to a PNG or JPEG file.</param>
    /// <param name="width">Maximum rendered width in PDF points; null = fill available width.</param>
    /// <exception cref="NotSupportedException">The data is neither PNG nor JPEG.</exception>
    internal ImageElement(string filePath, double? width = null)
        : this(File.ReadAllBytes(filePath), width)
    {
    }

    /// <param name="imageData">Raw PNG or JPEG file bytes.</param>
    /// <param name="width">Maximum rendered width in PDF points. When set the element reports
    ///   this width during measure, so wrapping in <c>AlignCenter()</c> centres it correctly.
    ///   When null the image fills the full available width.</param>
    /// <exception cref="NotSupportedException">The data is neither PNG nor JPEG.</exception>
    internal ImageElement(byte[] imageData, double? width = null)
    {
        if (IsJpegData(imageData))
        {
            // JPEG: read dimensions only - raw bytes are passed straight to PdfPage
            using var ms = new MemoryStream(imageData, writable: false);
            var info    = JpegInfo.Read(ms);
            _imgWidth   = info.Width;
            _imgHeight  = info.Height;
            _components = info.Components;
            _data       = imageData;
            _isJpeg     = true;
        }
        else if (IsPngData(imageData))
        {
            using var ms = new MemoryStream(imageData, writable: false);
            _data       = PngDecoder.Decode(ms, out _imgWidth, out _imgHeight, out _alpha);
            _components = 3;
            _isJpeg     = false;
        }
        else
        {
            throw new NotSupportedException(
                "Image data is not a recognised PNG or JPEG (checked by magic bytes). " +
                "Only PNG and JPEG images are supported.");
        }

        _maxWidth = width;
        // Each element gets a unique alias so multiple images on the same page don't collide
        _alias = $"Im{System.Threading.Interlocked.Increment(ref _aliasCounter)}";
    }

    private static bool IsPngData(byte[] d) =>
        d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47;

    private static bool IsJpegData(byte[] d) =>
        d.Length >= 2 && d[0] == 0xFF && d[1] == 0xD8;

    // -- Sizing ----------------------------------------------------

    /// <summary>
    /// Scaled draw size within (availW, availH): fill the width (capped at
    /// <see cref="_maxWidth"/>), and when the resulting height exceeds the
    /// available height shrink both axes so the aspect ratio is preserved.
    /// </summary>
    private (double W, double H) ScaledSize(double availW, double availH)
    {
        double aspect = (double)_imgHeight / _imgWidth;
        double w = _maxWidth.HasValue ? Math.Min(availW, _maxWidth.Value) : availW;
        double h = w * aspect;
        if (h > availH)
        {
            h = availH;
            w = h / aspect;
        }
        return (w, h);
    }

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint)
    {
        if (_imgWidth == 0) return new ElementSize(0, 0);
        var (dw, dh) = ScaledSize(w, h);
        return new ElementSize(dw, dh);
    }

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        if (_imgWidth == 0 || ctx.Width <= 0 || ctx.Height <= 0) return;

        var (drawW, drawH) = ScaledSize(ctx.Width, ctx.Height);

        ctx.Page.DrawImage(_alias, _data, _imgWidth, _imgHeight,
            ctx.X, ctx.Y, drawW, drawH,
            _isJpeg, _components, _alpha);
    }
}
