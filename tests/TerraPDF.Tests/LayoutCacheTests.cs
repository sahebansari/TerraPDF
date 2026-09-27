using TerraPDF.Core;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Guards the performance shortcuts in the layout and image pipeline: memoised text
/// and table layouts are scoped to a single publish, the page-count re-layout is
/// skipped only when nothing depends on it, and PNGs are decoded once per distinct
/// image while still being validated when the document is composed.
/// </summary>
public sealed class LayoutCacheTests
{
    private static string Raw(byte[] b) => System.Text.Encoding.Latin1.GetString(b);

    private static int CountOccurrences(string text, string token)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(token, idx, StringComparison.Ordinal)) >= 0) { count++; idx += token.Length; }
        return count;
    }

    [Fact]
    public void PublishingTwiceProducesIdenticalOutput()
    {
        var doc = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Footer().Text(t =>
            {
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
            p.Content().Column(col =>
            {
                for (int i = 0; i < 40; i++)
                    col.Item().Text($"Paragraph {i} with enough words to wrap across more than one line of the page.");
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(2); });
                    t.HeaderRow(r => { r.Cell().Text("Key"); r.Cell().Text("Value"); });
                    for (int i = 0; i < 80; i++)
                        t.Row(r => { r.Cell().Text($"k{i}"); r.Cell().Text($"value number {i}"); });
                });
            });
        }));

        Assert.Equal(doc.PublishPdf(), doc.PublishPdf());
    }

    [Fact]
    public void ContentAddedAfterFirstPublishAppearsInSecondPublish()
    {
        TextDescriptor? text = null;
        var doc = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Text(t => { text = t; t.Span("first"); });
        }));

        string before = PdfTestUtils.InflatedText(doc.PublishPdf());
        text!.Span(" appended");
        string after = PdfTestUtils.InflatedText(doc.PublishPdf());

        Assert.DoesNotContain("appended", before);
        Assert.Contains("appended", after);
    }

    [Theory]
    [InlineData(3)]     // fewer digits than the initial page-count hint (99)
    [InlineData(120)]   // more digits than the initial hint
    public void TotalPagesIsExactWhenDigitCountDiffersFromHint(int pages)
    {
        byte[] pdf = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Footer().Text(t => t.TotalPages());
            p.Content().Column(col =>
            {
                for (int i = 0; i < pages; i++)
                {
                    col.Item().Text($"Page body {i}");
                    if (i < pages - 1) col.Item().PageBreak();
                }
            });
        })).PublishPdf();

        string content = PdfTestUtils.InflatedText(pdf);
        Assert.Equal(pages, CountOccurrences(Raw(pdf), "/Type /Page /"));
        Assert.Equal(pages, CountOccurrences(content, $"({pages}) Tj"));
    }

    [Fact]
    public void SamePngFromSeparateArraysIsEmbeddedOnce()
    {
        byte[] png = TestImageData.MakePng(6, 6, rgba: true, alphaValue: 128);
        byte[] copy = (byte[])png.Clone();

        byte[] pdf = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Column(col =>
            {
                col.Item().Image(png, 50);
                col.Item().Image(copy, 50);
            });
        })).PublishPdf();

        // One colour image plus its one /SMask.
        Assert.Equal(2, CountOccurrences(Raw(pdf), "/Subtype /Image"));
        Assert.Equal(1, CountOccurrences(Raw(pdf), "/SMask "));
    }

    [Fact]
    public void UnsupportedPngIsRejectedWhenComposed()
    {
        byte[] png = TestImageData.MakePng(4, 4, rgba: false, alphaValue: 0);
        png[24] = 16; // IHDR bit depth (signature 8 + length 4 + type 4 + width 4 + height 4)

        Assert.Throws<NotSupportedException>(() =>
            Document.Create(c => c.Page(p => p.Content().Image(png))));
    }
}
