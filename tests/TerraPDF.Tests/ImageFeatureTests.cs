using System.IO.Compression;
using System.Text.RegularExpressions;
using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for tier-5 image features: byte[]/Stream sources, PNG alpha (/SMask),
/// document-level deduplication, and the aspect-ratio fix.
/// </summary>
public sealed class ImageFeatureTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    private static string Raw(byte[] b) => System.Text.Encoding.Latin1.GetString(b);

    private static int CountOccurrences(string text, string token)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(token, idx, StringComparison.Ordinal)) >= 0) { count++; idx += token.Length; }
        return count;
    }

    // ── Minimal in-memory PNG builder (CRCs are zeroed; the decoder skips them) ──

    private static byte[] MakePng(int width, int height, bool rgba, byte alphaValue)
    {
        using var ms = new MemoryStream();
        void WriteBE(int v) { ms.WriteByte((byte)(v >> 24)); ms.WriteByte((byte)(v >> 16)); ms.WriteByte((byte)(v >> 8)); ms.WriteByte((byte)v); }
        void Chunk(string type, byte[] data)
        {
            WriteBE(data.Length);
            ms.Write(System.Text.Encoding.ASCII.GetBytes(type));
            ms.Write(data);
            WriteBE(0); // CRC — not verified by PngDecoder
        }

        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        var ihdr = new byte[13];
        ihdr[0] = (byte)(width >> 24);  ihdr[1] = (byte)(width >> 16);  ihdr[2] = (byte)(width >> 8);  ihdr[3] = (byte)width;
        ihdr[4] = (byte)(height >> 24); ihdr[5] = (byte)(height >> 16); ihdr[6] = (byte)(height >> 8); ihdr[7] = (byte)height;
        ihdr[8]  = 8;                        // bit depth
        ihdr[9]  = (byte)(rgba ? 6 : 2);     // colour type
        ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
        Chunk("IHDR", ihdr);

        int bpp = rgba ? 4 : 3;
        var scanlines = new byte[height * (1 + width * bpp)];
        int p = 0;
        for (int y = 0; y < height; y++)
        {
            scanlines[p++] = 0; // filter: None
            for (int x = 0; x < width; x++)
            {
                scanlines[p++] = 200; scanlines[p++] = 100; scanlines[p++] = 50;
                if (rgba) scanlines[p++] = alphaValue;
            }
        }
        using var idat = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionLevel.Fastest, leaveOpen: true))
            z.Write(scanlines);
        Chunk("IDAT", idat.ToArray());
        Chunk("IEND", []);
        return ms.ToArray();
    }

    // Minimal JPEG: SOI + SOF0 (with dims) + EOI — enough for JpegInfo.
    private static byte[] MakeJpegHeaderOnly(int width, int height)
    {
        return
        [
            0xFF, 0xD8,                                      // SOI
            0xFF, 0xC0, 0x00, 0x11,                          // SOF0, length 17
            0x08,                                            // precision
            (byte)(height >> 8), (byte)height,
            (byte)(width  >> 8), (byte)width,
            0x03,                                            // 3 components
            0x01, 0x22, 0x00,  0x02, 0x11, 0x01,  0x03, 0x11, 0x01,
            0xFF, 0xD9,                                      // EOI
        ];
    }

    // ── byte[] / Stream sources ──────────────────────────────────────────────

    [Fact]
    public void PngFromBytesProducesImageXObject()
    {
        byte[] png = MakePng(4, 4, rgba: false, alphaValue: 0);
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Image(png, 100);
        }));

        Assert.Contains("/Subtype /Image", Raw(pdf));
        Assert.Contains("/Width 4 /Height 4", Raw(pdf));
    }

    [Fact]
    public void PngFromStreamProducesImageXObject()
    {
        using var stream = new MemoryStream(MakePng(3, 5, rgba: false, alphaValue: 0));
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Image(stream, 100);
        }));

        Assert.Contains("/Width 3 /Height 5", Raw(pdf));
    }

    [Fact]
    public void JpegFromBytesDetectedByMagicBytes()
    {
        byte[] jpeg = MakeJpegHeaderOnly(7, 9);
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Image(jpeg, 100);
        }));

        Assert.Contains("/Filter /DCTDecode", Raw(pdf));
        Assert.Contains("/Width 7 /Height 9", Raw(pdf));
    }

    [Fact]
    public void UnknownImageDataThrowsNotSupported()
    {
        byte[] bogus = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        Assert.Throws<NotSupportedException>(() =>
            Build(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Image(bogus);
            })));
    }

    // ── PNG alpha → /SMask ───────────────────────────────────────────────────

    [Fact]
    public void TransparentRgbaPngEmitsSMask()
    {
        byte[] png = MakePng(4, 4, rgba: true, alphaValue: 128);
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Image(png, 100);
        }));

        string raw = Raw(pdf);
        Assert.Contains("/SMask", raw);
        Assert.Contains("/ColorSpace /DeviceGray", raw);   // the soft-mask image
    }

    [Fact]
    public void FullyOpaqueRgbaPngEmitsNoSMask()
    {
        byte[] png = MakePng(4, 4, rgba: true, alphaValue: 255);
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Image(png, 100);
        }));

        Assert.DoesNotContain("/SMask", Raw(pdf));
    }

    // ── Document-level deduplication ─────────────────────────────────────────

    [Fact]
    public void SameImageOnTwoPagesIsEmbeddedOnce()
    {
        byte[] png = MakePng(6, 6, rgba: false, alphaValue: 0);
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Image(png, 50);
                col.PageBreak();
                col.Item().Image(png, 50);
            });
        }));

        Assert.Equal(2, CountOccurrences(Raw(pdf), "/Type /Page /"));
        Assert.Equal(1, CountOccurrences(Raw(pdf), "/Subtype /Image"));
    }

    [Fact]
    public void DistinctImagesAreEmbeddedSeparately()
    {
        byte[] a = MakePng(6, 6, rgba: false, alphaValue: 0);
        byte[] b = MakePng(8, 8, rgba: false, alphaValue: 0);
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Column(col =>
            {
                col.Item().Image(a, 50);
                col.Item().Image(b, 50);
            });
        }));

        Assert.Equal(2, CountOccurrences(Raw(pdf), "/Subtype /Image"));
    }

    // ── Aspect-ratio fix ─────────────────────────────────────────────────────

    [Fact]
    public void HeightConstrainedImageKeepsAspectRatio()
    {
        // 10×1000 px (1:100): at content width the scaled height would far
        // exceed the page, so both axes must shrink together.
        byte[] png = MakePng(10, 1000, rgba: false, alphaValue: 0);
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Image(png);
        }));

        string content = PdfTestUtils.InflatedText(pdf);
        var m = Regex.Match(content, @"([0-9.]+) 0 0 ([0-9.]+) [0-9.-]+ [0-9.-]+ cm");
        Assert.True(m.Success, "Image placement matrix not found in content stream.");

        double w = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        double h = double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        // Aspect preserved (1:100) within coordinate-rounding tolerance —
        // emitted values are rounded to 2 decimals, so allow ±1%.
        Assert.True(Math.Abs(h / w - 100.0) < 1.0,
            $"Aspect ratio distorted: {w} × {h} (h/w = {h / w:F2}, expected ≈ 100).");
    }
}
