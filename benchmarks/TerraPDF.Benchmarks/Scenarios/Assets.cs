namespace TerraPDF.Benchmarks.Scenarios;

/// <summary>Loads the fonts and images copied next to the benchmark binary.</summary>
internal static class Assets
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Assets");

    internal static string LatoRegularPath => Path.Combine(Root, "Fonts", "Lato-Regular.ttf");
    internal static string LatoBoldPath => Path.Combine(Root, "Fonts", "Lato-Bold.ttf");
    internal static string DevanagariPath => Path.Combine(Root, "Fonts", "NotoSansDevanagari-Regular.ttf");

    internal static byte[] LatoRegular() => File.ReadAllBytes(LatoRegularPath);
    internal static byte[] Devanagari() => File.ReadAllBytes(DevanagariPath);
    internal static byte[] HeaderLogoPng() => File.ReadAllBytes(Path.Combine(Root, "header_logo.png"));
    internal static byte[] SmallLogoJpg() => File.ReadAllBytes(Path.Combine(Root, "small_logo.jpg"));
    internal static byte[] AlphaBadgePng() => File.ReadAllBytes(Path.Combine(Root, "alpha_badge.png"));

    /// <summary>
    /// The header logo re-encoded as an opaque RGB (colour type 2) PNG, the kind of PNG
    /// TerraPDF embeds without decoding. The sample assets are all RGBA.
    /// </summary>
    internal static byte[] HeaderLogoRgbPng()
    {
        byte[] rgb = TerraPDF.Drawing.PngDecoder.Decode(HeaderLogoPng(), out int width, out int height, out _);

        var scanlines = new byte[height * (1 + width * 3)];
        for (int y = 0; y < height; y++)
            Buffer.BlockCopy(rgb, y * width * 3, scanlines, y * (1 + width * 3) + 1, width * 3); // filter 0

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 2; // colour type RGB
        WriteChunk(png, "IHDR", ihdr);
        using (var idat = new MemoryStream())
        {
            using (var zlib = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(scanlines);
            WriteChunk(png, "IDAT", idat.ToArray());
        }
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    // CRCs are left at zero: TerraPDF does not verify them.
    private static void WriteChunk(Stream png, string type, byte[] data)
    {
        Span<byte> word = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        png.Write(word);
        png.Write(System.Text.Encoding.ASCII.GetBytes(type));
        png.Write(data);
        word.Clear();
        png.Write(word);
    }

    private static int _fontsRegistered;

    /// <summary>
    /// Registers the custom font families once per process. Registration is
    /// process-wide in TerraPDF, so repeated calls from several benchmark classes are skipped.
    /// </summary>
    internal static void RegisterFonts()
    {
        if (Interlocked.Exchange(ref _fontsRegistered, 1) == 1) return;
        TerraPDF.Helpers.FontFamily.Register("Lato", LatoRegularPath);
        TerraPDF.Helpers.FontFamily.Register("Lato", LatoBoldPath, bold: true);
        TerraPDF.Helpers.FontFamily.Register("Devanagari", DevanagariPath);
    }
}
