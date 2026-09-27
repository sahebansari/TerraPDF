using System.IO.Compression;
using TerraPDF.Core;
using TerraPDF.Drawing;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// RGB and palette PNGs are embedded without decoding (<c>/Predictor 15</c>) only when their
/// data inflates to the full image; malformed files are reported as before instead of
/// producing a PDF the viewer cannot render. Also checks that <c>Image(byte[])</c> does not
/// depend on the caller's buffer after the call.
/// </summary>
public sealed class PngPassthroughValidationTests
{
    private static byte[] Publish(byte[] png) => Document.Create(c => c.Page(p =>
    {
        p.Size(PageSize.A5);
        p.Content().Image(png, 100);
    })).PublishPdf();

    private static string Raw(byte[] pdf) => System.Text.Encoding.Latin1.GetString(pdf);

    /// <summary>Scanlines of a <paramref name="width"/>×<paramref name="height"/> RGB image, filter byte None.</summary>
    private static byte[] RgbScanlines(int width, int height)
    {
        var rows = new byte[height * (1 + width * 3)];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = i % (1 + width * 3) == 0 ? (byte)0 : (byte)(i * 7);
        return rows;
    }

    private static byte[] Zlib(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Fastest, leaveOpen: true))
            z.Write(data);
        return ms.ToArray();
    }

    /// <summary>A valid RGB PNG whose IDAT payload is replaced by <paramref name="idat"/>.</summary>
    private static byte[] RgbPngWithIdat(int width, int height, byte[] idat)
    {
        byte[] png = TestImageData.MakePng(width, height, rgba: false, alphaValue: 0);
        using var result = new MemoryStream();
        result.Write(png, 0, 8);
        var length = new byte[4];
        for (int pos = 8; pos + 8 <= png.Length;)
        {
            int len = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos));
            bool isIdat = png.AsSpan(pos + 4, 4).SequenceEqual("IDAT"u8);
            byte[] data = isIdat ? idat : png.AsSpan(pos + 8, len).ToArray();
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            result.Write(length);
            result.Write(png, pos + 4, 4);
            result.Write(data);
            result.Write([0, 0, 0, 0]); // CRC, not verified
            pos += 12 + len;
        }
        return result.ToArray();
    }

    [Fact]
    public void ValidRgbPngIsPassedThroughAndCached()
    {
        byte[] png = TestImageData.MakePng(13, 11, rgba: false, alphaValue: 0);
        string key = ImageSource.FromBytes(png).ContentKey;
        EncodedImageCache.Remove(key);

        string raw = Raw(Publish(png));

        Assert.Contains("/Predictor 15", raw);
        Assert.True(EncodedImageCache.Contains(key));
    }

    [Fact]
    public void TruncatedRgbPngIsReportedOnPublish()
    {
        byte[] rows = RgbScanlines(12, 10);
        byte[] png = RgbPngWithIdat(12, 10, Zlib(rows[..(rows.Length / 2)]));

        var ex = Assert.Throws<InvalidDataException>(() => Publish(png));
        Assert.Contains("shorter than its declared size", ex.Message);
    }

    [Fact]
    public void CorruptDeflateDataInRgbPngIsReportedOnPublish()
    {
        byte[] png = RgbPngWithIdat(12, 9, [0x78, 0x9C, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x13, 0x37]);

        Assert.Throws<InvalidDataException>(() => Publish(png));
    }

    [Fact]
    public void RgbPngWithInvalidRowFilterIsDecodedNotPassedThrough()
    {
        byte[] rows = RgbScanlines(12, 8);
        rows[(1 + 12 * 3) * 3] = 9; // row 3: no such filter type
        byte[] png = RgbPngWithIdat(12, 8, Zlib(rows));

        string raw = Raw(Publish(png));

        Assert.Contains("/Width 12 /Height 8", raw);
        Assert.DoesNotContain("/Predictor 15", raw);
    }

    [Fact]
    public void PngWithOutOfRangeWidthIsRejected()
    {
        byte[] png = TestImageData.MakePng(3, 5, rgba: false, alphaValue: 0);
        png[16] = 0x80; // IHDR width ≥ 2^31: reads as negative

        Assert.Throws<InvalidDataException>(() => Publish(png));
    }

    [Fact]
    public void ReusingTheBufferAfterImageDoesNotChangeTheDocument()
    {
        byte[] buffer = TestImageData.MakePng(14, 6, rgba: false, alphaValue: 0);
        var document = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A5);
            p.Content().Image(buffer, 100);
        }));

        Array.Clear(buffer); // the caller reuses its buffer before publishing

        string raw = Raw(document.PublishPdf());
        Assert.Contains("/Width 14 /Height 6", raw);
        Assert.Contains("/Predictor 15", raw);
    }

    [Fact]
    public void ABufferPassedAgainAfterChangingIsNotServedFromTheEarlierCopy()
    {
        byte[] buffer = TestImageData.MakePng(15, 6, rgba: false, alphaValue: 0);
        byte[] first = ImageSource.Snapshot(buffer);
        Assert.NotSame(buffer, first);
        Assert.Same(first, ImageSource.Snapshot(buffer)); // unchanged: the copy is reused

        buffer[20] ^= 0xFF; // same instance, content changed
        byte[] afterEdit = ImageSource.Snapshot(buffer);

        Assert.NotSame(first, afterEdit);
        Assert.Equal(buffer, afterEdit);
        Assert.NotEqual(first, afterEdit);
    }
}
