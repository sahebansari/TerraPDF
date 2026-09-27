using TerraPDF.Core;
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

    private readonly ImageSource _source;  // original file bytes + header size; PNG pixels decoded at save time
    private readonly int _imgWidth;
    private readonly int _imgHeight;
    private readonly string _alias;       // PDF XObject resource name, e.g. "Im3"
    private readonly double? _maxWidth;    // optional width cap in PDF points; null = fill available width

    internal (int Width, int Height) PixelSize => (_imgWidth, _imgHeight);

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
        // Only the header is read here; PNG pixels are decoded once per distinct
        // image when the document is saved (see PdfDocument).
        _source = ImageSource.FromBytes(imageData);
        _imgWidth = _source.Width;
        _imgHeight = _source.Height;

        _maxWidth = width;
        // Each element gets a unique alias so multiple images on the same page don't collide
        _alias = $"Im{System.Threading.Interlocked.Increment(ref _aliasCounter)}";
    }

    internal static void ValidateFormat(byte[] imageData)
    {
        if (!ImageSource.IsPngData(imageData) && !ImageSource.IsJpegData(imageData))
            throw new ArgumentException(
                "Image data is not a recognised PNG or JPEG (checked by magic bytes). " +
                "Only PNG and JPEG images are supported.", nameof(imageData));
    }

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

        ctx.Page.DrawImage(_alias, _source, ctx.X, ctx.Y, drawW, drawH);
    }

    internal void DrawAt(PdfPage page, double x, double y, double width, double height, ImageFit fit)
    {
        // Same guard as Draw: a zero-dimension image still clears the magic-byte
        // check, and dividing by it would write NaN operands into the content stream.
        if (_imgWidth == 0 || _imgHeight == 0 || width <= 0 || height <= 0) return;

        double imageAspect = (double)_imgWidth / _imgHeight;
        double targetAspect = width / height;
        double drawWidth = width;
        double drawHeight = height;
        double drawX = x;
        double drawY = y;

        if (fit == ImageFit.Contain)
        {
            if (imageAspect > targetAspect)
                drawHeight = width / imageAspect;
            else
                drawWidth = height * imageAspect;
            drawX += (width - drawWidth) / 2;
            drawY += (height - drawHeight) / 2;
        }
        else if (fit is ImageFit.Cover or ImageFit.CoverTopLeft or ImageFit.CropTopLeft)
        {
            if (fit is ImageFit.Cover or ImageFit.CoverTopLeft)
            {
                if (imageAspect > targetAspect)
                    drawWidth = height * imageAspect;
                else
                    drawHeight = width / imageAspect;
                if (fit == ImageFit.Cover)
                {
                    drawX += (width - drawWidth) / 2;
                    drawY += (height - drawHeight) / 2;
                }
            }
            else
            {
                drawWidth = _imgWidth * 72d / 96d;
                drawHeight = _imgHeight * 72d / 96d;
            }
            page.BeginClip(x, y, width, height);
        }

        page.DrawImage(_alias, _source, drawX, drawY, drawWidth, drawHeight);

        if (fit is ImageFit.Cover or ImageFit.CoverTopLeft or ImageFit.CropTopLeft)
            page.EndClip();
    }
}
