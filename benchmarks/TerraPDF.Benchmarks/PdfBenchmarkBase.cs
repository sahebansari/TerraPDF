using TerraPDF.Core;

namespace TerraPDF.Benchmarks;

/// <summary>
/// Shared output sink: every benchmark publishes into one reused <see cref="MemoryStream"/>
/// so disk I/O never shows up in the numbers. Returning the length keeps the JIT honest.
/// </summary>
public abstract class PdfBenchmarkBase
{
    private readonly MemoryStream _output = new(capacity: 1 << 20);

    protected long Publish(DocumentComposer document)
    {
        _output.SetLength(0);
        document.PublishPdf(_output);
        return _output.Length;
    }
}
