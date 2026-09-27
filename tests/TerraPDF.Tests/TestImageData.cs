using System.IO.Compression;
using System.Text;

namespace TerraPDF.Tests;

internal static class TestImageData
{
    internal static byte[] MakeJpegHeaderOnly(int width, int height) =>
    [
        0xFF, 0xD8,
        0xFF, 0xC0, 0x00, 0x11,
        0x08,
        (byte)(height >> 8), (byte)height,
        (byte)(width >> 8), (byte)width,
        0x03,
        0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
        0xFF, 0xD9,
    ];

    /// <summary>
    /// Builds an 8-bit grayscale+alpha (colour type 4) PNG, the one alpha-bearing
    /// colour type <see cref="MakePng"/> cannot produce.
    /// </summary>
    internal static byte[] MakeGrayAlphaPng(int width, int height, byte grayValue, byte alphaValue) =>
        BuildPng(width, height, colorType: 4,
            writePixel: (buffer, offset) =>
            {
                buffer[offset] = grayValue;
                buffer[offset + 1] = alphaValue;
                return 2;
            });

    internal static byte[] MakePng(int width, int height, bool rgba, byte alphaValue) =>
        BuildPng(width, height, colorType: rgba ? 6 : 2,
            writePixel: (buffer, offset) =>
            {
                buffer[offset] = 200;
                buffer[offset + 1] = 100;
                buffer[offset + 2] = 50;
                if (!rgba) return 3;
                buffer[offset + 3] = alphaValue;
                return 4;
            });

    /// <summary>
    /// Builds an 8-bit indexed (colour type 3) PNG with a <paramref name="paletteSize"/>-entry
    /// palette; pixel (x, y) uses palette index <c>(x + y) % paletteSize</c>.
    /// </summary>
    internal static byte[] MakeIndexedPng(int width, int height, int paletteSize)
    {
        var palette = new byte[paletteSize * 3];
        for (int i = 0; i < paletteSize; i++)
        {
            palette[i * 3]     = (byte)(i * 40);
            palette[i * 3 + 1] = (byte)(255 - i * 40);
            palette[i * 3 + 2] = 128;
        }
        return BuildPng(width, height, colorType: 3,
            writePixel: (buffer, offset) =>
            {
                int stride = 1 + width;              // filter byte + one index per pixel
                int y = offset / stride;
                int x = offset % stride - 1;
                buffer[offset] = (byte)((x + y) % paletteSize);
                return 1;
            },
            palette);
    }

    /// <summary>
    /// Writes a minimal single-IDAT PNG. <paramref name="writePixel"/> fills one pixel at
    /// the given offset and returns how many bytes it wrote (the source bytes per pixel).
    /// </summary>
    private static byte[] BuildPng(int width, int height, int colorType,
        Func<byte[], int, int> writePixel, byte[]? palette = null)
    {
        using var stream = new MemoryStream();

        void WriteBigEndian(int value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        void WriteChunk(string type, byte[] data)
        {
            WriteBigEndian(data.Length);
            stream.Write(Encoding.ASCII.GetBytes(type));
            stream.Write(data);
            WriteBigEndian(0); // The test decoder deliberately ignores CRC values.
        }

        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        header[0] = (byte)(width >> 24);
        header[1] = (byte)(width >> 16);
        header[2] = (byte)(width >> 8);
        header[3] = (byte)width;
        header[4] = (byte)(height >> 24);
        header[5] = (byte)(height >> 16);
        header[6] = (byte)(height >> 8);
        header[7] = (byte)height;
        header[8] = 8;
        header[9] = (byte)colorType;
        WriteChunk("IHDR", header);
        if (palette is not null) WriteChunk("PLTE", palette);

        int bytesPerPixel = colorType switch { 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 3 };
        var scanlines = new byte[height * (1 + width * bytesPerPixel)];
        int offset = 0;
        for (int y = 0; y < height; y++)
        {
            scanlines[offset++] = 0; // filter type: None
            for (int x = 0; x < width; x++)
                offset += writePixel(scanlines, offset);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(scanlines);
        WriteChunk("IDAT", compressed.ToArray());
        WriteChunk("IEND", []);
        return stream.ToArray();
    }
}
