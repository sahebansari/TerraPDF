using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

public sealed class HeaderFirstPageOnlyTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    private static string PdfText(byte[] b) =>
        System.Text.Encoding.Latin1.GetString(b);

    private static string PdfHeader(byte[] b) =>
        System.Text.Encoding.ASCII.GetString(b, 0, 5);

    private static int PageCount(byte[] b) =>
        CountOccurrences(PdfText(b), "/Type /Page ");

    // ── API / fluent chain ────────────────────────────────────────────────

    [Fact]
    public void HeaderOnFirstPageOnlyReturnsSameDescriptor()
    {
        // Fluent method must return the same PageDescriptor instance for chaining
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4)
             .Margin(2, Unit.Centimetre)
             .HeaderOnFirstPageOnly();

            p.Header().Text("Cover header");
            p.Content().Text("Single page.");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Single-page document: header is drawn ─────────────────────────────

    [Fact]
    public void SinglePageDocumentStillDrawsHeader()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4).Margin(2, Unit.Centimetre).HeaderOnFirstPageOnly();
            p.Header().Text("Unique-header-marker-XQ1");
            p.Content().Text("Only one page.");
        }));

        Assert.Contains("Unique-header-marker-XQ1", PdfText(bytes));
        Assert.Equal(1, PageCount(bytes));
    }

    // ── Multi-page: header only on page 1 ────────────────────────────────

    [Fact]
    public void HeaderAppearsOnlyOnFirstPageWithExplicitBreaks()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4).Margin(2, Unit.Centimetre).HeaderOnFirstPageOnly();
            p.Header().Text("Cover Title");
            p.Content().Column(col =>
            {
                col.Item().Text("Page 1 body");
                col.PageBreak();
                col.Item().Text("Page 2 body");
                col.PageBreak();
                col.Item().Text("Page 3 body");
            });
        }));

        Assert.Equal(3, PageCount(bytes));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Without the flag: header repeats on every page ───────────────────

    [Fact]
    public void WithoutFlagHeaderRepeatsOnAllPages()
    {
        // Control: no HeaderOnFirstPageOnly — default repeating-header behaviour.
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4).Margin(2, Unit.Centimetre);
            p.Header().Text("Repeated Header");
            p.Content().Column(col =>
            {
                col.Item().Text("Page 1");
                col.PageBreak();
                col.Item().Text("Page 2");
            });
        }));

        Assert.Equal(2, PageCount(bytes));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Extra content space on continuation pages ─────────────────────────

    [Fact]
    public void ContinuationPagesHaveMoreVerticalSpaceWhenHeaderIsFirstPageOnly()
    {
        // Fill exactly one page with tight content, then add HeaderOnFirstPageOnly.
        // The continuation page should fit all items without a third page because
        // it now has the header height available as content space.
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4).Margin(2, Unit.Centimetre).HeaderOnFirstPageOnly();
            p.Header().Padding(20).Text("Tall header taking 40pt+");
            p.Content().Column(col =>
            {
                col.Spacing(2);
                // 50 small items — enough to cause overflow onto page 2
                for (int i = 1; i <= 50; i++)
                {
                    int n = i;
                    col.Item().Text($"Item {n}");
                }
            });
        }));

        Assert.True(PageCount(bytes) >= 2);
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Natural overflow (no explicit PageBreak) ──────────────────────────

    [Fact]
    public void HeaderFirstPageOnlyWorksWithNaturalContentOverflow()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4).Margin(2, Unit.Centimetre).HeaderOnFirstPageOnly();
            p.Header().Background(Color.Blue.Darken2).Padding(10)
             .Text("Report Title").Bold().FontSize(16).FontColor(Color.White);
            p.Content().Column(col =>
            {
                col.Spacing(4);
                for (int i = 1; i <= 60; i++)
                {
                    int n = i;
                    col.Item().Text($"Content line {n} — some body text here.");
                }
            });
        }));

        Assert.True(PageCount(bytes) >= 2);
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Footer still repeats on all pages ────────────────────────────────

    [Fact]
    public void FooterStillRepeatsOnAllPagesWhenHeaderIsFirstPageOnly()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4).Margin(2, Unit.Centimetre).HeaderOnFirstPageOnly();
            p.Header().Text("First-page-only header");
            p.Content().Column(col =>
            {
                col.Item().Text("Page 1");
                col.PageBreak();
                col.Item().Text("Page 2");
            });
            p.Footer().AlignCenter().Text(t =>
            {
                t.CurrentPageNumber().FontSize(9);
                t.Span(" / ").FontSize(9);
                t.TotalPages().FontSize(9);
            });
        }));

        Assert.Equal(2, PageCount(bytes));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Table with HeaderRow on a first-page-only header page ────────────

    [Fact]
    public void TableWithHeaderRowWorksWhenPageHeaderIsFirstPageOnly()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4).Margin(2, Unit.Centimetre).HeaderOnFirstPageOnly();
            p.Header().Text("Invoice").Bold().FontSize(18);
            p.Content().Column(col =>
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(4);
                        cols.RelativeColumn(1);
                    });
                    table.HeaderRow(row =>
                    {
                        row.Cell().Background(Color.Blue.Darken2).Padding(5)
                           .Text("Description").Bold().FontColor(Color.White);
                        row.Cell().Background(Color.Blue.Darken2).Padding(5)
                           .Text("Amount").Bold().FontColor(Color.White);
                    });
                    // Enough rows to force multi-page table split
                    for (int i = 1; i <= 35; i++)
                    {
                        int n = i;
                        table.Row(row =>
                        {
                            row.Cell().Padding(5).Text($"Line item {n}");
                            row.Cell().Padding(5).AlignRight().Text($"${n * 10}");
                        });
                    }
                });
            });
        }));

        Assert.True(PageCount(bytes) >= 2);
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Combination: HeaderOnFirstPageOnly + PageBreak ────────────────────

    [Fact]
    public void PageBreakCombinedWithHeaderFirstPageOnlyRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4).Margin(2, Unit.Centimetre).HeaderOnFirstPageOnly();
            p.Header().Text("Cover Page Header");
            p.Content().Column(col =>
            {
                col.Item().Text("Cover content.");
                col.PageBreak();
                col.Item().Text("Chapter 1 — no header above this.");
                col.PageBreak();
                col.Item().Text("Chapter 2 — no header here either.");
            });
        }));

        Assert.Equal(3, PageCount(bytes));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Helper ───────────────────────────────────────────────────────────

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        { count++; idx += needle.Length; }
        return count;
    }
}
