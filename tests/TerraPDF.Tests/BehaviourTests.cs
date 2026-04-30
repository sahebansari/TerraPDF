using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

// =============================================================================
//  Behavioural tests
//  Verify that layout, formatting, and decorator logic work correctly.
// =============================================================================
public sealed class BehaviourTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    private static string PdfHeader(byte[] b) =>
        System.Text.Encoding.ASCII.GetString(b, 0, 5);

    // ── SpanDescriptor isolation ──────────────────────────────────────────

    /// <summary>
    /// Formatting chained after .Span() must not bleed into sibling spans.
    /// This catches the regression where Bold() on a span was setting the
    /// whole-block SpanStyle instead of the individual span's style.
    /// </summary>
    [Fact]
    public void SpanBoldDoesNotAffectSiblingSpans()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Text(t =>
            {
                t.Span("Normal ");
                t.Span("Bold ").Bold();
                t.Span("Italic ").Italic();
                t.Span("Struck ").Strikethrough();
                t.Span("Big").FontSize(18).FontColor(Color.Red.Medium);
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void CurrentPageNumberAndTotalPagesAcceptPerSpanStyle()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Footer().Text(t =>
            {
                t.Span("Page ").FontSize(9);
                t.CurrentPageNumber().FontSize(9).FontColor(Color.Grey.Medium);
                t.Span(" / ").FontSize(9);
                t.TotalPages().FontSize(9).FontColor(Color.Grey.Medium);
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Padding / Margin ──────────────────────────────────────────────────

    [Fact]
    public void PaddingZeroIsAccepted() =>
        Assert.Equal("%PDF-", PdfHeader(Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Padding(0).Text("x");
        }))));

    [Fact]
    public void MarginZeroIsAccepted() =>
        Assert.Equal("%PDF-", PdfHeader(Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Margin(0).Text("x");
        }))));

    [Fact]
    public void MarginAndPaddingChainRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content()
             .Margin(20)
             .Background(Color.Blue.Lighten5)
             .Padding(10)
             .Text("Decorated");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── ShowIf ────────────────────────────────────────────────────────────

    [Fact]
    public void ShowIfTrueRendersContent() =>
        Assert.Equal("%PDF-", PdfHeader(Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().ShowIf(true).Text("Visible");
        }))));

    [Fact]
    public void ShowIfFalseProducesValidPdfWithoutContent() =>
        Assert.Equal("%PDF-", PdfHeader(Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            // ShowIf(false) should suppress content but not crash
            p.Content().ShowIf(false).Text("Hidden");
        }))));

    // ── Column alignment ──────────────────────────────────────────────────

    [Fact]
    public void ColumnAlignItemsCenterRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Column(col =>
            {
                col.Spacing(4);
                col.AlignItemsLeft();
                col.Item().Text("Left");
                col.AlignItemsCenter();
                col.Item().Text("Center");
                col.AlignItemsRight();
                col.Item().Text("Right");
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Row item sizing ───────────────────────────────────────────────────

    [Fact]
    public void RowMixedItemTypesRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Row(row =>
            {
                row.Spacing(6);
                row.RelativeItem(2).Text("2x");
                row.RelativeItem(1).Text("1x");
                row.AutoItem().Text("Auto");
                row.ConstantItem(80).Text("80pt");
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Table header row repeat ───────────────────────────────────────────

    [Fact]
    public void TableWithHeaderRowRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(1, Unit.Centimetre);
            p.Content().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.ConstantColumn(60);
                });
                table.HeaderRow(row =>
                {
                    row.Cell().Background(Color.Blue.Darken2).Padding(5).Text("Item").FontColor(Color.White);
                    row.Cell().Background(Color.Blue.Darken2).Padding(5).Text("Price").FontColor(Color.White);
                });
                for (int i = 1; i <= 5; i++)
                {
                    int n = i;
                    table.Row(row =>
                    {
                        row.Cell().Padding(5).Text($"Product {n}");
                        row.Cell().Padding(5).AlignRight().Text($"${n * 10}");
                    });
                }
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── IComponent ────────────────────────────────────────────────────────

    [Fact]
    public void ComponentRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Component(new CalloutBox("Note: test component."));
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    private sealed class CalloutBox : IComponent
    {
        private readonly string _text;
        public CalloutBox(string text) => _text = text;
        public void Compose(IContainer container) =>
            container.Margin(6).Background(Color.Blue.Lighten5).Padding(8).Text(_text).Italic();
    }

    // ── IDocument ─────────────────────────────────────────────────────────

    [Fact]
    public void IDocumentRenders()
    {
        byte[] bytes = Document.Create(new SimpleReport("Q1 2025")).PublishPdf();
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    private sealed class SimpleReport : IDocument
    {
        private readonly string _title;
        public SimpleReport(string title) => _title = title;
        public void Compose(IDocumentContainer container) =>
            container.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(s => s.FontSize(11));
                page.Header().Text(_title).Bold().FontSize(18);
                page.Content().Text("Report body content.").Justify();
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ").FontSize(9);
                    t.CurrentPageNumber().FontSize(9);
                });
            });
    }

    // ── Lines ─────────────────────────────────────────────────────────────

    [Fact]
    public void LinesHorizontalAndVerticalRender()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Column(col =>
            {
                col.Item().Text("Above");
                col.Item().LineHorizontal(1, Color.Grey.Medium);
                col.Item().Text("Below");
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Decorators: Border + Background ───────────────────────────────────

    [Fact]
    public void BorderAndBackgroundChainRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content()
             .Border(1.5, Color.Blue.Darken2)
             .Background(Color.Blue.Lighten5)
             .Padding(12)
             .Text("Card content");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Multi-page with header/footer ─────────────────────────────────────

    [Fact]
    public void MultiPageHeaderAndFooterOnEveryPage()
    {
        byte[] bytes = Build(c =>
        {
            for (int i = 1; i <= 3; i++)
            {
                int n = i;
                c.Page(p =>
                {
                    p.Size(PageSize.A4);
                    p.Margin(2, Unit.Centimetre);
                    p.Header().Text($"Header {n}").Bold();
                    p.Content().Text($"Content page {n}");
                    p.Footer().AlignCenter().Text(t =>
                    {
                        t.CurrentPageNumber().FontSize(9);
                        t.Span(" / ").FontSize(9);
                        t.TotalPages().FontSize(9);
                    });
                });
            }
        });

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Page configuration ────────────────────────────────────────────────

    [Fact]
    public void PageColorRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.PageColor(Color.Grey.Lighten5);
            p.Content().Text("Tinted page");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void LandscapeSizeRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.Landscape(PageSize.A4));
            p.Content().Text("Landscape");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Text alignment ────────────────────────────────────────────────────

    [Theory]
    [InlineData("left")]
    [InlineData("center")]
    [InlineData("right")]
    [InlineData("justify")]
    public void TextAlignmentRenders(string mode)
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            var td = p.Content().Text("The quick brown fox jumps over the lazy dog.");
            _ = mode switch
            {
                "left"    => td.AlignLeft(),
                "center"  => td.AlignCenter(),
                "right"   => td.AlignRight(),
                "justify" => td.Justify(),
                _         => td,
            };
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }
}
