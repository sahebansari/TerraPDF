using BenchmarkDotNet.Attributes;
using TerraPDF.Barcodes;
using TerraPDF.Barcodes.QrCode;
using TerraPDF.Benchmarks.Scenarios;

namespace TerraPDF.Benchmarks;

/// <summary>QR code encoding per error correction level.</summary>
public class QrCodeBenchmarks
{
    private const string ShortData = "https://github.com/sahebansari/TerraPDF";
    private static readonly string LongData = string.Concat(Enumerable.Repeat("TerraPDF-QR-payload-", 40));

    [Params(QrErrorCorrectionLevel.L, QrErrorCorrectionLevel.H)]
    public QrErrorCorrectionLevel Level { get; set; }

    [Benchmark]
    public int QrShort() => QrCodeGenerator.Generate(ShortData, Level).Size;

    [Benchmark]
    public int QrLong() => QrCodeGenerator.Generate(LongData, Level).Size;
}

/// <summary>Code128 encoding and a QR-heavy document.</summary>
public class BarcodeBenchmarks : PdfBenchmarkBase
{
    [Benchmark]
    public bool[] Code128() => Code128Encoder.Encode("TERRAPDF-0123456789-ABCDEF");

    [Benchmark]
    public long Document100QrCodes() => Publish(Docs.QrCodes(100));
}
