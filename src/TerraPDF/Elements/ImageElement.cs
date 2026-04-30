using TerraPDF.Drawing;
using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>
/// Renders a raster image from a PNG or JPEG file.
/// <list type="bullet">
///   <item><b>PNG</b>  — decoded to raw RGB and re-compressed with FlateDecode inside the PDF.</item>
///   <item><b>JPEG</b> — raw file bytes are embedded verbatim using PDF's native DCTDecode filter;
///                       no pixel decoding is performed, so it is fast and lossless.</item>
/// </list>
/// The image scales to fill the available width while preserving its aspect ratio.
/// </summary>
internal sealed class ImageElement : Element
{
    // Static counter used to generate a unique PDF resource alias per ImageElement instance
    private static int _aliasCounter;

    private readonly byte[]  _data;        // PNG: decoded RGB pixels | JPEG: raw file bytes
    private readonly int     _imgWidth;
    private readonly int     _imgHeight;
    private readonly string  _alias;       // PDF XObject resource name, e.g. "Im3"
    private readonly bool    _isJpeg;
    private readonly int     _components;  // colour component count (used to choose ColorSpace)
    private readonly double? _maxWidth;    // optional width cap in PDF points; null = fill available width

    /// <param name="filePath">Absolute path to a PNG or JPEG file.</param>
    /// <param name="width">Maximum rendered width in PDF points. When set the element reports
    ///   this width during measure, so wrapping in <c>AlignCenter()</c> centres it correctly.
    ///   When null the image fills the full available width.</param>
    /// <exception cref="NotSupportedException">The file extension is not .png, .jpg, or .jpeg.</exception>
    internal ImageElement(string filePath, double? width = null)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext == ".jpg" || ext == ".jpeg")
        {
            // JPEG: read dimensions only - raw bytes are passed straight to PdfPage
            var info   = JpegInfo.Read(filePath);
            _imgWidth  = info.Width;
            _imgHeight = info.Height;
            _components = info.Components;
            _data      = File.ReadAllBytes(filePath);
            _isJpeg    = true;
        }
        else if (ext == ".png")
        {
            _data      = PngDecoder.Decode(filePath, out _imgWidth, out _imgHeight);
            _components = 3;
            _isJpeg    = false;
        }
        else
        {
            throw new NotSupportedException(
                $"Image format '{ext}' is not supported. Use .png, .jpg, or .jpeg.");
        }

        _maxWidth = width;
        // Each element gets a unique alias so multiple images on the same page don't collide
        _alias = $"Im{System.Threading.Interlocked.Increment(ref _aliasCounter)}";
    }

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null)
    {
        if (_imgWidth == 0) return new ElementSize(0, 0);

        // Cap to maxWidth when provided; otherwise fill the available width
        double useW    = _maxWidth.HasValue ? Math.Min(w, _maxWidth.Value) : w;
        double scaledH = useW * ((double)_imgHeight / _imgWidth);
        return new ElementSize(useW, Math.Min(scaledH, h));
    }

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        if (_imgWidth == 0 || ctx.Width <= 0 || ctx.Height <= 0) return;

        // Mirror Measure: cap to maxWidth when provided, otherwise fill the available width.
        // Using ctx.Width alone would stretch the image to the parent container's full width
        // when the caller has requested a smaller fixed size.
        double drawW = _maxWidth.HasValue ? Math.Min(ctx.Width, _maxWidth.Value) : ctx.Width;
        double drawH = drawW * ((double)_imgHeight / _imgWidth);
        drawH = Math.Min(drawH, ctx.Height);

        ctx.Page.DrawImage(_alias, _data, _imgWidth, _imgHeight,
            ctx.X, ctx.Y, drawW, drawH,
            _isJpeg, _components);
    }
}

