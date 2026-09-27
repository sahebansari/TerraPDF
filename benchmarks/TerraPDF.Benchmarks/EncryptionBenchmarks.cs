using BenchmarkDotNet.Attributes;
using TerraPDF.Benchmarks.Scenarios;
using TerraPDF.Core;

namespace TerraPDF.Benchmarks;

/// <summary>Overhead of encryption on a ~20 page text document.</summary>
public class EncryptionBenchmarks : PdfBenchmarkBase
{
    private const int Pages = 20;

    [Benchmark(Baseline = true)]
    public long Unencrypted() => Publish(Docs.LongText(Pages));

    [Benchmark]
    public long Aes128() => Publish(Docs.Encrypted(Docs.LongText(Pages), EncryptionAlgorithm.Aes128));

    [Benchmark]
    public long Aes256() => Publish(Docs.Encrypted(Docs.LongText(Pages), EncryptionAlgorithm.Aes256));
}
