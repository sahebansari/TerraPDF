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

    // ── byte[] / Stream sources ──────────────────────────────────────────────

    [Fact]
    public void PngFromBytesProducesImageXObject()
    {
        byte[] png = TestImageData.MakePng(4, 4, rgba: false, alphaValue: 0);
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
        using var stream = new MemoryStream(TestImageData.MakePng(3, 5, rgba: false, alphaValue: 0));
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
        byte[] jpeg = TestImageData.MakeJpegHeaderOnly(7, 9);
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
        byte[] png = TestImageData.MakePng(4, 4, rgba: true, alphaValue: 128);
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
        byte[] png = TestImageData.MakePng(4, 4, rgba: true, alphaValue: 255);
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Image(png, 100);
        }));

        Assert.DoesNotContain("/SMask", Raw(pdf));
    }

    [Fact]
    public void TransparentGrayscaleAlphaPngEmitsSMask()
    {
        byte[] png = TestImageData.MakeGrayAlphaPng(4, 4, grayValue: 90, alphaValue: 128);
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Image(png, 100);
        }));

        string raw = Raw(pdf);
        Assert.Contains("/SMask", raw);
        Assert.Contains("/Width 4 /Height 4", raw);
    }

    [Fact]
    public void FullyOpaqueGrayscaleAlphaPngEmitsNoSMask()
    {
        byte[] png = TestImageData.MakeGrayAlphaPng(4, 4, grayValue: 90, alphaValue: 255);
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Image(png, 100);
        }));

        Assert.DoesNotContain("/SMask", Raw(pdf));
    }

    // ── PNG passthrough (no decode) ──────────────────────────────────────────

    [Fact]
    public void RgbPngIsEmbeddedWithoutDecoding()
    {
        byte[] png = TestImageData.MakePng(5, 3, rgba: false, alphaValue: 0);
        string raw = Raw(Build(c => c.Page(p => p.Content().Image(png))));

        Assert.Contains("/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode " +
                        "/DecodeParms << /Predictor 15 /Colors 3 /BitsPerComponent 8 /Columns 5 >>", raw);
    }

    [Fact]
    public void IndexedPngIsEmbeddedWithIndexedColorSpace()
    {
        byte[] png = TestImageData.MakeIndexedPng(4, 4, paletteSize: 5);
        string raw = Raw(Build(c => c.Page(p => p.Content().Image(png))));

        Assert.Matches(@"/ColorSpace \[/Indexed /DeviceRGB 4 \d+ 0 R\]", raw);
        Assert.Contains("/DecodeParms << /Predictor 15 /Colors 1 /BitsPerComponent 8 /Columns 4 >>", raw);
    }

    [Fact]
    public void RgbaPngIsStillDecoded()
    {
        byte[] png = TestImageData.MakePng(4, 4, rgba: true, alphaValue: 128);
        string raw = Raw(Build(c => c.Page(p => p.Content().Image(png))));

        Assert.DoesNotContain("/Predictor 15", raw);
        Assert.Contains("/SMask ", raw);
    }

    // ── Document-level deduplication ─────────────────────────────────────────

    [Fact]
    public void SameImageOnTwoPagesIsEmbeddedOnce()
    {
        byte[] png = TestImageData.MakePng(6, 6, rgba: false, alphaValue: 0);
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
        byte[] a = TestImageData.MakePng(6, 6, rgba: false, alphaValue: 0);
        byte[] b = TestImageData.MakePng(8, 8, rgba: false, alphaValue: 0);
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
        byte[] png = TestImageData.MakePng(10, 1000, rgba: false, alphaValue: 0);
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
