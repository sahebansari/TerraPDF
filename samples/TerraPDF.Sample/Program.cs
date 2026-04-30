using System.Globalization;
using TerraPDF.Core;
using TerraPDF.Helpers;

// ─────────────────────────────────────────────────────────────────────────────
//  TerraPDF Samples  –  seven PDFs from simple to complex, saved to Desktop.
// ─────────────────────────────────────────────────────────────────────────────

string desktop  = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
string imgDir   = AppContext.BaseDirectory;          // images live next to the exe
string headerImg = Path.Combine(imgDir, "header_logo.png");
string smallImg  = Path.Combine(imgDir, "small_logo.jpg");

GenerateSimpleReport        (Path.Combine(desktop, "01_simple_report.pdf"));
GenerateStyledNewsletter    (Path.Combine(desktop, "02_newsletter.pdf"));
GenerateInvoice             (Path.Combine(desktop, "03_invoice.pdf"));
GenerateCompanyProfile      (Path.Combine(desktop, "04_company_profile.pdf"),        headerImg, smallImg);
GenerateEventBrochure       (Path.Combine(desktop, "05_event_brochure.pdf"),         headerImg, smallImg);
GenerateProductCatalogue    (Path.Combine(desktop, "06_product_catalogue.pdf"),      headerImg, smallImg);
GenerateProductCatalogCover (Path.Combine(desktop, "07_product_catalog_cover.pdf"),  smallImg);

Console.WriteLine("All PDFs saved to Desktop.");

// =============================================================================
//  1. SIMPLE REPORT
//  Shows: bold / italic / strikethrough / colour spans, justified paragraphs,
//  horizontal rules, multi-span mixed text, alternating-row table, page numbers.
// =============================================================================
static void GenerateSimpleReport(string path)
{
    const string accent = "#2E4057";
    const string light  = "#F0F4F8";

    Document.Create(doc =>
    {
        doc.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(2.5, Unit.Centimetre);
            page.PageColor(Color.White);
            page.DefaultTextStyle(s => s.FontSize(11));

            // ── Header ───────────────────────────────────────────────────────
            page.Header().Column(col =>
            {
                col.Item().Background(accent).Padding(10)
                   .Text("ANNUAL PERFORMANCE REPORT")
                   .Bold().FontSize(16).FontColor(Color.White).AlignCenter();

                col.Item().PaddingTop(4)
                   .Text("Fiscal Year 2025  |  Prepared by Finance Division")
                   .FontColor(Color.Grey.Medium).AlignCenter();

                col.Item().PaddingTop(6).LineHorizontal(1.5, accent);
            });

            // ── Content ──────────────────────────────────────────────────────
            page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
            {
                col.Spacing(8);

                // Helper: coloured section heading
                void H(string title) =>
                    col.Item().MarginVertical(6).Background(light).Padding(6)
                       .Text(title).Bold().FontSize(13).FontColor(accent);

                // 1. Executive summary
                H("1. Executive Summary");

                col.Item().Text(
                    "This report summarises the financial and operational performance of the " +
                    "organisation for fiscal year 2025. Overall revenue increased by 12% " +
                    "year-on-year while operating costs were held flat, resulting in an improved " +
                    "EBITDA margin of 28%. The board considers these results to be in line with " +
                    "the medium-term strategic plan approved in 2023.").Justify();

                col.Item().Text(t =>
                {
                    t.Span("Key highlight: ").Bold();
                    t.Span("Net profit reached $4.2 M, the highest in the company's history.");
                });

                // 2. Text formatting
                H("2. Text Formatting Showcase");

                col.Item().Text(t =>
                {
                    t.Span("Normal  ");
                    t.Span("Bold  ").Bold();
                    t.Span("Italic  ").Italic();
                    t.Span("Strikethrough  ").Strikethrough();
                    t.Span("Coloured  ").FontColor(Color.Blue.Medium);
                    t.Span("Large").FontSize(16).FontColor(accent);
                });

                col.Item().AlignCenter()
                   .Text("Centred heading text").Bold().FontSize(13).FontColor(accent);

                col.Item().AlignRight()
                   .Text("Right-aligned annotation").Italic().FontColor(Color.Grey.Darken1);

                // 3. Table
                H("3. Department Revenue Summary");

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });

                    table.HeaderRow(row =>
                    {
                        row.Cell().Background(accent).Padding(5).Text("Department").Bold().FontColor(Color.White);
                        row.Cell().Background(accent).Padding(5).AlignRight().Text("Q1-Q2 ($K)").Bold().FontColor(Color.White);
                        row.Cell().Background(accent).Padding(5).AlignRight().Text("Q3-Q4 ($K)").Bold().FontColor(Color.White);
                        row.Cell().Background(accent).Padding(5).AlignRight().Text("Total ($K)").Bold().FontColor(Color.White);
                    });

                    (string Name, string H1, string H2, string Tot)[] depts =
                    [
                        ("Sales",       "1,840", "2,110", "3,950"),
                        ("Engineering", "  620", "  580", "1,200"),
                        ("Marketing",   "  310", "  290", "  600"),
                        ("Support",     "  200", "  210", "  410"),
                    ];

                    bool shade = false;
                    foreach (var d in depts)
                    {
                        string bg = shade ? light : Color.White;
                        table.Row(row =>
                        {
                            row.Cell().Background(bg).Padding(5).Text(d.Name);
                            row.Cell().Background(bg).Padding(5).AlignRight().Text(d.H1);
                            row.Cell().Background(bg).Padding(5).AlignRight().Text(d.H2);
                            row.Cell().Background(bg).Padding(5).AlignRight().Text(d.Tot).Bold();
                        });
                        shade = !shade;
                    }
                });

                // 4. Disclaimer
                H("4. Notes & Disclaimers");

                col.Item().Text(
                    "All figures are unaudited and subject to revision. Currency amounts are " +
                    "expressed in thousands of US dollars (USD) unless otherwise stated. " +
                    "This document is intended for internal use only and must not be distributed " +
                    "to third parties without prior written consent.")
                   .Justify().FontColor(Color.Grey.Darken1).FontSize(10);
            });

            // ── Footer ───────────────────────────────────────────────────────
            page.Footer().Column(col =>
            {
                col.Item().LineHorizontal(1, accent);
                col.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem()
                       .Text("(c) 2025 Acme Corporation - Confidential")
                       .FontColor(Color.Grey.Medium).FontSize(9);

                    row.AutoItem().AlignRight().Text(t =>
                    {
                        t.Span("Page ").FontSize(9).FontColor(Color.Grey.Medium);
                        t.CurrentPageNumber().FontSize(9).FontColor(Color.Grey.Medium);
                        t.Span(" of ").FontSize(9).FontColor(Color.Grey.Medium);
                        t.TotalPages().FontSize(9).FontColor(Color.Grey.Medium);
                    });
                });
            });
        });
    }).PublishPdf(path);

    Console.WriteLine($"  [1] Simple report    -> {path}");
}

// =============================================================================
//  2. STYLED NEWSLETTER
//  Shows: two-column row layout, coloured banners, pull-quote block, statistics
//  row with bordered boxes, In-Brief table, multi-page repeating footer.
// =============================================================================
static void GenerateStyledNewsletter(string path)
{
    const string primary   = "#1B4332";
    const string secondary = "#52B788";
    const string highlight = "#D8F3DC";
    const string muted     = "#6C757D";

    Document.Create(doc =>
    {
        doc.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(2, Unit.Centimetre);
            page.PageColor(Color.White);
            page.DefaultTextStyle(s => s.FontSize(11));

            // ── Header ───────────────────────────────────────────────────────
            page.Header().Column(col =>
            {
                col.Item().Background(primary).Padding(12).Row(row =>
                {
                    row.RelativeItem().AlignMiddle()
                       .Text("GreenTech Monthly").Bold().FontSize(20).FontColor(Color.White);

                    row.AutoItem().AlignRight().MarginTop(4)
                       .Text("Issue #47  -  April 2026").FontColor(secondary).FontSize(10);
                });
                // Thin coloured rule under masthead
                col.Item().Background(secondary).Padding(3);
            });

            // ── Content ──────────────────────────────────────────────────────
            page.Content().PaddingVertical(0.8, Unit.Centimetre).Column(col =>
            {
                col.Spacing(10);

                // Feature story banner
                col.Item().Background(highlight).Border(1, secondary).Padding(10).Column(inner =>
                {
                    inner.Item().Text("FEATURE STORY").Bold().FontSize(9).FontColor(primary);
                    inner.Item().PaddingTop(2)
                         .Text("Renewable Energy Hits Record 40% of Global Power in 2025")
                         .Bold().FontSize(15).FontColor(primary);
                    inner.Item().PaddingTop(4).Text(
                        "In a landmark achievement for the global energy transition, renewables " +
                        "accounted for more than 40% of total electricity generation in 2025, " +
                        "driven by unprecedented growth in solar and offshore wind capacity. " +
                        "This milestone surpassed projections by two years, signalling a decisive " +
                        "acceleration in the clean-energy shift across all major economies.")
                        .FontColor(Color.Grey.Darken2).Justify();
                });

                // Two-column articles
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(art =>
                    {
                        art.Item().Background(primary).Padding(5)
                           .Text("TECHNOLOGY").Bold().FontSize(8).FontColor(secondary);
                        art.Item().PaddingTop(4)
                           .Text("Solid-State Batteries Finally Go Commercial")
                           .Bold().FontSize(12).FontColor(primary);
                        art.Item().PaddingTop(4).PaddingRight(4).Text(
                            "Three major manufacturers announced production-ready solid-state " +
                            "cells this quarter, promising twice the energy density of lithium-ion " +
                            "at competitive cost. Volume shipments are expected by Q4 2025.").Justify();
                    });

                    // Vertical divider
                    row.ConstantItem(2).PaddingLeft(4).Background(secondary);

                    row.RelativeItem().PaddingLeft(8).Column(art =>
                    {
                        art.Item().Background(secondary).Padding(5)
                           .Text("POLICY").Bold().FontSize(8).FontColor(primary);
                        art.Item().PaddingTop(4)
                           .Text("G20 Nations Agree on Carbon Border Tax Framework")
                           .Bold().FontSize(12).FontColor(primary);
                        art.Item().PaddingTop(4).Text(
                            "After two years of negotiation, G20 finance ministers endorsed a " +
                            "harmonised carbon border adjustment mechanism set to take effect " +
                            "in January 2026. Analysts predict significant shifts in trade " +
                            "patterns for carbon-intensive industries.").Justify();
                    });
                });

                col.Item().LineHorizontal(1, secondary);

                // Pull quote
                col.Item().Margin(10)
                   .RoundedBox(radius: 12, fillHexColor: highlight,
                               borderHexColor: secondary, lineWidth: 1.5)
                   .Padding(16).Column(q =>
                {
                    q.Item().AlignCenter()
                     .Text("The energy transition is no longer a question of if - it is a question of how fast.")
                     .Italic().FontSize(13).FontColor(primary);
                    q.Item().PaddingTop(6).AlignRight()
                     .Text("- Dr. Amara Osei, IRENA Director General")
                     .FontSize(10).FontColor(muted);
                });

                // Statistics row
                col.Item().Row(row =>
                {
                    void Stat(string value, string label, string fillColor) =>
                        row.RelativeItem().Margin(4)
                           .RoundedBox(radius: 10, fillHexColor: fillColor,
                                       borderHexColor: secondary, lineWidth: 1.5)
                           .Padding(12).Column(s =>
                           {
                               s.Item().AlignCenter().Text(value).Bold().FontSize(22).FontColor(primary);
                               s.Item().AlignCenter().Text(label).FontSize(9).FontColor(muted);
                           });

                    Stat("40%",   "Renewables share",       highlight);
                    Stat("$2.8T", "Global clean investment", Color.White);
                    Stat("180M",  "EVs on the road",         highlight);
                    Stat("-18%",  "Carbon intensity drop",   Color.White);
                });

                col.Item().LineHorizontal(1, secondary);

                // In Brief table
                col.Item().Text("IN BRIEF").Bold().FontColor(primary);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(5); c.RelativeColumn(2); });

                    table.HeaderRow(row =>
                    {
                        row.Cell().Background(primary).Padding(5).Text("Story").Bold().FontColor(Color.White);
                        row.Cell().Background(primary).Padding(5).AlignCenter().Text("Category").Bold().FontColor(Color.White);
                    });

                    (string Story, string Cat)[] briefs =
                    [
                        ("EU mandates heat-pump installations in all new builds from 2026", "Policy"),
                        ("Ocean thermal energy conversion pilot exceeds efficiency targets", "Technology"),
                        ("Green hydrogen production costs fall below $2/kg for first time", "Energy"),
                        ("Antarctica ice-shelf monitoring network expanded to 400 sensors",  "Climate"),
                        ("Vertical farming sector attracts record $9B in 2025 investments",  "Agriculture"),
                        ("New global biodiversity treaty signed by 134 nations",             "Conservation"),
                    ];

                    bool shade = false;
                    foreach (var b in briefs)
                    {
                        string bg = shade ? highlight : Color.White;
                        table.Row(row =>
                        {
                            row.Cell().Background(bg).Padding(5).Text(b.Story);
                            row.Cell().Background(bg).Padding(5).AlignCenter()
                               .Text(b.Cat).Italic().FontColor(muted);
                        });
                        shade = !shade;
                    }
                });

                col.Item().PaddingTop(4).Text(
                    "Thank you for reading GreenTech Monthly. To subscribe, update your " +
                    "preferences, or access the full article archive, visit our website. " +
                    "Forward this newsletter to a colleague who cares about sustainability.")
                   .Justify().FontColor(muted).FontSize(10);
            });

            // ── Footer ───────────────────────────────────────────────────────
            page.Footer().Column(col =>
            {
                col.Item().Background(secondary).Padding(2);
                col.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem()
                       .Text("GreenTech Monthly  |  editor@greentech.example")
                       .FontSize(9).FontColor(muted);

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

    Console.WriteLine($"  [2] Newsletter        -> {path}");
}

// =============================================================================
//  3. INVOICE  (multi-page with repeating table header row)
//  Shows: logo, bill-from/to columns, 30-row line-items table that spans pages
//  with the header repeated on every page, totals block, payment terms,
//  due-date colour highlight, mixed text spans, page numbers.
// =============================================================================
static void GenerateInvoice(string path)
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

    Console.WriteLine($"  [3] Invoice           -> {path}");
}

// =============================================================================
//  4. COMPANY PROFILE
//  Shows: full-width header banner image, small logo in footer, about section,
//  key-metrics row, team table, services list, multi-page with logo footer.
// =============================================================================
static void GenerateCompanyProfile(string path, string headerImg, string smallImg)
{
    const string brand   = "#0D3349";
    const string accent  = "#F4A020";
    const string light   = "#F7F9FC";
    const string muted   = "#607080";

    Document.Create(doc =>
    {
        doc.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(0);                          // zero margin – we control spacing manually
            page.PageColor(Color.White);
            page.DefaultTextStyle(s => s.FontSize(11));

            // ── Header: full-width banner image ──────────────────────────
            page.Header().Column(col =>
            {
                // Full-width banner
                if (File.Exists(headerImg))
                    col.Item().Image(headerImg);
                else
                    col.Item().Background(brand).Padding(30)
                       .AlignCenter().Text("TERRAPDF CO.").Bold().FontSize(28).FontColor(Color.White);

                // Coloured accent bar under banner
                col.Item().Background(accent).Padding(4);
            });

            // ── Content ──────────────────────────────────────────────────
            page.Content().Padding(40).Column(col =>
            {
                col.Spacing(14);

                // Company tagline
                col.Item().AlignCenter()
                   .Text("Building Tomorrow's Software, Today.")
                   .Bold().FontSize(17).FontColor(brand);

                col.Item().AlignCenter()
                   .Text("Established 2010  ·  San Francisco, CA  ·  www.terrapdf.example")
                   .FontColor(muted).FontSize(10);

                col.Item().LineHorizontal(1.5, accent);

                // About us
                col.Item().Background(light).Padding(12).Column(about =>
                {
                    about.Item().Text("ABOUT US").Bold().FontSize(10).FontColor(accent);
                    about.Item().PaddingTop(4).Text(
                        "TerraPDF Co. is a leading software engineering firm specialising in " +
                        "document generation, enterprise SaaS platforms, and cloud-native " +
                        "solutions. With over 200 engineers across three continents, we help " +
                        "Fortune 500 clients modernise their document workflows and unlock new " +
                        "levels of operational efficiency. Our open-source libraries are trusted " +
                        "by more than 40,000 developers worldwide.").Justify();
                });

                // Key metrics
                col.Item().Text("KEY METRICS AT A GLANCE").Bold().FontColor(brand);

                col.Item().Row(row =>
                {
                    void Metric(string val, string lbl, string bg)
                    {
                        row.RelativeItem().Margin(4).Background(bg).Border(1, accent).Padding(12).Column(m =>
                        {
                            m.Item().AlignCenter().Text(val).Bold().FontSize(24).FontColor(brand);
                            m.Item().AlignCenter().Text(lbl).FontSize(9).FontColor(muted);
                        });
                    }

                    Metric("200+",  "Engineers",        light);
                    Metric("40K+",  "OSS Developers",   Color.White);
                    Metric("$18M",  "ARR",               light);
                    Metric("98%",   "Client Retention",  Color.White);
                });

                // Leadership team table
                col.Item().Text("LEADERSHIP TEAM").Bold().FontColor(brand);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(4);
                    });

                    table.HeaderRow(row =>
                    {
                        row.Cell().Background(brand).Padding(6).Text("Name").Bold().FontColor(Color.White);
                        row.Cell().Background(brand).Padding(6).Text("Title").Bold().FontColor(Color.White);
                        row.Cell().Background(brand).Padding(6).Text("Background").Bold().FontColor(Color.White);
                    });

                    var team = new[]
                    {
                        ("Sarah Chen",      "CEO & Co-Founder", "15 yrs at Google, Stanford MBA"),
                        ("Marcus Obi",       "CTO",              "Former principal engineer at AWS"),
                        ("Priya Nair",       "CPO",              "Ex-product lead at Stripe"),
                        ("James Whitfield",  "CFO",              "CPA, ex-Goldman Sachs"),
                        ("Lena Hoffmann",    "VP Engineering",   "Distributed systems expert"),
                    };

                    bool shade = false;
                    foreach (var (name, title, bio) in team)
                    {
                        string bg = shade ? light : Color.White;
                        table.Row(row =>
                        {
                            row.Cell().Background(bg).Padding(6).Text(name).Bold();
                            row.Cell().Background(bg).Padding(6).Text(title).FontColor(accent);
                            row.Cell().Background(bg).Padding(6).Text(bio).FontColor(muted).FontSize(10);
                        });
                        shade = !shade;
                    }
                });

                // Services
                col.Item().Text("OUR SERVICES").Bold().FontColor(brand);

                col.Item().Row(row =>
                {
                    void Service(string icon, string title, string desc)
                    {
                        row.RelativeItem().Margin(4).Border(1, light).Padding(10).Column(s =>
                        {
                            s.Item().Text(icon + "  " + title).Bold().FontColor(brand);
                            s.Item().PaddingTop(4).Text(desc).FontSize(10).FontColor(muted).Justify();
                        });
                    }

                    Service("*", "PDF & Document APIs",
                        "High-performance document generation SDKs for .NET, Java, and Node.js.");
                    Service("*", "Cloud SaaS Platform",
                        "Scalable multi-tenant SaaS infrastructure with 99.99% SLA guarantee.");
                    Service("*", "Consulting & Training",
                        "On-site workshops, architecture reviews, and dedicated support plans.");
                });

                col.Item().LineHorizontal(1, Color.Grey.Lighten2);

                col.Item().AlignCenter()
                   .Text("Contact us: hello@terrapdf.example  |  +1 (415) 000-0000")
                   .FontColor(muted).FontSize(10);
            });

            // ── Footer: small logo + page number ─────────────────────────
            page.Footer().Padding(20).Row(row =>
            {
                row.ConstantItem(55).AlignMiddle().Column(c =>
                {
                    if (File.Exists(smallImg))
                        c.Item().Image(smallImg, 50);
                    else
                        c.Item().Text("TerraPDF").Bold().FontColor(brand);
                });

                row.RelativeItem().PaddingLeft(10).AlignMiddle().AlignRight().Text(t =>
                {
                    t.Span("Page ").FontSize(9).FontColor(muted);
                    t.CurrentPageNumber().FontSize(9).FontColor(muted);
                    t.Span(" / ").FontSize(9).FontColor(muted);
                    t.TotalPages().FontSize(9).FontColor(muted);
                });
            });
        });
    }).PublishPdf(path);

    Console.WriteLine($"  [4] Company profile  -> {path}");
}

// =============================================================================
//  5. EVENT BROCHURE
//  Shows: header banner + overlay text, agenda table with alternating rows,
//  speaker cards in two-column layout, sponsor logos row, call-to-action banner.
// =============================================================================
static void GenerateEventBrochure(string path, string headerImg, string smallImg)
{
    const string primary  = "#6A0572";
    const string gold     = "#D4A017";
    const string light    = "#FAF5FB";
    const string muted    = "#7A7A8C";

    Document.Create(doc =>
    {
        doc.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(0);
            page.PageColor(Color.White);
            page.DefaultTextStyle(s => s.FontSize(11));

            // ── Header: banner image + event title overlay ────────────────
            page.Header().Column(col =>
            {
                if (File.Exists(headerImg))
                    col.Item().Image(headerImg);
                else
                    col.Item().Background(primary).Padding(40);

                col.Item().Background(primary).Padding(12).Column(overlay =>
                {
                    overlay.Item().AlignCenter()
                           .Text("TECHVISION SUMMIT 2025").Bold().FontSize(22).FontColor(Color.White);
                    overlay.Item().AlignCenter()
                           .Text("September 18-19, 2025  |  Convention Center, San Francisco")
                           .FontColor(gold).FontSize(11);
                });
            });

            // ── Content ──────────────────────────────────────────────────
            page.Content().Padding(35).Column(col =>
            {
                col.Spacing(12);

                // Welcome
                col.Item().Background(light).Padding(12).Column(w =>
                {
                    w.Item().Text("WELCOME").Bold().FontSize(10).FontColor(gold);
                    w.Item().PaddingTop(4).Text(
                        "TechVision Summit brings together 2,000+ technology leaders, " +
                        "engineers, and innovators for two days of cutting-edge talks, " +
                        "workshops, and networking. Explore the future of AI, cloud, " +
                        "security, and developer tools — all in one place.").Justify();
                });

                // Agenda table
                col.Item().Text("CONFERENCE AGENDA – DAY 1").Bold().FontColor(primary);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(70);
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                    });

                    table.HeaderRow(row =>
                    {
                        row.Cell().Background(primary).Padding(6).Text("Time").Bold().FontColor(Color.White);
                        row.Cell().Background(primary).Padding(6).Text("Session").Bold().FontColor(Color.White);
                        row.Cell().Background(primary).Padding(6).Text("Track").Bold().FontColor(Color.White);
                    });

                    var agenda = new[]
                    {
                        ("09:00", "Registration & Breakfast",                       "All"),
                        ("10:00", "Opening Keynote: The Next Decade of AI",          "Keynote"),
                        ("11:00", "Building Resilient Cloud-Native Architectures",   "Cloud"),
                        ("11:00", "Zero-Trust Security in the Modern Enterprise",    "Security"),
                        ("12:00", "Lunch Break & Expo Hall",                         "All"),
                        ("13:00", "LLM Fine-Tuning at Scale",                        "AI"),
                        ("13:00", "Developer Experience: Making Teams 10x Faster",   "DevEx"),
                        ("14:30", "Panel: Open Source and the Enterprise",            "Open Source"),
                        ("15:30", "Coffee Break & Networking",                        "All"),
                        ("16:00", "Workshop: Hands-on Kubernetes GitOps",             "Cloud"),
                        ("16:00", "Workshop: RAG Pipelines with Local LLMs",          "AI"),
                        ("17:30", "Day 1 Closing & Drinks Reception",                 "All"),
                    };

                    bool shade = false;
                    foreach (var (time, session, track) in agenda)
                    {
                        string bg = shade ? light : Color.White;
                        table.Row(row =>
                        {
                            row.Cell().Background(bg).Padding(5).Text(time).Bold().FontColor(primary);
                            row.Cell().Background(bg).Padding(5).Text(session);
                            row.Cell().Background(bg).Padding(5).AlignCenter()
                               .Text(track).Italic().FontColor(muted).FontSize(10);
                        });
                        shade = !shade;
                    }
                });

                // Speakers
                col.Item().Text("FEATURED SPEAKERS").Bold().FontColor(primary);

                col.Item().Row(row =>
                {
                    void Speaker(string name, string role, string org, string topic)
                    {
                        row.RelativeItem().Margin(5).Border(1, Color.Grey.Lighten2).Padding(10).Column(s =>
                        {
                            s.Item().Background(primary).Padding(4)
                             .Text(name).Bold().FontColor(Color.White).FontSize(11);
                            s.Item().PaddingTop(4).Text(role).FontColor(gold).FontSize(10);
                            s.Item().Text(org).FontColor(muted).FontSize(10);
                            s.Item().PaddingTop(6).Text("Topic: " + topic)
                             .Italic().FontColor(primary).FontSize(10);
                        });
                    }

                    Speaker("Dr. Aiko Tanaka",  "Research Director",  "OpenMind Labs",    "The Next Decade of AI");
                    Speaker("Carlos Rivera",    "VP Engineering",     "CloudScale Inc.",  "Resilient Cloud Architectures");
                    Speaker("Emma Johansson",   "CISO",               "Fortress Security","Zero-Trust in Enterprise");
                });

                col.Item().LineHorizontal(1, Color.Grey.Lighten2);

                // Sponsors
                col.Item().Text("OUR SPONSORS").Bold().FontColor(primary);

                col.Item().Row(row =>
                {
                    void Sponsor(string tier, string name, string bg)
                    {
                        row.RelativeItem().Margin(3).Background(bg).Border(1, gold).Padding(10).Column(s =>
                        {
                            s.Item().AlignCenter().Text(tier).FontSize(8).FontColor(muted);
                            s.Item().AlignCenter().Text(name).Bold().FontColor(primary);
                            if (File.Exists(smallImg))
                                s.Item().AlignCenter().Image(smallImg, 40);
                        });
                    }

                    Sponsor("PLATINUM", "TerraPDF Co.",  light);
                    Sponsor("GOLD",     "CloudScale Inc.", Color.White);
                    Sponsor("GOLD",     "Fortress Sec.",   light);
                    Sponsor("SILVER",   "DevEx Labs",      Color.White);
                });

                // CTA banner
                col.Item().MarginTop(10).Background(primary).Padding(14).Column(cta =>
                {
                    cta.Item().AlignCenter()
                       .Text("REGISTER NOW — EARLY BIRD ENDS AUGUST 1")
                       .Bold().FontSize(14).FontColor(Color.White);
                    cta.Item().PaddingTop(4).AlignCenter()
                       .Text("summit.techvision.example  |  tickets@techvision.example  |  +1 (800) 123-4567")
                       .FontColor(gold).FontSize(10);
                });
            });

            // ── Footer ───────────────────────────────────────────────────
            page.Footer().Padding(16).Row(row =>
            {
                row.ConstantItem(55).AlignMiddle().Column(c =>
                {
                    if (File.Exists(smallImg))
                        c.Item().Image(smallImg, 45);
                });

                row.RelativeItem().PaddingLeft(8).AlignMiddle()
                   .Text("TechVision Summit 2025  |  summit.techvision.example")
                   .FontColor(muted).FontSize(9);

                row.AutoItem().AlignMiddle().AlignRight().Text(t =>
                {
                    t.Span("Page ").FontSize(9).FontColor(muted);
                    t.CurrentPageNumber().FontSize(9).FontColor(muted);
                    t.Span(" / ").FontSize(9).FontColor(muted);
                    t.TotalPages().FontSize(9).FontColor(muted);
                });
            });
        });
    }).PublishPdf(path);

    Console.WriteLine($"  [5] Event brochure   -> {path}");
}

// =============================================================================
//  6. PRODUCT CATALOGUE
//  Shows: branded header with small logo inset, multi-page product table with
//  repeating header row, feature-highlight column, pricing summary, full-width
//  header-banner image on a second section page.
// =============================================================================
static void GenerateProductCatalogue(string path, string headerImg, string smallImg)
{
    const string brand  = "#1A3C5E";
    const string teal   = "#0E8A87";
    const string light  = "#F0F7F7";
    const string orange = "#E8630A";
    const string muted  = "#607080";

    // Build 22 products to force table page-break with repeating header
    var products = new (string Code, string Name, string Category, string Features, decimal Price)[]
    {
        ("PDF-100", "TerraPDF Core",           "SDK",        "Text, tables, images",        199.00m),
        ("PDF-200", "TerraPDF Pro",             "SDK",        "All Core + charts, barcodes", 499.00m),
        ("PDF-300", "TerraPDF Enterprise",      "SDK",        "All Pro + FIPS, audit log",  1299.00m),
        ("CLO-101", "CloudRender Starter",      "SaaS",       "10K pages/mo, 3 users",        49.00m),
        ("CLO-201", "CloudRender Business",     "SaaS",       "100K pages/mo, 20 users",     149.00m),
        ("CLO-301", "CloudRender Enterprise",   "SaaS",       "Unlimited, SSO, SLA 99.99%",  599.00m),
        ("API-110", "REST API – Basic",         "API",        "50 req/min, public endpoints", 29.00m),
        ("API-210", "REST API – Advanced",      "API",        "500 req/min, webhooks",        99.00m),
        ("API-310", "REST API – Dedicated",     "API",        "Unlimited, private VPC",      399.00m),
        ("TMP-105", "Template Studio",          "Tool",       "Visual designer, 200 fonts",   79.00m),
        ("TMP-205", "Template Studio Pro",      "Tool",       "Custom fonts, team library",  199.00m),
        ("INT-120", "Salesforce Connector",     "Integration","Native SF objects to PDF",    149.00m),
        ("INT-130", "SharePoint Connector",     "Integration","On-prem & Online support",    149.00m),
        ("INT-140", "Google Drive Connector",   "Integration","Folder watch & auto-convert",  99.00m),
        ("INT-150", "Zapier App",               "Integration","2000+ Zap triggers",           49.00m),
        ("SEC-200", "Digital Signing Add-on",   "Security",   "PAdES, XAdES, qualified e-sig",299.00m),
        ("SEC-210", "Redaction Add-on",         "Security",   "PII auto-detect & redact",    199.00m),
        ("SUP-501", "Developer Support",        "Support",    "Email, 2-day SLA",             99.00m),
        ("SUP-502", "Priority Support",         "Support",    "Phone + email, 4-hr SLA",     299.00m),
        ("SUP-503", "Premier Support",          "Support",    "Dedicated engineer, 1-hr SLA",999.00m),
        ("TRN-601", "Online Training (10 hrs)", "Training",   "Self-paced video course",      79.00m),
        ("TRN-602", "On-Site Workshop (1 day)", "Training",   "Up to 12 attendees",         1500.00m),
    };

    Document.Create(doc =>
    {
        doc.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(0);
            page.PageColor(Color.White);
            page.DefaultTextStyle(s => s.FontSize(10));

            // ── Header: banner + small logo overlay ───────────────────────
            page.Header().Column(col =>
            {
                if (File.Exists(headerImg))
                    col.Item().Image(headerImg);
                else
                    col.Item().Background(brand).Padding(35);

                // Brand bar with small logo + catalogue title
                col.Item().Background(brand).Padding(10).Row(row =>
                {
                    row.AutoItem().AlignMiddle().Column(c =>
                    {
                        if (File.Exists(smallImg))
                            c.Item().Image(smallImg, 55);
                        else
                            c.Item().Text("TerraPDF").Bold().FontColor(Color.White);
                    });

                    row.RelativeItem().PaddingLeft(12).AlignMiddle().Column(c =>
                    {
                        c.Item().Text("PRODUCT CATALOGUE 2025")
                         .Bold().FontSize(16).FontColor(Color.White);
                        c.Item().Text("Complete Portfolio of SDKs, APIs & Services")
                         .FontColor(teal).FontSize(10);
                    });

                    row.AutoItem().AlignMiddle().AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("www.terrapdf.example")
                         .FontColor(Color.White).FontSize(9);
                        c.Item().AlignRight().Text("sales@terrapdf.example")
                         .FontColor(teal).FontSize(9);
                    });
                });

                col.Item().Background(teal).Padding(2);
            });

            // ── Content ──────────────────────────────────────────────────
            page.Content().Padding(30).Column(col =>
            {
                col.Spacing(12);

                // Introduction
                col.Item().Row(row =>
                {
                    row.RelativeItem(3).Margin(4).Background(light).Padding(12).Column(intro =>
                    {
                        intro.Item().Text("ABOUT THIS CATALOGUE").Bold().FontColor(teal).FontSize(9);
                        intro.Item().PaddingTop(4).Text(
                            "This catalogue lists all TerraPDF products, integrations, and " +
                            "support plans available in 2025. Prices are per month (SaaS) or " +
                            "one-time perpetual licence (SDK/Tools). Volume discounts available " +
                            "for orders of 5+ licences. Contact sales for custom pricing.")
                            .Justify().FontSize(10);
                    });

                    row.RelativeItem(1).Margin(4).Background(orange).Padding(12).Column(cta =>
                    {
                        cta.Item().AlignCenter()
                           .Text("NEW").Bold().FontSize(20).FontColor(Color.White);
                        cta.Item().PaddingTop(4).AlignCenter()
                           .Text("v4.0 Released!").Bold().FontColor(Color.White).FontSize(11);
                        cta.Item().PaddingTop(6).AlignCenter()
                           .Text("50% faster rendering, ARM64 native")
                           .FontColor(Color.White).FontSize(9);
                    });
                });

                // Full product table (22 rows – triggers multi-page with repeating header)
                col.Item().Text("FULL PRODUCT LISTING").Bold().FontColor(brand);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(68);   // Code
                        c.RelativeColumn(3);    // Name
                        c.RelativeColumn(2);    // Category
                        c.RelativeColumn(4);    // Key features
                        c.RelativeColumn(1);    // Price
                    });

                    table.HeaderRow(row =>
                    {
                        row.Cell().Background(brand).Padding(6).Text("Code").Bold().FontColor(Color.White);
                        row.Cell().Background(brand).Padding(6).Text("Product").Bold().FontColor(Color.White);
                        row.Cell().Background(brand).Padding(6).Text("Category").Bold().FontColor(Color.White);
                        row.Cell().Background(brand).Padding(6).Text("Key Features").Bold().FontColor(Color.White);
                        row.Cell().Background(brand).Padding(6).AlignRight().Text("Price ($)").Bold().FontColor(Color.White);
                    });

                    bool shade = false;
                    foreach (var (code, name, cat, feat, price) in products)
                    {
                        string bg = shade ? light : Color.White;
                        table.Row(row =>
                        {
                            row.Cell().Background(bg).Padding(5).Text(code)
                               .Bold().FontColor(teal).FontSize(9);
                            row.Cell().Background(bg).Padding(5).Text(name).Bold();
                            row.Cell().Background(bg).Padding(5).AlignCenter()
                               .Text(cat).Italic().FontColor(muted).FontSize(9);
                            row.Cell().Background(bg).Padding(5)
                               .Text(feat).FontColor(muted).FontSize(9);
                            row.Cell().Background(bg).Padding(5).AlignRight()
                               .Text($"{price:N2}").Bold().FontColor(brand);
                        });
                        shade = !shade;
                    }
                });

                // Pricing tiers summary
                col.Item().Text("PRICING TIERS AT A GLANCE").Bold().FontColor(brand);

                col.Item().Row(row =>
                {
                    void Tier(string name, string price, string desc, string bg, string fg)
                    {
                        row.RelativeItem().Margin(5).Background(bg).Border(1, teal).Padding(10).Column(t =>
                        {
                            t.Item().AlignCenter().Text(name).Bold().FontColor(fg).FontSize(11);
                            t.Item().AlignCenter().Text(price).Bold().FontSize(18).FontColor(fg);
                            t.Item().PaddingTop(4).Text(desc).FontSize(9).FontColor(muted).Justify();
                        });
                    }

                    Tier("Starter",    "From $29/mo",  "Ideal for indie developers and small teams.",       light,         brand);
                    Tier("Business",   "From $149/mo", "For growing companies needing scale & support.",    teal,          Color.White);
                    Tier("Enterprise", "Custom",        "Dedicated infrastructure, SLA & account manager.", brand,         Color.White);
                });

                col.Item().LineHorizontal(1, Color.Grey.Lighten2);

                // Contact footer row
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("GET IN TOUCH").Bold().FontColor(brand);
                        c.Item().Text("sales@terrapdf.example").FontColor(teal);
                        c.Item().Text("+1 (415) 000-0000");
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("DOCUMENTATION").Bold().FontColor(brand);
                        c.Item().Text("docs.terrapdf.example").FontColor(teal);
                        c.Item().Text("github.com/terrapdf").FontColor(muted);
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("LEGAL").Bold().FontColor(brand);
                        c.Item().Text("Prices excl. VAT/GST.").FontColor(muted).FontSize(9);
                        c.Item().Text("Subject to change without notice.").FontColor(muted).FontSize(9);
                    });
                });
            });

            // ── Footer ───────────────────────────────────────────────────
            page.Footer().Background(light).Padding(12).Row(row =>
            {
                row.ConstantItem(55).AlignMiddle().Column(c =>
                {
                    if (File.Exists(smallImg))
                        c.Item().Image(smallImg, 45);
                    else
                        c.Item().Text("TerraPDF").Bold().FontColor(brand);
                });

                row.RelativeItem().PaddingLeft(10).AlignMiddle()
                   .Text("(c) 2025 TerraPDF Co. Ltd. All rights reserved.")
                   .FontColor(muted).FontSize(9);

                row.AutoItem().AlignMiddle().AlignRight().Text(t =>
                {
                    t.Span("Page ").FontSize(9).FontColor(muted);
                    t.CurrentPageNumber().FontSize(9).FontColor(muted);
                    t.Span(" / ").FontSize(9).FontColor(muted);
                    t.TotalPages().FontSize(9).FontColor(muted);
                });
            });
        });
    }).PublishPdf(path);

    Console.WriteLine($"  [6] Product catalogue -> {path}");
}

// =============================================================================
//  7. PRODUCT CATALOG WITH COVER HEADER  (first-page-only header)
//  Shows: HeaderOnFirstPageOnly, full-bleed branded cover header drawn only on
//  page 1, clean continuation pages with no header, product-card grid using
//  RoundedBox, per-category section breaks with PageBreak, repeating footer,
//  pricing summary table, and a terms-and-conditions block.
// =============================================================================
static void GenerateProductCatalogCover(string path, string smallImg)
{
    const string brand    = "#0D3B66";    // deep navy
    const string accent   = "#F4A261";    // warm amber
    const string teal     = "#2EC4B6";    // teal highlight
    const string light    = "#F0F6FF";    // pale blue tint
    const string muted    = "#6B7C93";    // slate grey
    const string divider  = "#CBD5E0";    // light rule colour

    // ── Product data ─────────────────────────────────────────────────────────
    // Each product: (Code, Name, Tagline, Badge, BadgeColor, Price, Unit)
    var sdks = new (string Code, string Name, string Tagline,
                    string Badge, string BadgeColor, decimal Price, string Unit)[]
    {
        ("PDF-100", "TerraPDF Core",
            "Zero-dependency PDF generation for .NET 8 and .NET 9. " +
            "Text, tables, images, and fluent layout API included.",
            "STABLE", teal, 199.00m, "one-time"),

        ("PDF-200", "TerraPDF Pro",
            "Everything in Core plus charts, barcodes, digital signatures, " +
            "and priority email support.",
            "POPULAR", accent, 499.00m, "one-time"),

        ("PDF-300", "TerraPDF Enterprise",
            "All Pro features with FIPS 140-2 compliance, audit logging, " +
            "on-premise deployment, and a dedicated account manager.",
            "ENTERPRISE", brand, 1_299.00m, "one-time"),
    };

    var saas = new (string Code, string Name, string Tagline,
                    string Badge, string BadgeColor, decimal Price, string Unit)[]
    {
        ("CLO-101", "CloudRender Starter",
            "10,000 pages per month, 3 concurrent users, REST API access, " +
            "community support forum.",
            "FREE TRIAL", teal, 49.00m, "/ month"),

        ("CLO-201", "CloudRender Business",
            "100,000 pages per month, 20 users, webhook triggers, priority " +
            "support with 8-hour SLA.",
            "BEST VALUE", accent, 149.00m, "/ month"),

        ("CLO-301", "CloudRender Enterprise",
            "Unlimited pages, SSO/SAML, private VPC deployment, 99.99% uptime " +
            "SLA with dedicated support engineer.",
            "ENTERPRISE", brand, 599.00m, "/ month"),
    };

    var addons = new (string Code, string Name, string Tagline,
                      string Badge, string BadgeColor, decimal Price, string Unit)[]
    {
        ("SEC-200", "Digital Signing",
            "PAdES and XAdES qualified e-signatures, LTV support, " +
            "timestamp authority integration.",
            "NEW", accent, 299.00m, "/ month"),

        ("SEC-210", "PII Redaction",
            "Automatic detection and redaction of personal data using ML-based " +
            "pattern matching and regex rules.",
            "NEW", accent, 199.00m, "/ month"),

        ("INT-120", "Salesforce Connector",
            "Generate PDFs directly from Salesforce objects and flows. " +
            "Supports Classic and Lightning.",
            "STABLE", teal, 149.00m, "/ month"),

        ("INT-130", "SharePoint Connector",
            "On-premises and Online. Folder watch, auto-convert, and version " +
            "history preservation.",
            "STABLE", teal, 149.00m, "/ month"),

        ("TMP-205", "Template Studio Pro",
            "Visual drag-and-drop designer, custom font manager, team template " +
            "library with version control.",
            "UPDATED", teal, 199.00m, "one-time"),
    };

    // ── Comparison table data ─────────────────────────────────────────────────
    (string Feature, string Core, string Pro, string Ent)[] featureMatrix =
    [
        ("PDF 1.7 generation",         "✓", "✓", "✓"),
        ("Text & rich formatting",      "✓", "✓", "✓"),
        ("Tables & images",             "✓", "✓", "✓"),
        ("Hyperlinks & annotations",    "✓", "✓", "✓"),
        ("Rounded-corner boxes",        "✓", "✓", "✓"),
        ("Barcodes & QR codes",         "—", "✓", "✓"),
        ("Digital signatures (PAdES)",  "—", "✓", "✓"),
        ("Chart rendering",             "—", "✓", "✓"),
        ("FIPS 140-2 compliance",       "—", "—", "✓"),
        ("Audit logging",               "—", "—", "✓"),
        ("On-premise deployment",       "—", "—", "✓"),
        ("Dedicated account manager",   "—", "—", "✓"),
        ("SLA guarantee",               "—", "—", "✓"),
    ];

    // ── Document ─────────────────────────────────────────────────────────────
    Document.Create(doc =>
    {
        doc.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(2, Unit.Centimetre);
            page.PageColor(Color.White);
            page.DefaultTextStyle(s => s.FontSize(10).LineHeight(1.45));

            // ── COVER HEADER — first page only ────────────────────────────
            page.HeaderOnFirstPageOnly();

            page.Header().Column(col =>
            {
                // Full-width brand banner
                col.Item().Background(brand).Padding(22).Column(banner =>
                {
                    // Logo row
                    banner.Item().Row(logoRow =>
                    {
                        logoRow.AutoItem().AlignMiddle().Column(logo =>
                        {
                            if (File.Exists(smallImg))
                                logo.Item().Image(smallImg, 52);
                            else
                                logo.Item()
                                    .RoundedBox(radius: 8, fillHexColor: teal,
                                                borderHexColor: teal)
                                    .Padding(8)
                                    .Text("T").Bold().FontSize(22).FontColor(Color.White)
                                    .AlignCenter();
                        });

                        logoRow.RelativeItem().PaddingLeft(14).AlignMiddle().Column(title =>
                        {
                            title.Item()
                                 .Text("TerraPDF Product Catalog 2025")
                                 .Bold().FontSize(20).FontColor(Color.White);
                            title.Item().PaddingTop(3)
                                 .Text("SDKs  ·  Cloud APIs  ·  Add-ons  ·  Support Plans")
                                 .FontColor(teal).FontSize(10);
                        });

                        logoRow.AutoItem().AlignMiddle().AlignRight().Column(meta =>
                        {
                            meta.Item().AlignRight()
                                .Text("Q1 2025 Edition").FontColor(Color.White).FontSize(9);
                            meta.Item().AlignRight()
                                .Text("All prices in USD").FontColor(teal).FontSize(9);
                        });
                    });

                    // Thin teal divider inside banner
                    banner.Item().PaddingTop(14).LineHorizontal(1, teal);

                    // Three headline KPIs inside the cover banner
                    banner.Item().PaddingTop(10).Row(kpiRow =>
                    {
                        void Kpi(string value, string label)
                        {
                            kpiRow.RelativeItem().AlignCenter().Column(k =>
                            {
                                k.Item().AlignCenter()
                                 .Text(value).Bold().FontSize(18).FontColor(accent);
                                k.Item().AlignCenter()
                                 .Text(label).FontSize(8).FontColor(teal);
                            });
                        }

                        Kpi("Zero",    "native dependencies");
                        Kpi(".NET 8+", "multi-target");
                        Kpi("22+",     "products & add-ons");
                        Kpi("10M+",    "pages generated daily");
                    });
                });

                // Amber accent stripe below the banner
                col.Item().Background(accent).Padding(3);
            });

            // ── CONTENT ───────────────────────────────────────────────────
            page.Content().PaddingTop(0.6 * 28.35).Column(col =>
            {
                col.Spacing(10);

                // ── Section helper ─────────────────────────────────────────
                void SectionHeading(string title, string subtitle)
                {
                    col.Item().PaddingTop(4).Column(h =>
                    {
                        h.Item().Row(row =>
                        {
                            row.ConstantItem(4).Background(accent);
                            row.RelativeItem().PaddingLeft(10).Column(text =>
                            {
                                text.Item()
                                    .Text(title).Bold().FontSize(13).FontColor(brand);
                                text.Item()
                                    .Text(subtitle).FontSize(9).FontColor(muted);
                            });
                        });
                        h.Item().PaddingTop(4).LineHorizontal(1, divider);
                    });
                }

                // ── Product-card helper ────────────────────────────────────
                // Lays out a 2-column grid of rounded product cards.
                void ProductGrid(
                    (string Code, string Name, string Tagline,
                     string Badge, string BadgeColor, decimal Price, string Unit)[] products)
                {
                    for (int i = 0; i < products.Length; i += 2)
                    {
                        col.Item().Row(cardRow =>
                        {
                            void Card(int idx)
                            {
                                if (idx >= products.Length)
                                {
                                    cardRow.RelativeItem().Margin(4);   // empty slot
                                    return;
                                }

                                var (code, name, tagline, badge, badgeColor, price, unit) =
                                    products[idx];

                                cardRow.RelativeItem().Margin(4)
                                    .RoundedBox(radius: 8, fillHexColor: light,
                                                borderHexColor: divider, lineWidth: 1)
                                    .Padding(12).Column(card =>
                                    {
                                        // Top row: product code + badge pill
                                        card.Item().Row(top =>
                                        {
                                            top.RelativeItem()
                                               .Text(code).Bold().FontSize(8)
                                               .FontColor(muted);

                                            top.AutoItem()
                                               .RoundedBox(radius: 4,
                                                           fillHexColor: badgeColor,
                                                           borderHexColor: badgeColor)
                                               .Padding(3)
                                               .Text(badge).Bold().FontSize(7)
                                               .FontColor(Color.White);
                                        });

                                        // Product name
                                        card.Item().PaddingTop(4)
                                            .Text(name).Bold().FontSize(12)
                                            .FontColor(brand);

                                        // Tagline
                                        card.Item().PaddingTop(4)
                                            .Text(tagline).FontSize(9)
                                            .FontColor(muted).LineHeight(1.4)
                                            .Justify();

                                        // Divider + price
                                        card.Item().PaddingTop(8)
                                            .LineHorizontal(0.5, divider);

                                        card.Item().PaddingTop(6).Row(pricing =>
                                        {
                                            pricing.RelativeItem()
                                                   .Text("Price").FontSize(8)
                                                   .FontColor(muted);

                                            pricing.AutoItem().Column(p =>
                                            {
                                                p.Item().AlignRight()
                                                 .Text($"${price:N2}")
                                                 .Bold().FontSize(13).FontColor(brand);
                                                p.Item().AlignRight()
                                                 .Text(unit).FontSize(8).FontColor(muted);
                                            });
                                        });
                                    });
                            }

                            Card(i);
                            Card(i + 1);
                        });
                    }
                }

                // ── 1. SDK LICENCES ───────────────────────────────────────
                SectionHeading("SDK Licences",
                    "Perpetual licences — install on unlimited developer machines.");

                ProductGrid(sdks);

                // ── 2. CLOUD APIs  (new page — continuation has no header)
                col.PageBreak();

                SectionHeading("Cloud APIs  (SaaS)",
                    "Subscription-based rendering in the cloud — no server to maintain.");

                ProductGrid(saas);

                // ── 3. ADD-ONS & INTEGRATIONS ─────────────────────────────
                col.PageBreak();

                SectionHeading("Add-ons & Integrations",
                    "Extend your licence with optional capabilities and connectors.");

                ProductGrid(addons);

                // ── 4. FEATURE COMPARISON TABLE ───────────────────────────
                col.PageBreak();

                SectionHeading("SDK Feature Comparison",
                    "Side-by-side view of Core, Pro, and Enterprise capabilities.");

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(4);   // Feature name
                        c.RelativeColumn(1);   // Core
                        c.RelativeColumn(1);   // Pro
                        c.RelativeColumn(1);   // Enterprise
                    });

                    table.HeaderRow(row =>
                    {
                        row.Cell().Background(brand).Padding(7)
                           .Text("Feature").Bold().FontColor(Color.White);
                        row.Cell().Background(brand).Padding(7)
                           .AlignCenter().Text("Core").Bold().FontColor(Color.White);
                        row.Cell().Background(brand).Padding(7)
                           .AlignCenter().Text("Pro").Bold().FontColor(accent);
                        row.Cell().Background(brand).Padding(7)
                           .AlignCenter().Text("Enterprise").Bold().FontColor(teal);
                    });

                    bool shade = false;
                    foreach (var (feature, core, pro, ent) in featureMatrix)
                    {
                        string bg = shade ? light : Color.White;
                        table.Row(row =>
                        {
                            row.Cell().Background(bg).Padding(6).Text(feature);
                            row.Cell().Background(bg).Padding(6).AlignCenter()
                               .Text(core).FontColor(core == "✓" ? teal : muted)
                               .Bold();
                            row.Cell().Background(bg).Padding(6).AlignCenter()
                               .Text(pro).FontColor(pro == "✓" ? teal : muted)
                               .Bold();
                            row.Cell().Background(bg).Padding(6).AlignCenter()
                               .Text(ent).FontColor(ent == "✓" ? teal : muted)
                               .Bold();
                        });
                        shade = !shade;
                    }
                });

                // ── 5. TERMS & CONTACT ────────────────────────────────────
                col.Item().PaddingTop(8)
                   .RoundedBox(radius: 8, fillHexColor: light,
                               borderHexColor: divider, lineWidth: 1)
                   .Padding(14).Column(terms =>
                {
                    terms.Item().Text("Terms & Contact")
                         .Bold().FontSize(11).FontColor(brand);

                    terms.Item().PaddingTop(6).Row(contact =>
                    {
                        contact.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Sales").Bold().FontColor(brand).FontSize(9);
                            c.Item().Text("sales@terrapdf.example")
                             .FontColor(teal).Underline();
                            c.Item().Text("+1 (415) 000-0000").FontColor(muted);
                        });

                        contact.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Documentation").Bold().FontColor(brand)
                             .FontSize(9);
                            c.Item().Text("docs.terrapdf.example")
                             .FontColor(teal).Underline();
                            c.Item().Text("github.com/terrapdf").FontColor(muted);
                        });

                        contact.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Legal").Bold().FontColor(brand).FontSize(9);
                            c.Item().Text("Prices exclude VAT / GST.")
                             .FontColor(muted).FontSize(9);
                            c.Item().Text("Subject to change without notice.")
                             .FontColor(muted).FontSize(9);
                            c.Item().Text("Volume discounts available on request.")
                             .FontColor(muted).FontSize(9);
                        });
                    });
                });
            });

            // ── FOOTER — repeats on every page ────────────────────────────
            page.Footer().Column(footer =>
            {
                footer.Item().LineHorizontal(1, divider);
                footer.Item().PaddingTop(6).Row(row =>
                {
                    row.AutoItem().AlignMiddle().Column(logo =>
                    {
                        if (File.Exists(smallImg))
                            logo.Item().Image(smallImg, 32);
                        else
                            logo.Item().Text("TerraPDF").Bold().FontSize(9)
                                .FontColor(brand);
                    });

                    row.RelativeItem().PaddingLeft(8).AlignMiddle()
                       .Text("© 2025 TerraPDF Co. Ltd.  All rights reserved.")
                       .FontColor(muted).FontSize(8);

                    row.AutoItem().AlignMiddle().AlignRight().Text(t =>
                    {
                        t.Span("Page ").FontSize(8).FontColor(muted);
                        t.CurrentPageNumber().FontSize(8).FontColor(brand).Bold();
                        t.Span(" / ").FontSize(8).FontColor(muted);
                        t.TotalPages().FontSize(8).FontColor(brand).Bold();
                    });
                });
            });
        });
    }).PublishPdf(path);

    Console.WriteLine($"  [7] Product catalog cover -> {path}");
}

