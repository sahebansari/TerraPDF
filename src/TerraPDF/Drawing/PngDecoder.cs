using System.IO.Compression;

namespace TerraPDF.Drawing;

/// <summary>
/// Minimal pure-C# PNG decoder that produces flat 24-bit RGB pixel data plus an
/// optional alpha channel.
/// Supports color types 2 (RGB), 4 (grayscale+alpha), 6 (RGBA), and 3 (indexed/palette),
/// all at bit depth 8. Interlaced PNGs are not supported.
/// Alpha is extracted from colour types 4 and 6; indexed transparency (tRNS)
/// is not supported and decodes as opaque.
/// </summary>
internal static class PngDecoder
{
    // Standard 8-byte PNG file signature
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>
    /// Decodes a PNG and returns a flat row-major array of RGB bytes
    /// (3 bytes per pixel, top-to-bottom, left-to-right).
    /// <paramref name="alpha"/> receives one byte per pixel for images with an alpha
    /// channel that actually use transparency, or <c>null</c> when the image is fully opaque.
    /// </summary>
    /// <remarks>
    /// Allocation is limited to the decompressed image (unfiltered in place), the RGB
    /// output and, only when some pixel is transparent, the alpha plane. IDAT chunks are
    /// decompressed where they sit in <paramref name="png"/> rather than concatenated first.
    /// </remarks>
    internal static byte[] Decode(byte[] png, out int width, out int height, out byte[]? alpha)
    {
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException("Stream does not begin with a valid PNG signature.");

        int imgWidth = 0, imgHeight = 0, colorType = 0;
        byte[]? palette = null;
        var idat = new List<(int Offset, int Length)>();

        // Read chunks until IEND (or the end of the data)
        int pos = 8;
        while (pos + 8 <= png.Length)
        {
            int len = ReadBigEndianInt32(png, pos);
            if (len < 0 || pos + 12L + len > png.Length)
                throw new InvalidDataException("PNG chunk extends past the end of the data.");
            var type = png.AsSpan(pos + 4, 4);
            int data = pos + 8;

            if (type.SequenceEqual("IHDR"u8))
            {
                imgWidth  = ReadBigEndianInt32(png, data);
                imgHeight = ReadBigEndianInt32(png, data + 4);
                colorType = png[data + 9];
                ValidateHeader(bitDepth: png[data + 8], colorType, interlace: png[data + 12]);
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                // Palette: 3 bytes (R, G, B) per entry
                palette = png.AsSpan(data, len).ToArray();
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                idat.Add((data, len));
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                break;
            }

            pos = data + len + 4; // skip CRC
        }

        width = imgWidth;
        height = imgHeight;

        int srcBpp = BytesPerPixel(colorType);
        int stride = imgWidth * srcBpp + 1; // +1 for the filter-type byte at the start of each row

        // Decompress: the IDAT chunks together form one zlib stream (2-byte header +
        // deflate data + 4-byte Adler32), read straight into a buffer of the exact size.
        byte[] raw = new byte[(long)stride * imgHeight];
        using (var zlib = new ZLibStream(new ChunkStream(png, idat), CompressionMode.Decompress))
        {
            if (zlib.ReadAtLeast(raw, raw.Length, throwOnEndOfStream: false) < raw.Length)
                throw new InvalidDataException("PNG image data is shorter than its declared size.");
        }

        Unfilter(raw, stride, imgHeight, srcBpp);
        return ToRgb(raw, stride, imgWidth, imgHeight, colorType, palette, out alpha);
    }

    private static int BytesPerPixel(int colorType) => colorType switch
    {
        2 => 3,   // RGB
        4 => 2,   // grayscale + alpha
        6 => 4,   // RGBA
        3 => 1,   // indexed (1 byte palette index)
        _ => 3,
    };

    /// <summary>
    /// Reads the pixel size from the IHDR chunk without decoding any pixel data,
    /// applying the same format checks as <see cref="Decode"/>. IHDR is required
    /// to be the first chunk, straight after the 8-byte signature.
    /// </summary>
    /// <exception cref="InvalidDataException">The data is not a PNG or has no IHDR chunk.</exception>
    /// <exception cref="NotSupportedException">The PNG uses a bit depth, colour type, or interlacing this decoder does not support.</exception>
    internal static (int Width, int Height) ReadHeader(ReadOnlySpan<byte> png)
    {
        if (png.Length < 8 || !png[..8].SequenceEqual(Signature))
            throw new InvalidDataException("Stream does not begin with a valid PNG signature.");

        // 8 signature + 4 length + 4 type + 13 IHDR payload
        if (png.Length < 8 + 8 + 13 || !png.Slice(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("PNG does not start with an IHDR chunk.");

        var ihdr = png.Slice(16, 13);
        int width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(ihdr);
        int height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(ihdr[4..]);
        ValidateHeader(bitDepth: ihdr[8], colorType: ihdr[9], interlace: ihdr[12]);
        return (width, height);
    }

    /// <summary>
    /// The still-compressed pixel data of an RGB (colour type 2) or indexed (type 3) PNG,
    /// which a PDF viewer can decode itself: the concatenated IDAT chunks form one zlib
    /// stream whose rows carry PNG filter bytes, i.e. exactly what <c>/FlateDecode</c> with
    /// <c>/DecodeParms &lt;&lt; /Predictor 15 … &gt;&gt;</c> expects.
    /// </summary>
    internal sealed record Passthrough(int Width, int Height, int ColorType, byte[]? Palette, byte[] ZlibData);

    /// <summary>
    /// Extracts the compressed image data for embedding without decoding, or returns
    /// <see langword="null"/> when the PNG needs decoding: colour types with an alpha
    /// channel (4, 6) must be split into colour and a /SMask, and a palette PNG without
    /// a valid PLTE chunk is left to <see cref="Decode"/>.
    /// Chunk CRCs are not verified, matching <see cref="Decode"/>.
    /// </summary>
    internal static Passthrough? TryReadPassthrough(byte[] png)
    {
        var (width, height) = ReadHeader(png);
        int colorType = png[16 + 9];
        if (colorType != 2 && colorType != 3) return null;

        byte[]? palette = null;
        var idat = new List<(int Offset, int Length)>();
        int total = 0;
        int pos = 8;
        while (pos + 8 <= png.Length)
        {
            int len = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos));
            if (len < 0 || pos + 12 + (long)len > png.Length) return null; // truncated: let Decode report it
            var type = png.AsSpan(pos + 4, 4);
            int data = pos + 8;

            if (type.SequenceEqual("IDAT"u8)) { idat.Add((data, len)); total += len; }
            else if (type.SequenceEqual("PLTE"u8)) palette = png.AsSpan(data, len).ToArray();
            else if (type.SequenceEqual("IEND"u8)) break;

            pos = data + len + 4; // skip CRC
        }

        if (idat.Count == 0) return null;
        if (colorType == 3 && (palette is null || palette.Length == 0 || palette.Length % 3 != 0 || palette.Length > 256 * 3))
            return null;

        var zlib = new byte[total];
        int at = 0;
        foreach (var (offset, length) in idat)
        {
            Buffer.BlockCopy(png, offset, zlib, at, length);
            at += length;
        }
        return new Passthrough(width, height, colorType, palette, zlib);
    }

    private static void ValidateHeader(int bitDepth, int colorType, int interlace)
    {
        if (bitDepth != 8)
            throw new NotSupportedException(
                $"PNG bit depth {bitDepth} is not supported; only 8-bit PNGs are accepted.");
        if (colorType != 2 && colorType != 3 && colorType != 4 && colorType != 6)
            throw new NotSupportedException(
                $"PNG color type {colorType} is not supported; use RGB (2), grayscale+alpha (4), RGBA (6), or indexed (3).");
        if (interlace != 0)
            throw new NotSupportedException("Interlaced PNGs are not supported.");
    }

    // ------------------------------------------------------------
    //  PNG row un-filtering
    // ------------------------------------------------------------

    /// <summary>
    /// Reverses the PNG row filters in place. Each row's filter byte stays where it is;
    /// the previous (already unfiltered) row sits just above in the same buffer.
    /// </summary>
    private static void Unfilter(byte[] raw, int stride, int height, int bpp)
    {
        int rowLength = stride - 1;
        var zeroRow = new byte[rowLength]; // the row "above" the first row is all zeros

        for (int y = 0; y < height; y++)
        {
            int rowStart = y * stride;
            var row  = raw.AsSpan(rowStart + 1, rowLength);
            var prev = y == 0 ? zeroRow : raw.AsSpan(rowStart - stride + 1, rowLength);
            ApplyInverseFilter(row, prev, raw[rowStart], bpp);
        }
    }

    /// <summary>
    /// Converts unfiltered rows to 24-bit RGB; for colour types 4 and 6 also extracts the
    /// alpha plane, allocated only when at least one pixel is not fully opaque.
    /// </summary>
    private static byte[] ToRgb(byte[] raw, int stride, int width, int height, int colorType,
        byte[]? palette, out byte[]? alpha)
    {
        bool hasAlpha = colorType == 4 || colorType == 6;
        int srcBpp = BytesPerPixel(colorType);
        int alphaOffset = srcBpp - 1;

        bool anyTransparency = false;
        if (hasAlpha)
        {
            for (int y = 0; y < height && !anyTransparency; y++)
            {
                int rowStart = y * stride + 1;
                for (int x = 0; x < width; x++)
                {
                    if (raw[rowStart + x * srcBpp + alphaOffset] != 0xFF) { anyTransparency = true; break; }
                }
            }
        }

        var rgb = new byte[width * height * 3];
        byte[]? alphaBytes = anyTransparency ? new byte[width * height] : null;
        int dst = 0;

        for (int y = 0; y < height; y++)
        {
            var row = raw.AsSpan(y * stride + 1, stride - 1);
            switch (colorType)
            {
                case 2: // RGB - copy directly
                    row.CopyTo(rgb.AsSpan(dst));
                    dst += row.Length;
                    break;

                case 6: // RGBA - RGB copied, alpha collected for a /SMask
                    for (int x = 0, i = 0; x < width; x++, i += 4)
                    {
                        rgb[dst++] = row[i];
                        rgb[dst++] = row[i + 1];
                        rgb[dst++] = row[i + 2];
                        alphaBytes?[y * width + x] = row[i + 3];
                    }
                    break;

                case 4: // Grayscale + alpha - expand gray to RGB and collect alpha
                    for (int x = 0, i = 0; x < width; x++, i += 2)
                    {
                        byte gray = row[i];
                        rgb[dst++] = gray;
                        rgb[dst++] = gray;
                        rgb[dst++] = gray;
                        alphaBytes?[y * width + x] = row[i + 1];
                    }
                    break;

                case 3: // Indexed - look up colour in palette
                    for (int x = 0; x < width; x++)
                    {
                        int pi = row[x] * 3;
                        rgb[dst++] = palette![pi];
                        rgb[dst++] = palette![pi + 1];
                        rgb[dst++] = palette![pi + 2];
                    }
                    break;
            }
        }

        alpha = alphaBytes;
        return rgb;
    }

    private static void ApplyInverseFilter(Span<byte> row, ReadOnlySpan<byte> prev, byte filter, int bpp)
    {
        switch (filter)
        {
            case 0: break; // None - no transformation

            case 1: // Sub - each byte is predicted from the byte bpp positions to the left
                for (int i = bpp; i < row.Length; i++)
                    row[i] = (byte)(row[i] + row[i - bpp]);
                break;

            case 2: // Up - each byte is predicted from the byte above it in the previous row
                for (int i = 0; i < row.Length; i++)
                    row[i] = (byte)(row[i] + prev[i]);
                break;

            case 3: // Average - predicted from floor((left + above) / 2)
                for (int i = 0; i < row.Length; i++)
                {
                    int left = i >= bpp ? row[i - bpp] : 0;
                    int above = prev[i];
                    row[i] = (byte)(row[i] + (left + above) / 2);
                }
                break;

            case 4: // Paeth - predicted by the Paeth predictor function
                for (int i = 0; i < row.Length; i++)
                {
                    int left = i >= bpp ? row[i - bpp] : 0;
                    int above = prev[i];
                    int upperLeft = i >= bpp ? prev[i - bpp] : 0;
                    row[i] = (byte)(row[i] + PaethPredictor(left, above, upperLeft));
                }
                break;
        }
    }

    /// <summary>
    /// Read-only stream over the IDAT chunk payloads of a PNG, in order, so they can be
    /// decompressed as one zlib stream without first being copied into one array.
    /// </summary>
    private sealed class ChunkStream(byte[] data, List<(int Offset, int Length)> chunks) : Stream
    {
        private int _chunk;
        private int _posInChunk;

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            while (_chunk < chunks.Count)
            {
                var (chunkOffset, chunkLength) = chunks[_chunk];
                int available = chunkLength - _posInChunk;
                if (available > 0)
                {
                    int n = Math.Min(available, buffer.Length);
                    data.AsSpan(chunkOffset + _posInChunk, n).CopyTo(buffer);
                    _posInChunk += n;
                    return n;
                }
                _chunk++;
                _posInChunk = 0;
            }
            return 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // ------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------

    // PNG stores multi-byte integers in big-endian order
    private static int ReadBigEndianInt32(byte[] buf, int offset = 0) =>
        (buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3];

    // Paeth predictor: selects the nearest of left, above, or upper-left
    private static int PaethPredictor(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }
}
