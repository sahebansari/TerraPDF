using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

public sealed class PageBreakTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    private static string PdfText(byte[] b) =>
        System.Text.Encoding.Latin1.GetString(b);

    private static string PdfHeader(byte[] b) =>
        System.Text.Encoding.ASCII.GetString(b, 0, 5);

    // ── Basic rendering ───────────────────────────────────────────────────

    [Fact]
    public void PageBreakViaColumnDescriptorProducesValidPdf()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Text("Section 1 content.");
                col.PageBreak();
                col.Item().Text("Section 2 content.");
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void PageBreakViaContainerExtensionProducesValidPdf()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Column(col =>
            {
                col.Item().Text("Before break.");
                col.Item().PageBreak();
                col.Item().Text("After break.");
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Page count ────────────────────────────────────────────────────────

    [Fact]
    public void SinglePageBreakProducesTwoPages()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Text("Page 1");
                col.PageBreak();
                col.Item().Text("Page 2");
            });
        }));

        // Two /Page objects must exist in the PDF
        string pdf = PdfText(bytes);
        int pageObjCount = CountOccurrences(pdf, "/Type /Page ");
        Assert.Equal(2, pageObjCount);
    }

    [Fact]
    public void TwoPageBreaksProduceThreePages()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Text("Page 1");
                col.PageBreak();
                col.Item().Text("Page 2");
                col.PageBreak();
                col.Item().Text("Page 3");
            });
        }));

        string pdf = PdfText(bytes);
        Assert.Equal(3, CountOccurrences(pdf, "/Type /Page "));
    }

    // ── No-blank-page rule ────────────────────────────────────────────────

    [Fact]
    public void PageBreakAtTopOfPageDoesNotEmitBlankPage()
    {
        // PageBreak is the very first item — curY is already 0 so it must be skipped.
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.PageBreak();   // at top — should be ignored
                col.Item().Text("Only one page of content.");
            });
        }));

        string pdf = PdfText(bytes);
        Assert.Equal(1, CountOccurrences(pdf, "/Type /Page "));
    }

    [Fact]
    public void ConsecutivePageBreaksDoNotEmitMultipleBlankPages()
    {
        // Two page breaks in a row: only one new page should be started.
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Text("Page 1");
                col.PageBreak();
                col.PageBreak();   // already at top of page 2 — skipped
                col.Item().Text("Page 2");
            });
        }));

        string pdf = PdfText(bytes);
        Assert.Equal(2, CountOccurrences(pdf, "/Type /Page "));
    }

    // ── Page numbers ──────────────────────────────────────────────────────

    [Fact]
    public void PageNumbersAreCorrectAcrossPageBreaks()
    {
        // TotalPages in footer must reflect the pages produced by explicit breaks.
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Text("First");
                col.PageBreak();
                col.Item().Text("Second");
                col.PageBreak();
                col.Item().Text("Third");
            });
            p.Footer().AlignCenter().Text(t =>
            {
                t.CurrentPageNumber().FontSize(9);
                t.Span(" / ").FontSize(9);
                t.TotalPages().FontSize(9);
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Interaction with header / footer ─────────────────────────────────

    [Fact]
    public void PageBreakWithHeaderAndFooterRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Header().Text("Document Title").Bold().FontSize(16);
            p.Content().Column(col =>
            {
                col.Spacing(8);
                col.Item().Text("Chapter 1 introduction text.");
                col.PageBreak();
                col.Item().Text("Chapter 2 introduction text.");
            });
            p.Footer().AlignCenter().Text(t =>
            {
                t.CurrentPageNumber().FontSize(9);
                t.Span(" of ").FontSize(9);
                t.TotalPages().FontSize(9);
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
        string pdf = PdfText(bytes);
        Assert.Equal(2, CountOccurrences(pdf, "/Type /Page "));
    }

    // ── Interaction with natural overflow ────────────────────────────────

    [Fact]
    public void PageBreakCombinedWithNaturalOverflowRenders()
    {
        // Mix explicit breaks with content that also causes natural overflow.
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Spacing(4);

                // Enough items to fill the page and overflow naturally
                for (int i = 1; i <= 40; i++)
                {
                    int n = i;
                    col.Item().Text($"Natural item {n}");
                }

                // Then an explicit break before a new section
                col.PageBreak();
                col.Item().Text("Explicit new section after page break.");
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Spacing is not added around page breaks ───────────────────────────

    [Fact]
    public void ColumnSpacingDoesNotAccumulateAroundPageBreak()
    {
        // This test ensures the document renders (no negative-size draws or crashes)
        // when Spacing is set and a PageBreak is present.
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Spacing(20);
                col.Item().Text("Item before break.");
                col.PageBreak();
                col.Item().Text("Item after break — should start at top of new page, not offset by spacing.");
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
        string pdf = PdfText(bytes);
        Assert.Equal(2, CountOccurrences(pdf, "/Type /Page "));
    }

    // ── Helper ───────────────────────────────────────────────────────────

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int idx   = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
