using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Verifies that the pagination walker traverses every layout-transparent
/// decorator (via <c>Element.PassthroughChild</c>), so multi-page columns keep
/// paginating no matter which decorators wrap them.  Before this fix, wrapping
/// content in Margin / RoundedBorder / PartialBorder silently disabled
/// pagination and the content overflowed a single page.
/// </summary>
public sealed class WalkerPaginationTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    private static int CountPages(byte[] pdf)
    {
        string text = System.Text.Encoding.Latin1.GetString(pdf);
        int count = 0, idx = 0;
        while ((idx = text.IndexOf("/Type /Page /", idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx++;
        }
        return count;
    }

    private static byte[] BuildWrappedColumn(Func<IContainer, IContainer> wrap) =>
        Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            wrap(p.Content()).Column(col =>
            {
                col.Item().Text("Section 1");
                col.PageBreak();
                col.Item().Text("Section 2");
            });
        }));

    [Fact]
    public void PageBreakWorksThroughMarginWrapper()
    {
        byte[] bytes = BuildWrappedColumn(c => c.Margin(10));
        Assert.Equal(2, CountPages(bytes));
    }

    [Fact]
    public void PageBreakWorksThroughRoundedBorderWrapper()
    {
        byte[] bytes = BuildWrappedColumn(c => c.RoundedBorder(radius: 6));
        Assert.Equal(2, CountPages(bytes));
    }

    [Fact]
    public void PageBreakWorksThroughPartialBorderWrapper()
    {
        byte[] bytes = BuildWrappedColumn(c => c.BorderTop(1));
        Assert.Equal(2, CountPages(bytes));
    }

    [Fact]
    public void PageBreakWorksThroughStackedDecorators()
    {
        byte[] bytes = BuildWrappedColumn(c =>
            c.Margin(8).Background("#EEEEEE").RoundedBorder(radius: 4).Padding(6));
        Assert.Equal(2, CountPages(bytes));
    }

    [Fact]
    public void OverflowingColumnInsideMarginSplitsAcrossPages()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Margin(10).Column(col =>
            {
                for (int i = 0; i < 120; i++)
                    col.Item().Text($"Line {i} of a long document that must flow onto multiple pages.");
            });
        }));

        Assert.True(CountPages(bytes) > 1,
            "A 120-line column wrapped in Margin must paginate onto multiple pages.");
    }

    [Fact]
    public void HundredPlusPageDocumentWithPageNumberFooterIsStable()
    {
        // Page-number spans are measured with a placeholder sized from the real
        // page count (fixpoint in DocumentComposer), so a "Page X / Y" footer in
        // a 100+ page document must not wrap taller at draw time than the space
        // reserved at measure time. Both explicit page breaks below map 1:1 to
        // pages, so the count is exact and any measure/draw drift would shift it.
        const int sections = 130;

        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                for (int i = 1; i <= sections; i++)
                {
                    col.Item().Text($"Section {i}");
                    if (i < sections) col.PageBreak();
                }
            });
            p.Footer().AlignCenter().Text(t =>
            {
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        }));

        Assert.Equal(sections, CountPages(bytes));
        // The drawn total must be the real count, not a 2-digit placeholder.
        // (Content streams are compressed, so inflate before searching.)
        Assert.Contains("130", PdfTestUtils.InflatedText(bytes));
    }

    [Fact]
    public void HyperlinkWrapperIsNotTraversedSoAnnotationSurvives()
    {
        // Link is deliberately opaque to the walker: traversing it would make the
        // splitter bypass Link.Draw and drop the annotation. The link content
        // renders on one page and the URI annotation must be present.
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Hyperlink("https://example.com").Column(col =>
            {
                col.Item().Text("Linked");
            });
        }));

        string text = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("/URI", text);
    }
}
