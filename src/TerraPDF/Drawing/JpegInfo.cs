namespace TerraPDF.Drawing;

/// <summary>
/// Reads image dimensions and component count from a JPEG file by scanning its markers.
/// No pixel decoding is performed - JPEG bytes are embedded in the PDF verbatim via DCTDecode.
/// </summary>
internal static class JpegInfo
{
    // JPEG markers that introduce a Start-Of-Frame segment containing image dimensions.
    // SOF0 (0xC0) = baseline DCT, SOF1 (0xC1) = extended sequential, SOF2 (0xC2) = progressive.
    // SOF3/SOF5-SOF7/SOF9-SOF11 are less common but share the same payload layout.
    private static readonly HashSet<byte> SofMarkers = new()
    {
        0xC0, 0xC1, 0xC2, 0xC3,
        0xC5, 0xC6, 0xC7,
        0xC9, 0xCA, 0xCB,
        0xCD, 0xCE, 0xCF,
    };

    /// <summary>
    /// Parses the JPEG file at <paramref name="path"/> and returns its pixel dimensions
    /// and number of colour components (1 = grayscale, 3 = RGB/YCbCr, 4 = CMYK).
    /// </summary>
    /// <exception cref="InvalidDataException">The file is not a valid JPEG.</exception>
    internal static (int Width, int Height, int Components) Read(string path)
    {
        using var fs = File.OpenRead(path);
        return Read(fs);
    }

    internal static (int Width, int Height, int Components) Read(Stream stream)
    {
        // JPEG files start with SOI marker FF D8
        int b0 = stream.ReadByte();
        int b1 = stream.ReadByte();
        if (b0 != 0xFF || b1 != 0xD8)
            throw new InvalidDataException("Stream does not begin with a valid JPEG SOI marker (FF D8).");

        while (true)
        {
            // Every JPEG marker begins with 0xFF; skip any padding 0xFF bytes
            int m = stream.ReadByte();
            if (m == -1) throw new InvalidDataException("Unexpected end of JPEG stream.");
            if (m != 0xFF) throw new InvalidDataException($"Expected JPEG marker byte 0xFF, got 0x{m:X2}.");

            // Skip additional 0xFF padding bytes (allowed by the spec)
            while (m == 0xFF) m = stream.ReadByte();
            if (m == -1) throw new InvalidDataException("Unexpected end of JPEG stream after marker prefix.");

            byte marker = (byte)m;

            // SOI / EOI markers have no length field
            if (marker == 0xD8) continue; // SOI (shouldn't appear again, but harmless)
            if (marker == 0xD9) break;    // EOI - end of image, no SOF found

            // Read the 2-byte big-endian segment length (includes the 2 length bytes themselves)
            int hi = stream.ReadByte();
            int lo = stream.ReadByte();
            if (hi == -1 || lo == -1) throw new InvalidDataException("Truncated JPEG segment length.");
            int segLen = (hi << 8) | lo;
            int dataLen = segLen - 2; // payload bytes after the length field

            if (SofMarkers.Contains(marker))
            {
                // SOF payload layout:
                //   1 byte  - sample precision (bit depth, usually 8)
                //   2 bytes - image height (big-endian)
                //   2 bytes - image width  (big-endian)
                //   1 byte  - number of colour components
                if (dataLen < 6)
                    throw new InvalidDataException("JPEG SOF segment is too short.");

                int precision  = stream.ReadByte();
                int heightHi   = stream.ReadByte();
                int heightLo   = stream.ReadByte();
                int widthHi    = stream.ReadByte();
                int widthLo    = stream.ReadByte();
                int components = stream.ReadByte();

                _ = precision; // not needed for embedding

                return (
                    Width:      (widthHi  << 8) | widthLo,
                    Height:     (heightHi << 8) | heightLo,
                    Components: components
                );
            }

            // Skip non-SOF segments
            if (dataLen > 0) stream.Seek(dataLen, SeekOrigin.Current);
        }

        throw new InvalidDataException("No SOF marker found in JPEG stream - cannot determine image dimensions.");
    }
}
