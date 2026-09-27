using BenchmarkDotNet.Attributes;
using TerraPDF.Benchmarks.Scenarios;
using TerraPDF.Drawing;

namespace TerraPDF.Benchmarks;

/// <summary>Image decoding and embedding.</summary>
public class ImageBenchmarks : PdfBenchmarkBase
{
    private byte[] _png = [];
    private string _pngKey = "";
    private byte[] _rgbPng = [];
    private byte[] _alphaPng = [];
    private byte[] _jpg = [];

    /// <summary>Number of placements of the same image — exposes missing de-duplication.</summary>
    [Params(1, 40)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _png = Assets.HeaderLogoPng();
        _pngKey = ImageSource.FromBytes(_png).ContentKey;
        _rgbPng = Assets.HeaderLogoRgbPng();
        _alphaPng = Assets.AlphaBadgePng();
        _jpg = Assets.SmallLogoJpg();
    }

    [Benchmark]
    public int DecodePng()
    {
        return PngDecoder.Decode(_png, out _, out _, out _).Length;
    }

    [Benchmark]
    public int DecodeAlphaPng()
    {
        return PngDecoder.Decode(_alphaPng, out _, out _, out _).Length;
    }

    /// <summary>
    /// RGBA PNG with the converted streams already in the process-wide cache
    /// (a service rendering its logo into every document).
    /// </summary>
    [Benchmark]
    public long PngDocument() => Publish(Docs.Images(_png, Count));

    /// <summary>Same document, but the image is converted again (first document of the process).</summary>
    [Benchmark]
    public long PngDocumentColdCache()
    {
        EncodedImageCache.Remove(_pngKey);
        return Publish(Docs.Images(_png, Count));
    }

    /// <summary>Opaque RGB PNG: embedded still compressed, no decode.</summary>
    [Benchmark]
    public long RgbPngDocument() => Publish(Docs.Images(_rgbPng, Count));

    [Benchmark]
    public long AlphaPngDocument() => Publish(Docs.Images(_alphaPng, Count));

    [Benchmark]
    public long JpegDocument() => Publish(Docs.Images(_jpg, Count));
}
