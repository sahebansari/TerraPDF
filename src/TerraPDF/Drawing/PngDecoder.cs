using System.IO.Compression;

namespace TerraPDF.Drawing;

/// <summary>
/// Minimal pure-C# PNG decoder that produces flat 24-bit RGB pixel data.
/// Supports color types 2 (RGB), 6 (RGBA - alpha discarded), and 3 (indexed/palette),
/// all at bit depth 8. Interlaced PNGs are not supported.
/// </summary>
internal static class PngDecoder
{
    // Standard 8-byte PNG file signature
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>
    /// Decodes a PNG file at <paramref name="path"/> and returns a flat row-major
    /// array of RGB bytes (3 bytes per pixel, top-to-bottom, left-to-right).
    /// </summary>
    internal static byte[] Decode(string path, out int width, out int height)
    {
        using var fs = File.OpenRead(path);
        return Decode(fs, out width, out height);
    }

    internal static byte[] Decode(Stream stream, out int width, out int height)
    {
        ValidateSignature(stream);

        int imgWidth = 0, imgHeight = 0, colorType = 0, bitDepth = 0;
        byte[]? palette = null;
        var idatChunks = new List<byte[]>();

        // Read chunks until IEND
        while (true)
        {
            var lenBuf = new byte[4];
            if (stream.Read(lenBuf, 0, 4) < 4) break;
            int len = ReadBigEndianInt32(lenBuf);

            var typeBuf = new byte[4];
            stream.ReadExactly(typeBuf, 0, 4);
            string chunkType = System.Text.Encoding.ASCII.GetString(typeBuf);

            var data = new byte[len];
            if (len > 0) stream.ReadExactly(data, 0, len);
            stream.ReadExactly(new byte[4], 0, 4); // skip CRC

            switch (chunkType)
            {
                case "IHDR":
                    imgWidth  = ReadBigEndianInt32(data, 0);
                    imgHeight = ReadBigEndianInt32(data, 4);
                    bitDepth  = data[8];
                    colorType = data[9];
                    if (bitDepth != 8)
                        throw new NotSupportedException(
                            $"PNG bit depth {bitDepth} is not supported; only 8-bit PNGs are accepted.");
                    if (colorType != 2 && colorType != 3 && colorType != 6)
                        throw new NotSupportedException(
                            $"PNG color type {colorType} is not supported; use RGB (2), RGBA (6), or indexed (3).");
                    if (data[12] != 0) // interlace method
                        throw new NotSupportedException("Interlaced PNGs are not supported.");
                    break;

                case "PLTE":
                    // Palette: 3 bytes (R, G, B) per entry
                    palette = data;
                    break;

                case "IDAT":
                    idatChunks.Add(data);
                    break;

                case "IEND":
                    goto parseDone;
            }
        }
        parseDone:

        width  = imgWidth;
        height = imgHeight;

        // Concatenate all IDAT chunks into one buffer
        int totalLen = idatChunks.Sum(c => c.Length);
        var idatAll  = new byte[totalLen];
        int pos      = 0;
        foreach (var chunk in idatChunks) { chunk.CopyTo(idatAll, pos); pos += chunk.Length; }

        // Decompress: idatAll is a zlib stream (2-byte header + deflate data + 4-byte Adler32)
        // ZLibStream handles the zlib framing transparently.
        using var compressedMs = new MemoryStream(idatAll);
        using var zlib         = new ZLibStream(compressedMs, CompressionMode.Decompress);
        using var rawMs        = new MemoryStream();
        zlib.CopyTo(rawMs);
        byte[] raw = rawMs.ToArray();

        return Unfilter(raw, imgWidth, imgHeight, colorType, palette);
    }

    // ------------------------------------------------------------
    //  PNG row un-filtering
    // ------------------------------------------------------------

    private static byte[] Unfilter(byte[] raw, int width, int height, int colorType, byte[]? palette)
    {
        // Bytes per source pixel in the filtered data stream
        int srcBpp = colorType switch
        {
            2 => 3,   // RGB
            6 => 4,   // RGBA
            3 => 1,   // indexed (1 byte palette index)
            _ => 3,
        };

        int rowStride = width * srcBpp + 1; // +1 for the filter-type byte at the start of each row
        var rgb       = new byte[width * height * 3];
        int dstOffset = 0;

        var prevRow = new byte[width * srcBpp]; // previous unfiltered row (all-zeros for first row)

        for (int y = 0; y < height; y++)
        {
            int    rowStart = y * rowStride;
            byte   filter   = raw[rowStart];
            var    row      = new byte[width * srcBpp];

            // Copy raw (still-filtered) bytes into row buffer
            Buffer.BlockCopy(raw, rowStart + 1, row, 0, row.Length);

            // Apply the inverse of the PNG row filter in-place
            ApplyInverseFilter(row, prevRow, filter, srcBpp);

            // Convert source pixels to 24-bit RGB output
            for (int x = 0; x < width; x++)
            {
                switch (colorType)
                {
                    case 2: // RGB - copy directly
                        rgb[dstOffset++] = row[x * 3];
                        rgb[dstOffset++] = row[x * 3 + 1];
                        rgb[dstOffset++] = row[x * 3 + 2];
                        break;
                    case 6: // RGBA - discard alpha channel
                        rgb[dstOffset++] = row[x * 4];
                        rgb[dstOffset++] = row[x * 4 + 1];
                        rgb[dstOffset++] = row[x * 4 + 2];
                        break;
                    case 3: // Indexed - look up colour in palette
                        int pi = row[x] * 3;
                        rgb[dstOffset++] = palette![pi];
                        rgb[dstOffset++] = palette![pi + 1];
                        rgb[dstOffset++] = palette![pi + 2];
                        break;
                }
            }

            prevRow = row;
        }

        return rgb;
    }

    private static void ApplyInverseFilter(byte[] row, byte[] prev, byte filter, int bpp)
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
                    int left  = i >= bpp ? row[i - bpp] : 0;
                    int above = prev[i];
                    row[i] = (byte)(row[i] + (left + above) / 2);
                }
                break;

            case 4: // Paeth - predicted by the Paeth predictor function
                for (int i = 0; i < row.Length; i++)
                {
                    int left      = i >= bpp ? row[i - bpp] : 0;
                    int above     = prev[i];
                    int upperLeft = i >= bpp ? prev[i - bpp] : 0;
                    row[i] = (byte)(row[i] + PaethPredictor(left, above, upperLeft));
                }
                break;
        }
    }

    // ------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------

    private static void ValidateSignature(Stream stream)
    {
        var sig = new byte[8];
        stream.ReadExactly(sig, 0, 8);
        for (int i = 0; i < 8; i++)
            if (sig[i] != Signature[i])
                throw new InvalidDataException("Stream does not begin with a valid PNG signature.");
    }

    // PNG stores multi-byte integers in big-endian order
    private static int ReadBigEndianInt32(byte[] buf, int offset = 0) =>
        (buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3];

    // Paeth predictor: selects the nearest of left, above, or upper-left
    private static int PaethPredictor(int a, int b, int c)
    {
        int p  = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc)             return b;
        return c;
    }
}
