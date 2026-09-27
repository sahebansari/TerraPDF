using BenchmarkDotNet.Attributes;
using TerraPDF.Benchmarks.Scenarios;

namespace TerraPDF.Benchmarks;

/// <summary>Table layout and pagination (repeating headers, row/column spans).</summary>
public class TableBenchmarks : PdfBenchmarkBase
{
    [Params(100, 1_000, 10_000)]
    public int Rows { get; set; }

    [Benchmark(Baseline = true)]
    public long PlainTable() => Publish(Docs.Table(Rows));

    [Benchmark]
    public long TableWithSpans() => Publish(Docs.TableWithSpans(Rows));
}
