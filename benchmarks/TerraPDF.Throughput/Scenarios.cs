using System.Globalization;
using TerraPDF.Barcodes;
using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Throughput;

/// <summary>
/// The two documents measured. Only public API that exists in every compared version
/// of TerraPDF is used, and the content is deterministic.
/// </summary>
internal static class Scenarios
{
    private const string Brand = "#1a4a8a";
    private const string Light = "#EBF2FF";
    private const string Muted = "#6C757D";

    private static readonly byte[] Logo = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "header_logo.png"));
    private static readonly string[] YearHeaders = ["Indicator", "2024", "2025", "2026"];
    private static readonly byte[] Photo = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "small_logo.jpg"));

    private const string Paragraph =
        "Revenue grew steadily across all regions this year, driven by strong demand for our core " +
        "products and the successful launch of two new service lines. Operating costs were kept under " +
        "control thanks to process automation, while investment in research and development continued " +
        "at a sustained pace. The board remains confident in the outlook for the coming financial year.";

    /// <summary>
    /// One-page invoice: PNG logo, addresses, a 12-line item table, totals, a payment QR code
    /// and a footer with page numbers — the typical transactional document.
    /// </summary>
    internal static DocumentComposer Invoice(int number) => Document.Create(doc =>
    {
        doc.MetadataTitle($"Invoice INV-{number:D6}");
        doc.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(1.8, Unit.Centimetre);
            page.DefaultTextStyle(s => s.FontSize(9.5));

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Image(Logo, 150);
                    c.Item().PaddingTop(6).Text("TerraPDF Co. Ltd. — 42 Innovation Drive, San Francisco").FontColor(Muted);
                });
                row.AutoItem().AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text("INVOICE").Bold().FontSize(22).FontColor(Brand);
                    c.Item().AlignRight().Text(t => { t.Span("No. ").FontColor(Muted); t.Span($"INV-{number:D6}").Bold(); });
                    c.Item().AlignRight().Text("Date: 26 September 2026");
                });
            });

            page.Content().PaddingVertical(12).Column(col =>
            {
                col.Spacing(10);
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("BILL TO").Bold().FontSize(8).FontColor(Muted);
                        c.Item().Text("Acme Global Solutions Inc.").Bold();
                        c.Item().Text("88 Commerce Blvd, Floor 12, New York, NY 10001");
                    });
                    row.RelativeItem().Background(Light).Padding(8).Column(c =>
                    {
                        c.Item().Text("PAYMENT").Bold().FontSize(8).FontColor(Muted);
                        c.Item().Text("IBAN FR76 3000 6000 0112 3456 7890 189");
                        c.Item().Text("Due: 26 October 2026").Bold().FontColor(Color.Red.Darken1);
                    });
                });

                decimal total = 0;
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(5); c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(2); });
                    table.HeaderRow(r =>
                    {
                        r.Cell().Background(Brand).Padding(5).Text("Description").Bold().FontColor(Color.White);
                        r.Cell().Background(Brand).Padding(5).AlignCenter().Text("Qty").Bold().FontColor(Color.White);
                        r.Cell().Background(Brand).Padding(5).AlignRight().Text("Unit price").Bold().FontColor(Color.White);
                        r.Cell().Background(Brand).Padding(5).AlignRight().Text("Amount").Bold().FontColor(Color.White);
                    });
                    for (int i = 0; i < 12; i++)
                    {
                        int qty = i % 4 + 1;
                        decimal unit = 45m + i * 17.5m;
                        total += qty * unit;
                        string bg = i % 2 == 0 ? Color.White : Light;
                        table.Row(r =>
                        {
                            r.Cell().Background(bg).Padding(5).Text($"Consulting services, work package {i + 1}");
                            r.Cell().Background(bg).Padding(5).AlignCenter().Text(qty.ToString(CultureInfo.InvariantCulture));
                            r.Cell().Background(bg).Padding(5).AlignRight().Text(unit.ToString("N2", CultureInfo.InvariantCulture));
                            r.Cell().Background(bg).Padding(5).AlignRight().Text((qty * unit).ToString("N2", CultureInfo.InvariantCulture));
                        });
                    }
                });

                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Scan to pay").Bold().FontSize(8).FontColor(Muted);
                        c.Item().QrCode($"https://pay.example.com/invoice/INV-{number:D6}?amount={total:F2}", 90, QrErrorCorrectionLevel.M);
                    });
                    row.ConstantItem(200).Column(c =>
                    {
                        c.Item().Row(r => { r.RelativeItem().Text("Subtotal"); r.RelativeItem().AlignRight().Text(total.ToString("N2", CultureInfo.InvariantCulture)); });
                        c.Item().Row(r => { r.RelativeItem().Text("VAT 20%"); r.RelativeItem().AlignRight().Text((total * 0.2m).ToString("N2", CultureInfo.InvariantCulture)); });
                        c.Item().LineHorizontal(1, Muted);
                        c.Item().Row(r => { r.RelativeItem().Text("Total").Bold(); r.RelativeItem().AlignRight().Text((total * 1.2m).ToString("N2", CultureInfo.InvariantCulture)).Bold().FontColor(Brand); });
                    });
                });

                col.Item().Text("Payment terms: 30 days. Late payments incur interest at three times the legal rate " +
                                "and a fixed recovery fee of EUR 40. Thank you for your business.").FontColor(Muted).FontSize(8);
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        });
    });

    /// <summary>
    /// A 19-page annual report: cover with logo, then chapters of body text, financial tables
    /// and vector charts, with running header and "Page x of y" footer.
    /// </summary>
    internal static DocumentComposer AnnualReport() => Document.Create(doc =>
    {
        doc.MetadataTitle("Annual Report 2026");
        doc.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(s => s.FontSize(10.5));

            page.Header().Row(row =>
            {
                row.RelativeItem().Text("TerraPDF Co. — Annual Report 2026").FontColor(Muted).FontSize(8);
                row.AutoItem().Image(Photo, 40);
            });
            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Page ").FontSize(8);
                t.CurrentPageNumber().FontSize(8);
                t.Span(" of ").FontSize(8);
                t.TotalPages().FontSize(8);
            });

            page.Content().Column(col =>
            {
                col.Spacing(8);
                col.Item().Image(Logo);
                col.Item().PaddingTop(40).Text("Annual Report 2026").Bold().FontSize(34).FontColor(Brand);
                col.Item().Text("Growth, resilience and long-term value").FontSize(16).FontColor(Muted);

                for (int chapter = 1; chapter <= 6; chapter++)
                {
                    col.Item().PageBreak();
                    col.Item().Text($"{chapter}. Chapter {chapter}: review of operations").Bold().FontSize(18).FontColor(Brand);
                    for (int p = 0; p < 6; p++)
                        col.Item().Text(Paragraph).Justify();

                    col.Item().Canvas(170, c =>
                    {
                        c.Text("Quarterly revenue (EUR m)", 0, 12, Muted, 9);
                        for (int q = 0; q < 12; q++)
                        {
                            double h = 30 + (q * 37 + chapter * 11) % 110;
                            c.FillRect(10 + q * 38, 160 - h, 26, h, q % 4 == 3 ? Brand : "#7FA3D9");
                            c.Text($"Q{q % 4 + 1}", 14 + q * 38, 168, Muted, 7);
                        }
                        c.FillPie(420, 25, 110, 110, -90, 140 + chapter * 10, Brand);
                        c.FillPie(420, 25, 110, 110, 50 + chapter * 10, 220 - chapter * 10, "#BFD1EC");
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); });
                        table.HeaderRow(r =>
                        {
                            foreach (string h in YearHeaders)
                                r.Cell().Background(Brand).Padding(4).Text(h).Bold().FontColor(Color.White);
                        });
                        for (int i = 0; i < 18; i++)
                        {
                            string bg = i % 2 == 0 ? Color.White : Light;
                            table.Row(r =>
                            {
                                r.Cell().Background(bg).Padding(4).Text($"Key figure {chapter}.{i + 1}");
                                for (int y = 0; y < 3; y++)
                                    r.Cell().Background(bg).Padding(4).AlignRight()
                                     .Text((1000 + chapter * 131 + i * 57 + y * 89).ToString("N0", CultureInfo.InvariantCulture));
                            });
                        }
                    });

                    for (int p = 0; p < 4; p++)
                        col.Item().Text(Paragraph);
                }
            });
        });
    });
}
