using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

// =============================================================================
//  Tests for the five high-priority features:
//    1. Underline text
//    2. Hyperlinks (URI annotations)
//    3. Per-edge cell borders (BorderTop/Bottom/Left/Right)
//    4. LineHeight control
//    5. Underline on SpanDescriptor
// =============================================================================
public sealed class HighPriorityFeatureTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).GeneratePdf();

    private static string PdfHeader(byte[] b) =>
        System.Text.Encoding.ASCII.GetString(b, 0, 5);

    // ── 1. Underline ──────────────────────────────────────────────────────

    [Fact]
    public void UnderlineOnTextDescriptorRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Text("Underlined text").Underline();
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void UnderlineOnSpanDescriptorRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Text(t =>
            {
                t.Span("Normal ");
                t.Span("Underlined").Underline().FontColor(Color.Blue.Medium);
                t.Span(" Normal");
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void UnderlineAndStrikethroughTogetherRender()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Text(t =>
            {
                t.Span("Under").Underline();
                t.Span("Strike").Strikethrough();
                t.Span("Both").Underline().Strikethrough();
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void UnderlineStyleStoredInTextStyle()
    {
        var style = new TextStyle().Underline();
        Assert.True(style.IsUnderline);
    }

    [Fact]
    public void NoUnderlineClearsFlag()
    {
        var style = new TextStyle().Underline().NoUnderline();
        Assert.False(style.IsUnderline);
    }

    [Fact]
    public void UnderlineMergesCorrectly()
    {
        var baseStyle     = new TextStyle();
        var overrideStyle = new TextStyle().Underline();
        var merged        = baseStyle.MergeWith(overrideStyle);
        Assert.True(merged.IsUnderline);
    }

    // ── 2. Hyperlinks (URI annotations) ──────────────────────────────────

    [Fact]
    public void HyperlinkRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content()
             .Hyperlink("https://terrapdf.example")
             .Text("Click here");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void HyperlinkAnnotationAppearsInPdfBytes()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content()
             .Hyperlink("https://example.com/unique-marker")
             .Text("Link");
        }));

        // The URL must appear as a PDF string literal somewhere in the file
        string pdfText = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("https://example.com/unique-marker", pdfText);
        Assert.Contains("/URI", pdfText);
        Assert.Contains("/Annots", pdfText);
    }

    [Fact]
    public void MultipleHyperlinksOnSamePageRender()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().Column(col =>
            {
                col.Spacing(8);
                col.Item().Hyperlink("https://one.example").Text("Link one");
                col.Item().Hyperlink("https://two.example").Text("Link two");
                col.Item().Hyperlink("https://three.example").Text("Link three");
            });
        }));

        string pdfText = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("https://one.example",   pdfText);
        Assert.Contains("https://two.example",   pdfText);
        Assert.Contains("https://three.example", pdfText);
    }

    [Fact]
    public void HyperlinkNullUrlThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Build(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Hyperlink(null!).Text("x");
            })));

    [Fact]
    public void HyperlinkWhitespaceUrlThrows() =>
        Assert.Throws<ArgumentException>(() =>
            Build(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Hyperlink("   ").Text("x");
            })));

    // ── 3. Per-edge cell borders ──────────────────────────────────────────

    [Fact]
    public void BorderTopRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().BorderTop(1).Padding(6).Text("Top border only");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void BorderBottomRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().BorderBottom(1.5, Color.Blue.Medium).Text("Bottom border");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void BorderLeftRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().BorderLeft(3, Color.Red.Medium).PaddingLeft(8).Text("Left accent");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void BorderRightRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content().BorderRight(1).Text("Right border");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void PartialBordersInTableCellsRender()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(1, Unit.Centimetre);
            p.Content().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                });
                table.Row(row =>
                {
                    // Only a bottom border — typical header-separator style
                    row.Cell().BorderBottom(1.5, Color.Grey.Darken2).Padding(6).Text("Col A").Bold();
                    row.Cell().BorderBottom(1.5, Color.Grey.Darken2).Padding(6).Text("Col B").Bold();
                });
                table.Row(row =>
                {
                    row.Cell().BorderBottom(0.5, Color.Grey.Lighten2).Padding(6).Text("Value 1");
                    row.Cell().BorderBottom(0.5, Color.Grey.Lighten2).Padding(6).Text("Value 2");
                });
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void BorderTopNegativeWidthThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Build(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().BorderTop(-1).Text("x");
            })));

    // ── 4. LineHeight control ─────────────────────────────────────────────

    [Fact]
    public void LineHeightTightRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content()
             .Text("Line one\nLine two\nLine three")
             .LineHeight(1.0);
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void LineHeightDoubleSpacedRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Content()
             .Text("Double-spaced paragraph text here.")
             .LineHeight(2.0);
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void LineHeightViaDefaultTextStyleRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.6));
            p.Content().Text("Relaxed line spacing across the whole page.");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void LineHeightStoredInTextStyle()
    {
        var style = new TextStyle().LineHeight(1.8);
        Assert.Equal(1.8, style.LineHeightMultiplier);
    }

    [Fact]
    public void LineHeightMergesCorrectly()
    {
        var baseStyle     = new TextStyle().LineHeight(1.4);
        var overrideStyle = new TextStyle().LineHeight(2.0);
        var merged        = baseStyle.MergeWith(overrideStyle);
        Assert.Equal(2.0, merged.LineHeightMultiplier);
    }

    [Fact]
    public void LineHeightZeroOrNegativeThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Build(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("x").LineHeight(0);
            })));

    // ── Combined: all five features together ──────────────────────────────

    [Fact]
    public void AllHighPriorityFeaturesRenderTogether()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.5));

            p.Content().Column(col =>
            {
                col.Spacing(10);

                // Underline in a span
                col.Item().Text(t =>
                {
                    t.Span("Visit ").FontSize(11);
                    t.Span("TerraPDF").Underline().FontColor(Color.Blue.Medium);
                    t.Span(" for more info.").FontSize(11);
                });

                // Hyperlink wrapping text
                col.Item()
                   .Hyperlink("https://terrapdf.example")
                   .Text("Click to open TerraPDF website")
                   .Underline()
                   .FontColor(Color.Blue.Darken2);

                // Table with per-edge borders
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(1);
                    });
                    table.HeaderRow(row =>
                    {
                        row.Cell().BorderBottom(2, Color.Blue.Darken2).Padding(5)
                           .Text("Product").Bold();
                        row.Cell().BorderBottom(2, Color.Blue.Darken2).Padding(5)
                           .Text("Price").Bold();
                    });
                    table.Row(row =>
                    {
                        row.Cell().BorderBottom(0.5, Color.Grey.Lighten2).Padding(5)
                           .Text("TerraPDF Core");
                        row.Cell().BorderBottom(0.5, Color.Grey.Lighten2).Padding(5)
                           .AlignRight().Text("$199");
                    });
                });
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }
}
