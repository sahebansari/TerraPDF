using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  6. PRODUCT CATALOGUE
//  Shows: branded header with small logo inset, multi-page product table with
//  repeating header row, feature-highlight column, pricing summary, full-width
//  header-banner image on a second section page.
// =============================================================================
internal static class ProductCatalogue
{
    internal static void Generate(string path, string headerImg, string smallImg)
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

                        Tier("Starter",    "From $29/mo",  "Ideal for indie developers and small teams.",       light,  brand);
                        Tier("Business",   "From $149/mo", "For growing companies needing scale & support.",    teal,   Color.White);
                        Tier("Enterprise", "Custom",        "Dedicated infrastructure, SLA & account manager.", brand,  Color.White);
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

        Console.WriteLine($"  [6] Product catalogue     -> {path}");
    }
}
