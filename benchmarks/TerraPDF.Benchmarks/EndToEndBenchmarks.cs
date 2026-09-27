using BenchmarkDotNet.Attributes;
using TerraPDF.Benchmarks.Scenarios;

namespace TerraPDF.Benchmarks;

/// <summary>Whole documents: compose, lay out, paginate and serialize.</summary>
public class EndToEndBenchmarks : PdfBenchmarkBase
{
    private byte[] _logo = [];

    [GlobalSetup]
    public void Setup() => _logo = Assets.SmallLogoJpg();

    /// <summary>Fixed per-document cost baseline.</summary>
    [Benchmark(Baseline = true)]
    public long HelloWorld() => Publish(Docs.HelloWorld());

    [Benchmark]
    public long Invoice30Lines() => Publish(Docs.Invoice(30, _logo));

    [Benchmark]
    public long Invoice300Lines() => Publish(Docs.Invoice(300, _logo));
}
