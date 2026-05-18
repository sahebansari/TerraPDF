using System.Globalization;
using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  3. INVOICE  (multi-page with repeating table header row)
//  Shows: logo, bill-from/to columns, 30-row line-items table that spans pages
//  with the header repeated on every page, totals block, payment terms,
//  due-date colour highlight, mixed text spans, page numbers.
// =============================================================================
internal static class Invoice
{
    internal static void Generate(string path)
    {
        const string brand      = "#1a4a8a";
        const string lightBrand = "#EBF2FF";
        const string muted      = "#6C757D";

        string logoSmall = Path.Combine(AppContext.BaseDirectory, "small_logo.jpg");

        // 30 line items - enough to force multi-page table splitting
        var items = new (string Desc, int Qty, decimal Unit)[]
        {
            ("Web Application Development - Phase 1",   1,  2500.00m),
            ("Web Application Development - Phase 2",   1,  2500.00m),
            ("UI/UX Design and Prototyping",            1,   800.00m),
            ("Server Infrastructure Setup",             1,   600.00m),
            ("Monthly Server Maintenance",             12,   150.00m),
            ("Database Design and Optimisation",        1,   450.00m),
            ("API Integration - Payment Gateway",       1,   350.00m),
            ("API Integration - Email Service",         1,   200.00m),
            ("API Integration - SMS Provider",          1,   200.00m),
            ("Performance Audit and Reporting",         1,   300.00m),
            ("Security Review and Penetration Test",    1,   700.00m),
            ("Accessibility Compliance Review",         1,   250.00m),
            ("Content Migration - Phase 1",             1,   400.00m),
            ("Content Migration - Phase 2",             1,   400.00m),
            ("SEO Configuration and Sitemap",           1,   180.00m),
            ("Analytics Integration",                   1,   150.00m),
            ("Admin Dashboard Module",                  1,   950.00m),
            ("Role and Permission Management Module",   1,   550.00m),
            ("Notification System Module",              1,   400.00m),
            ("File Storage and CDN Integration",        1,   350.00m),
            ("Multi-Language Support (3 languages)",    1,   600.00m),
            ("Dark-Mode Theme Implementation",          1,   200.00m),
            ("Mobile Responsive Optimisation",          1,   300.00m),
            ("Unit Test Suite - Backend",               1,   450.00m),
            ("Unit Test Suite - Frontend",              1,   350.00m),
            ("End-to-End Test Automation",              1,   500.00m),
            ("CI/CD Pipeline Configuration",            1,   400.00m),
            ("Technical Documentation",                 1,   350.00m),
            ("User Training Sessions (4 x 2 h)",        4,   200.00m),
            ("Post-Launch Support (30 days)",           1,   750.00m),
        };

        decimal subtotal = items.Sum(r => r.Qty * r.Unit);
        decimal tax      = Math.Round(subtotal * 0.10m, 2);
        decimal total    = subtotal + tax;

        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(10));

                // ── Header ───────────────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Spacing(4);

                    // Logo + invoice meta
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            if (File.Exists(logoSmall))
                                c.Item().Image(logoSmall, 80);
                            else
                                c.Item().Text("TerraPDF Co.").Bold().FontSize(18).FontColor(brand);
                        });

                        row.AutoItem().AlignRight().Column(info =>
                        {
                            info.Item().AlignRight()
                                .Text("INVOICE").Bold().FontSize(26).FontColor(brand);
                            info.Item().AlignRight().Text(t =>
                            {
                                t.Span("Invoice # ").FontColor(muted);
                                t.Span("INV-2025-0042").Bold().FontColor(brand);
                            });
                            info.Item().AlignRight().Text(t =>
                            {
                                t.Span("Date: ").FontColor(muted);
                                t.Span("June 15, 2025");
                            });
                            info.Item().AlignRight().Text(t =>
                            {
                                t.Span("Due:  ").FontColor(muted);
                                t.Span("July 15, 2025").Bold().FontColor(Color.Red.Darken1);
                            });
                        });
                    });

                    col.Item().LineHorizontal(2, brand);

                    // Bill-from / Bill-to
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("FROM").Bold().FontSize(8).FontColor(muted);
                            c.Item().Text("TerraPDF Co. Ltd.").Bold().FontColor(brand);
                            c.Item().Text("42 Innovation Drive, Suite 7");
                            c.Item().Text("San Francisco, CA 94105");
                            c.Item().Text("billing@terrapdf.example");
                        });

                        row.RelativeItem().MarginLeft(12).Background(lightBrand).Padding(8).Column(c =>
                        {
                            c.Item().Text("BILL TO").Bold().FontSize(8).FontColor(muted);
                            c.Item().Text("Acme Global Solutions Inc.").Bold().FontColor(brand);
                            c.Item().Text("Mr. Jonathan Harker, CFO");
                            c.Item().Text("88 Commerce Blvd, Floor 12");
                            c.Item().Text("New York, NY 10001");
                        });
                    });

                    col.Item().PaddingTop(4).LineHorizontal(1, Color.Grey.Lighten2);
                });

                // ── Content ──────────────────────────────────────────────────────
                page.Content().PaddingVertical(0.6, Unit.Centimetre).Column(col =>
                {
                    col.Spacing(6);

                    // Line-items table (HeaderRow repeats on every continuation page)
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(5);  // Description
                            c.RelativeColumn(1);  // Qty
                            c.RelativeColumn(2);  // Unit price
                            c.RelativeColumn(2);  // Line total
                        });

                        table.HeaderRow(row =>
                        {
                            row.Cell().Background(brand).Padding(6).Text("Description").Bold().FontColor(Color.White);
                            row.Cell().Background(brand).Padding(6).AlignCenter().Text("Qty").Bold().FontColor(Color.White);
                            row.Cell().Background(brand).Padding(6).AlignRight().Text("Unit Price").Bold().FontColor(Color.White);
                            row.Cell().Background(brand).Padding(6).AlignRight().Text("Total").Bold().FontColor(Color.White);
                        });

                        bool shade = false;
                        foreach (var (desc, qty, unit) in items)
                        {
                            string  bg   = shade ? lightBrand : Color.White;
                            decimal line = qty * unit;
                            table.Row(row =>
                            {
                                row.Cell().Background(bg).Padding(6).Text(desc);
                                row.Cell().Background(bg).Padding(6).AlignCenter().Text(qty.ToString(CultureInfo.InvariantCulture));
                                row.Cell().Background(bg).Padding(6).AlignRight().Text($"${unit:N2}");
                                row.Cell().Background(bg).Padding(6).AlignRight().Text($"${line:N2}");
                            });
                            shade = !shade;
                        }
                    });

                    // Totals block (right-aligned)
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(6);  // left spacer

                        row.RelativeItem(4).Column(totals =>
                        {
                            totals.Spacing(2);

                            void TLine(string label, string value,
                                       bool bold = false, string? color = null)
                            {
                                totals.Item().Row(r =>
                                {
                                    var lbl = r.RelativeItem().Text(label);
                                    if (bold) lbl.Bold();
                                    var val = r.RelativeItem().AlignRight().Text(value);
                                    if (bold) val.Bold();
                                    if (color is not null) val.FontColor(color);
                                });
                            }

                            totals.Item().LineHorizontal(1, Color.Grey.Lighten2);
                            TLine("Subtotal",  $"${subtotal:N2}");
                            TLine("Tax (10%)", $"${tax:N2}");
                            totals.Item().LineHorizontal(1.5, brand);
                            TLine("TOTAL DUE", $"${total:N2}", bold: true, color: brand);

                            totals.Item().Background(brand).Padding(6).AlignCenter()
                                  .Text($"Amount Due: ${total:N2}")
                                  .Bold().FontSize(12).FontColor(Color.White);
                        });
                    });

                    // Payment terms box
                    col.Item().MarginTop(8).Border(1, Color.Grey.Lighten2).Padding(8).Column(terms =>
                    {
                        terms.Item().Text("PAYMENT TERMS AND NOTES")
                             .Bold().FontColor(brand).FontSize(9);

                        terms.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("Payment method: ").Bold();
                            t.Span("Bank transfer (SWIFT / ACH).  ");
                            t.Span("Late payment: ").Bold();
                            t.Span("1.5% per month after due date.");
                        });

                        terms.Item().PaddingTop(2).Text(
                            "All amounts are in USD. This invoice is issued under the Master " +
                            "Services Agreement dated January 10, 2025. Disputes must be raised " +
                            "in writing within 14 days of receipt.")
                            .FontColor(muted).FontSize(9);
                    });
                });

                // ── Footer ───────────────────────────────────────────────────────
                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1, Color.Grey.Lighten2);
                    col.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem()
                           .Text("Thank you for your business!")
                           .FontColor(muted).FontSize(9);

                        row.AutoItem().AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontSize(9).FontColor(muted);
                            t.CurrentPageNumber().FontSize(9).FontColor(muted);
                            t.Span(" / ").FontSize(9).FontColor(muted);
                            t.TotalPages().FontSize(9).FontColor(muted);
                        });
                    });
                });
            });
        }).PublishPdf(path);

        Console.WriteLine($"  [3] Invoice               -> {path}");
    }
}
