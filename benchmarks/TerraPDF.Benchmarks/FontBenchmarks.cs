using BenchmarkDotNet.Attributes;
using TerraPDF.Benchmarks.Scenarios;
using TerraPDF.Drawing.TrueType;

namespace TerraPDF.Benchmarks;

/// <summary>Custom TrueType fonts: parsing, shaping, subsetting and embedding.</summary>
public class FontBenchmarks : PdfBenchmarkBase
{
    private byte[] _latoBytes = [];
    private byte[] _devanagariBytes = [];
    private TrueTypeFont _lato = null!;
    private HashSet<ushort> _latinGlyphs = [];

    [GlobalSetup]
    public void Setup()
    {
        Assets.RegisterFonts();
        _latoBytes = Assets.LatoRegular();
        _devanagariBytes = Assets.Devanagari();
        _lato = TrueTypeFont.Parse(_latoBytes);
        _latinGlyphs = [.. Text.Paragraph.Select(ch => _lato.GetGlyphId(ch))];
    }

    [Benchmark]
    public int ParseLato() => TrueTypeFont.Parse(_latoBytes).NumGlyphs;

    [Benchmark]
    public int ParseDevanagari() => TrueTypeFont.Parse(_devanagariBytes).NumGlyphs;

    [Benchmark]
    public byte[] SubsetLato() => _lato.BuildSubsetRawData(_latinGlyphs);

    /// <summary>10 pages of Latin text in an embedded, subsetted custom font.</summary>
    [Benchmark]
    public long LatinDocument10Pages() => Publish(Docs.LongText(10, "Lato"));

    /// <summary>10 pages of Devanagari (GSUB conjuncts + reordering).</summary>
    [Benchmark]
    public long DevanagariDocument10Pages() => Publish(Docs.LongText(10, "Devanagari", Text.Devanagari));
}
