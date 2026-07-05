using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for the fragment-based layout engine: decorator chrome repeated on
/// every page of a split column, count/render agreement via cached fragments,
/// and thread safety after removal of the static heading recorder.
/// </summary>
public sealed class FragmentEngineTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    // Content streams are Flate-compressed; inflate them before text asserts.
    private static string PdfText(byte[] b) => PdfTestUtils.InflatedText(b);

    private static int CountOccurrences(string text, string token)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += token.Length;
        }
        return count;
    }

    private static int CountPages(byte[] pdf) =>
        CountOccurrences(PdfText(pdf), "/Type /Page /");

    // ── Decorator chrome repeats on every page ────────────────────────────────

    [Fact]
    public void BackgroundChromeIsDrawnOnEveryPageOfSplitColumn()
    {
        // Pure red background → fill op "1.0000 0.0000 0.0000 rg" appears once
        // per page (nothing else in the document is red).
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Background("#FF0000").Column(col =>
            {
                col.Item().Text("Page one");
                col.PageBreak();
                col.Item().Text("Page two");
            });
        }));

        Assert.Equal(2, CountPages(bytes));
        Assert.Equal(2, CountOccurrences(PdfText(bytes), "1.0000 0.0000 0.0000 rg"));
    }

    [Fact]
    public void RoundedBorderChromeIsDrawnOnEveryPageOfSplitColumn()
    {
        // Distinctive stroke colour #204060 → "0.1255 0.2510 0.3765 RG" per page.
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().RoundedBorder(radius: 6, lineWidth: 2, hexColor: "#204060").Column(col =>
            {
                col.Item().Text("Page one");
                col.PageBreak();
                col.Item().Text("Page two");
            });
        }));

        Assert.Equal(2, CountPages(bytes));
        Assert.Equal(2, CountOccurrences(PdfText(bytes), "0.1255 0.2510 0.3765 RG"));
    }

    [Fact]
    public void SinglePageDecoratedColumnDrawsChromeExactlyOnce()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Background("#FF0000").Column(col =>
            {
                col.Item().Text("Only page");
            });
        }));

        Assert.Equal(1, CountPages(bytes));
        Assert.Equal(1, CountOccurrences(PdfText(bytes), "1.0000 0.0000 0.0000 rg"));
    }

    // ── Count/render agreement (cached fragments) ─────────────────────────────

    [Fact]
    public void TocDocumentFooterTotalMatchesActualPageCount()
    {
        byte[] bytes = Build(c =>
        {
            c.TableOfContents(p =>
            {
                p.Size(PageSize.A4);
                p.Margin(2, Unit.Centimetre);
            });
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Margin(2, Unit.Centimetre);
                p.Content().Column(col =>
                {
                    col.Item().H1("Alpha");
                    col.PageBreak();
                    col.Item().H1("Beta");
                    col.PageBreak();
                    col.Item().H1("Gamma");
                });
                p.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        });

        // 1 TOC page + 3 content pages.
        int pages = CountPages(bytes);
        Assert.Equal(4, pages);

        // The rendered TotalPages span must equal the actual page count.
        Assert.Contains($"({pages}) Tj", PdfText(bytes));
    }

    // ── Thread safety (static heading recorder removed) ───────────────────────

    [Fact]
    public void ConcurrentTocDocumentsDoNotCrossContaminate()
    {
        const int docs = 8;
        var results = new byte[docs][];

        Parallel.For(0, docs, i =>
        {
            results[i] = Build(c =>
            {
                c.TableOfContents(p =>
                {
                    p.Size(PageSize.A4);
                    p.Margin(2, Unit.Centimetre);
                });
                c.Page(p =>
                {
                    p.Size(PageSize.A4);
                    p.Margin(2, Unit.Centimetre);
                    p.Content().Column(col =>
                    {
                        col.Item().H1($"UniqueHeadingDoc{i}A");
                        col.PageBreak();
                        col.Item().H1($"UniqueHeadingDoc{i}B");
                    });
                });
            });
        });

        for (int i = 0; i < docs; i++)
        {
            string pdf = PdfText(results[i]);

            // Own headings present (in body and in the generated TOC).
            Assert.Contains($"UniqueHeadingDoc{i}A", pdf);
            Assert.Contains($"UniqueHeadingDoc{i}B", pdf);

            // No heading from any other concurrently generated document.
            for (int j = 0; j < docs; j++)
            {
                if (j == i) continue;
                Assert.DoesNotContain($"UniqueHeadingDoc{j}A", pdf);
                Assert.DoesNotContain($"UniqueHeadingDoc{j}B", pdf);
            }
        }
    }
}
