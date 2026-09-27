using TerraPDF.Core;
using TerraPDF.Drawing;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// The process-wide cache of converted PNG streams: a PNG used by several documents is
/// decoded and compressed once, and cached streams produce exactly the same PDF bytes.
/// </summary>
public sealed class EncodedImageCacheTests
{
    // Each test uses its own image (distinct alpha value), since the cache is shared by
    // every test running in the process.
    private static byte[] UniquePng(byte alpha) => TestImageData.MakePng(9, 7, rgba: true, alphaValue: alpha);

    private static DocumentComposer Compose(byte[] png) => Document.Create(c => c.Page(p =>
    {
        p.Size(PageSize.A5);
        p.Content().Image(png, 100);
    }));

    private static byte[] Publish(byte[] png) => Compose(png).PublishPdf();

    [Fact]
    public void SecondDocumentReusesTheConvertedImage()
    {
        byte[] png = UniquePng(101);
        string key = ImageSource.FromBytes(png).ContentKey;
        EncodedImageCache.Remove(key);

        Publish(png);
        Assert.True(EncodedImageCache.Contains(key));

        // A different byte[] with the same content hits the same entry.
        Publish((byte[])png.Clone());
        Assert.True(EncodedImageCache.Contains(key));
    }

    [Fact]
    public void CachedAndFreshConversionsProduceIdenticalPdfs()
    {
        byte[] png = UniquePng(77);
        string key = ImageSource.FromBytes(png).ContentKey;

        // Same document published twice (image aliases are per element, so two separately
        // composed documents never match byte for byte).
        var document = Compose(png);
        EncodedImageCache.Remove(key);
        byte[] fresh = document.PublishPdf();     // converts and caches
        Assert.True(EncodedImageCache.Contains(key));
        byte[] cached = document.PublishPdf();    // served from the cache

        Assert.Equal(fresh, cached);
        Assert.Contains("/SMask ", System.Text.Encoding.Latin1.GetString(cached));
    }

    [Fact]
    public void CacheStaysWithinItsBudget()
    {
        for (byte a = 1; a < 40; a++)
            Publish(UniquePng(a));

        Assert.InRange(EncodedImageCache.Bytes, 1, EncodedImageCache.MaxBytes);
    }
}
