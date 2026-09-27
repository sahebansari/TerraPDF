using System.Security.Cryptography;

namespace TerraPDF.Drawing;

/// <summary>
/// An image as supplied by the caller: the original PNG or JPEG file bytes plus
/// the header fields layout needs. Pixel data is never decoded here.
/// <para>
/// PNG pixels are decoded by <see cref="PdfDocument"/> at save time, once per
/// distinct <see cref="ContentKey"/>, so an image placed on many pages (or by many
/// elements from the same bytes) costs a single decode instead of one per placement.
/// </para>
/// </summary>
internal sealed class ImageSource
{
    /// <summary>Original file bytes: PNG (decoded at save time) or JPEG (embedded verbatim via DCTDecode).</summary>
    internal byte[] Data { get; }
    internal int Width { get; }
    internal int Height { get; }
    internal bool IsJpeg { get; }

    /// <summary>Colour component count: 1 = grayscale, 3 = RGB, 4 = CMYK. Decoded PNGs are always RGB.</summary>
    internal int Components { get; }

    private string? _contentKey;

    private ImageSource(byte[] data, int width, int height, bool isJpeg, int components)
    {
        Data = data;
        Width = width;
        Height = height;
        IsJpeg = isJpeg;
        Components = components;
    }

    /// <summary>
    /// Reads the image header. The caller must not mutate <paramref name="data"/> afterwards.
    /// </summary>
    /// <exception cref="NotSupportedException">The data is neither PNG nor JPEG, or is a PNG variant the decoder does not support.</exception>
    internal static ImageSource FromBytes(byte[] data)
    {
        if (IsJpegData(data))
        {
            using var ms = new MemoryStream(data, writable: false);
            var info = JpegInfo.Read(ms);
            return new ImageSource(data, info.Width, info.Height, isJpeg: true, info.Components);
        }

        if (IsPngData(data))
        {
            var (width, height) = PngDecoder.ReadHeader(data);
            return new ImageSource(data, width, height, isJpeg: false, components: 3);
        }

        throw new NotSupportedException(
            "Image data is not a recognised PNG or JPEG (checked by magic bytes). " +
            "Only PNG and JPEG images are supported.");
    }

    internal static bool IsPngData(byte[] d) =>
        d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47;

    internal static bool IsJpegData(byte[] d) =>
        d.Length >= 2 && d[0] == 0xFF && d[1] == 0xD8;

    /// <summary>
    /// Content-identity key for document-wide deduplication: SHA-256 over the original
    /// file bytes, computed once. Hashing the compressed file is far cheaper than
    /// hashing decoded pixels, and identical files always decode identically.
    /// </summary>
    internal string ContentKey => _contentKey ??= Convert.ToHexString(SHA256.HashData(Data));

    /// <summary>Decodes PNG pixels to flat RGB plus an optional alpha channel (null when fully opaque).</summary>
    internal byte[] DecodePng(out byte[]? alpha) => PngDecoder.Decode(Data, out _, out _, out alpha);
}
