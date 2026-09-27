using BenchmarkDotNet.Attributes;
using TerraPDF.Benchmarks.Scenarios;

namespace TerraPDF.Benchmarks;

/// <summary>Vector canvas rendering: lines, dashes, pies, gradients, rotated text.</summary>
public class CanvasBenchmarks : PdfBenchmarkBase
{
    [Params(10, 100)]
    public int Canvases { get; set; }

    [Benchmark]
    public long DenseCanvas() => Publish(Docs.DenseCanvas(Canvases));
}
