using BenchmarkDotNet.Attributes;
using TerraPDF.Benchmarks.Scenarios;

namespace TerraPDF.Benchmarks;

/// <summary>Word wrapping, pagination and span styling with the built-in fonts.</summary>
public class TextBenchmarks : PdfBenchmarkBase
{
    [Params(10, 100, 500)]
    public int Pages { get; set; }

    /// <summary>Plain wrapped paragraphs; cost should grow linearly with <see cref="Pages"/>.</summary>
    [Benchmark]
    public long LongText() => Publish(Docs.LongText(Pages));

    /// <summary>Same volume, but every word is its own styled span.</summary>
    [Benchmark]
    public long RichSpans() => Publish(Docs.RichSpans(Pages * Text.ParagraphsPerPage));

    /// <summary>Non-WinAnsi text with a built-in font (fallback / encoding path).</summary>
    [Benchmark]
    public long UnicodeWithBuiltInFont() => Publish(Docs.LongText(Pages, paragraph: Text.Multilingual));
}
