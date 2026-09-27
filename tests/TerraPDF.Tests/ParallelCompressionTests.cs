using TerraPDF.Core;
using TerraPDF.Drawing;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Large documents compress their page content streams on several cores; the bytes must be
/// exactly those of the sequential path.
/// </summary>
public sealed class ParallelCompressionTests
{
    [Fact]
    public void ParallelAndSequentialCompressionProduceIdenticalPdfs()
    {
        // ~100 pages of dense tables (well over 512 K characters of content): above both thresholds.
        var document = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Footer().Text(t => { t.Span("Page "); t.CurrentPageNumber(); });
            p.Content().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                for (int i = 0; i < 5000; i++)
                    t.Row(r =>
                    {
                        r.Cell().Background(i % 2 == 0 ? "#FFFFFF" : "#EEF2F7").Padding(2).Text($"Row {i} first column");
                        r.Cell().Padding(2).Text($"{i * 3.14159:F3}");
                        r.Cell().Padding(2).AlignRight().Text($"value {i}");
                    });
            });
        }));

        byte[] parallel = document.PublishPdf();
        byte[] sequential;
        try
        {
            PdfDocument.ParallelCompressionEnabled = false;
            sequential = document.PublishPdf();
        }
        finally
        {
            PdfDocument.ParallelCompressionEnabled = true;
        }

        int pages = System.Text.RegularExpressions.Regex.Count(
            System.Text.Encoding.Latin1.GetString(parallel), "/Type /Page ");
        Assert.True(pages >= PdfDocument.ParallelCompressionMinPages);
        Assert.Equal(sequential, parallel);
    }
}
