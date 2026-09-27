using System.Globalization;
using TerraPDF.Barcodes;
using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Benchmarks.Scenarios;

/// <summary>
/// Document builders shared by the benchmarks. Each returns a composed but not yet
/// published document, so a benchmark measures composition + layout + serialization.
/// </summary>
internal static class Docs
{
    private const string Brand = "#1a4a8a";
    private const string LightBrand = "#EBF2FF";
    private const string Muted = "#6C757D";

    private static void StandardPage(PageDescriptor page)
    {
        page.Size(PageSize.A4);
        page.Margin(2, Unit.Centimetre);
        page.PageColor(Color.White);
        page.DefaultTextStyle(s => s.FontSize(11));
    }

    private static void Footer(PageDescriptor page) =>
        page.Footer().AlignCenter().Text(t =>
        {
            t.Span("Page ");
            t.CurrentPageNumber();
        });

    internal static DocumentComposer HelloWorld() =>
        Document.Create(doc => doc.Page(page =>
        {
            StandardPage(page);
            page.Content().Text("Hello PDF!");
        }));

    /// <summary>Wrapped paragraphs spanning roughly <paramref name="pages"/> pages.</summary>
    internal static DocumentComposer LongText(int pages, string? fontFamily = null, string paragraph = Text.Paragraph) =>
        Document.Create(doc => doc.Page(page =>
        {
            StandardPage(page);
            Footer(page);
            page.Content().Column(col =>
            {
                col.Spacing(8);
                int count = pages * Text.ParagraphsPerPage;
                for (int i = 0; i < count; i++)
                {
                    var text = col.Item().Text(paragraph);
                    if (fontFamily is not null) text.FontFamily(fontFamily);
                }
            });
        }));

    /// <summary>Paragraphs made of many short, individually styled spans.</summary>
    internal static DocumentComposer RichSpans(int paragraphs) =>
        Document.Create(doc => doc.Page(page =>
        {
            StandardPage(page);
            page.Content().Column(col =>
            {
                col.Spacing(6);
                string[] words = Text.Paragraph.Split(' ');
                for (int p = 0; p < paragraphs; p++)
                {
                    col.Item().Text(t =>
                    {
                        for (int w = 0; w < words.Length; w++)
                        {
                            var span = t.Span(words[w] + " ");
                            switch (w % 4)
                            {
                                case 0: span.Bold(); break;
                                case 1: span.Italic().FontColor(Brand); break;
                                case 2: span.FontSize(13); break;
                            }
                        }
                    });
                }
            });
        }));

    /// <summary>Simple 4-column table with a repeating header row.</summary>
    internal static DocumentComposer Table(int rows) =>
        Document.Create(doc => doc.Page(page =>
        {
            StandardPage(page);
            Footer(page);
            page.Content().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(5);
                    c.RelativeColumn(1);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                });
                table.HeaderRow(row =>
                {
                    row.Cell().Background(Brand).Padding(4).Text("Description").Bold().FontColor(Color.White);
                    row.Cell().Background(Brand).Padding(4).Text("Qty").Bold().FontColor(Color.White);
                    row.Cell().Background(Brand).Padding(4).AlignRight().Text("Unit").Bold().FontColor(Color.White);
                    row.Cell().Background(Brand).Padding(4).AlignRight().Text("Total").Bold().FontColor(Color.White);
                });
                for (int i = 0; i < rows; i++)
                {
                    string bg = (i & 1) == 0 ? Color.White : LightBrand;
                    int qty = i % 7 + 1;
                    decimal unit = 10m + i % 97;
                    table.Row(row =>
                    {
                        row.Cell().Background(bg).Padding(4).Text($"Line item number {i} — consulting services");
                        row.Cell().Background(bg).Padding(4).Text(qty.ToString(CultureInfo.InvariantCulture));
                        row.Cell().Background(bg).Padding(4).AlignRight().Text(unit.ToString("N2", CultureInfo.InvariantCulture));
                        row.Cell().Background(bg).Padding(4).AlignRight().Text((qty * unit).ToString("N2", CultureInfo.InvariantCulture));
                    });
                }
            });
        }));

    /// <summary>Table where every group of three rows shares a row-spanned first cell.</summary>
    internal static DocumentComposer TableWithSpans(int rows) =>
        Document.Create(doc => doc.Page(page =>
        {
            StandardPage(page);
            page.Content().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn(3);
                    c.RelativeColumn(3);
                });
                for (int i = 0; i < rows; i++)
                {
                    int group = i;
                    table.Row(row =>
                    {
                        if (group % 3 == 0)
                            row.Cell(rowSpan: 3).Background(LightBrand).Padding(4).Text($"Group {group / 3}");
                        if (group % 6 == 1)
                            row.Cell(columnSpan: 2).Padding(4).Text("Merged cell spanning two columns");
                        else
                        {
                            row.Cell().Padding(4).Text($"Row {group} col 2");
                            row.Cell().Padding(4).Text($"Row {group} col 3");
                        }
                    });
                }
            });
        }));

    /// <summary>Invoice-like mixed layout: header with columns, spans, table, totals, footer.</summary>
    internal static DocumentComposer Invoice(int lineItems, byte[]? logo = null) =>
        Document.Create(doc =>
        {
            doc.MetadataTitle("Invoice");
            doc.MetadataAuthor("TerraPDF Benchmarks");
            doc.Page(page =>
            {
                StandardPage(page);
                page.DefaultTextStyle(s => s.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Spacing(4);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            if (logo is not null) c.Item().Image(logo, 80);
                            else c.Item().Text("TerraPDF Co.").Bold().FontSize(18).FontColor(Brand);
                        });
                        row.AutoItem().AlignRight().Column(info =>
                        {
                            info.Item().AlignRight().Text("INVOICE").Bold().FontSize(26).FontColor(Brand);
                            info.Item().AlignRight().Text(t =>
                            {
                                t.Span("Invoice # ").FontColor(Muted);
                                t.Span("INV-2025-0042").Bold().FontColor(Brand);
                            });
                        });
                    });
                    col.Item().LineHorizontal(2, Brand);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("FROM").Bold().FontSize(8).FontColor(Muted);
                            c.Item().Text("TerraPDF Co. Ltd.").Bold().FontColor(Brand);
                            c.Item().Text("42 Innovation Drive, Suite 7");
                        });
                        row.RelativeItem().MarginLeft(12).Background(LightBrand).Padding(8).Column(c =>
                        {
                            c.Item().Text("BILL TO").Bold().FontSize(8).FontColor(Muted);
                            c.Item().Text("Acme Global Solutions Inc.").Bold().FontColor(Brand);
                            c.Item().Text("88 Commerce Blvd, Floor 12");
                        });
                    });
                });
                page.Content().PaddingVertical(0.6, Unit.Centimetre).Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(5);
                            c.RelativeColumn(1);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });
                        table.HeaderRow(row =>
                        {
                            row.Cell().Background(Brand).Padding(6).Text("Description").Bold().FontColor(Color.White);
                            row.Cell().Background(Brand).Padding(6).AlignCenter().Text("Qty").Bold().FontColor(Color.White);
                            row.Cell().Background(Brand).Padding(6).AlignRight().Text("Unit Price").Bold().FontColor(Color.White);
                            row.Cell().Background(Brand).Padding(6).AlignRight().Text("Total").Bold().FontColor(Color.White);
                        });
                        for (int i = 0; i < lineItems; i++)
                        {
                            string bg = (i & 1) == 0 ? Color.White : LightBrand;
                            table.Row(row =>
                            {
                                row.Cell().Background(bg).Padding(6).Text($"Web Application Development - Phase {i}");
                                row.Cell().Background(bg).Padding(6).AlignCenter().Text("1");
                                row.Cell().Background(bg).Padding(6).AlignRight().Text("$2,500.00");
                                row.Cell().Background(bg).Padding(6).AlignRight().Text("$2,500.00");
                            });
                        }
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(6);
                        row.RelativeItem(4).Column(totals =>
                        {
                            totals.Item().LineHorizontal(1, Color.Grey.Lighten2);
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Total").Bold();
                                r.RelativeItem().AlignRight().Text("$75,000.00").Bold().FontColor(Brand);
                            });
                        });
                    });
                });
                Footer(page);
            });
        });

    /// <summary>Grid of <paramref name="count"/> images, all from the same bytes.</summary>
    internal static DocumentComposer Images(byte[] image, int count) =>
        Document.Create(doc => doc.Page(page =>
        {
            StandardPage(page);
            page.Content().Column(col =>
            {
                col.Spacing(4);
                for (int i = 0; i < count; i += 4)
                {
                    col.Item().Row(row =>
                    {
                        row.Spacing(4);
                        for (int j = 0; j < 4; j++)
                            row.RelativeItem().Image(image);
                    });
                }
            });
        }));

    /// <summary>Page filled with <paramref name="count"/> QR codes.</summary>
    internal static DocumentComposer QrCodes(int count) =>
        Document.Create(doc => doc.Page(page =>
        {
            StandardPage(page);
            page.Content().Column(col =>
            {
                col.Spacing(4);
                for (int i = 0; i < count; i += 5)
                {
                    int start = i;
                    col.Item().Row(row =>
                    {
                        row.Spacing(4);
                        for (int j = 0; j < 5; j++)
                            row.RelativeItem().QrCode($"https://example.com/item/{start + j}", 90, QrErrorCorrectionLevel.M);
                    });
                }
            });
        }));

    /// <summary>Canvases dense with shapes, dashes, gradients and rotated text.</summary>
    internal static DocumentComposer DenseCanvas(int canvases) =>
        Document.Create(doc => doc.Page(page =>
        {
            StandardPage(page);
            page.Content().Column(col =>
            {
                col.Spacing(6);
                for (int n = 0; n < canvases; n++)
                {
                    col.Item().Canvas(160, c =>
                    {
                        for (int i = 0; i < 40; i++)
                        {
                            double x = i * 12;
                            c.Line(x, 0, x + 20, 150, Muted, 0.5, dashPattern: [3, 2]);
                            c.FillRect(x, 10 + i % 5 * 20, 10, 10, Brand, 0.6);
                            c.StrokeCircle(x + 5, 120, 5, Brand, 0.75);
                        }
                        c.FillPie(10, 10, 60, 60, -90, 120, Brand);
                        c.StrokePie(80, 10, 60, 60, -90, 270, Muted, 1.5, dashPattern: [8, 3, 2, 3]);
                        c.Path(p => p.Rect(160, 10, 110, 70).FillLinearGradient(LightBrand, Brand, 45));
                        c.Path(p => p.Circle(330, 45, 35).FillRadialGradient(Color.White, Brand));
                        c.Path(p => p.Polygon((390, 68), (430, 12), (470, 68)).Stroke(Brand, 1.5).Dash([1, 3]));
                        c.Text("rotated label", 200, 140, Brand, 9, angle: 30);
                    });
                }
            });
        }));

    internal static DocumentComposer Encrypted(DocumentComposer doc, EncryptionAlgorithm algorithm)
    {
        doc.Encrypt(new EncryptionOptions
        {
            UserPassword = "user",
            OwnerPassword = "owner",
            Algorithm = algorithm,
        });
        return doc;
    }
}
