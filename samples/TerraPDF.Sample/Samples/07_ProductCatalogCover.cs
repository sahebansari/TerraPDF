using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  7. PRODUCT CATALOG WITH COVER HEADER  (first-page-only header)
//  Shows: HeaderOnFirstPageOnly, full-bleed branded cover header drawn only on
//  page 1, clean continuation pages with no header, product-card grid using
//  RoundedBox, per-category section breaks with PageBreak, repeating footer,
//  pricing summary table, and a terms-and-conditions block.
// =============================================================================
internal static class ProductCatalogCover
{
    internal static void Generate(string path, string smallImg)
    {
        const string brand   = "#0D3B66";
        const string accent  = "#F4A261";
        const string teal    = "#2EC4B6";
        const string light   = "#F0F6FF";
        const string muted   = "#6B7C93";
        const string divider = "#CBD5E0";

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

        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(10).LineHeight(1.45));

                page.HeaderOnFirstPageOnly();

                page.Header().Column(col =>
                {
                    col.Item().Background(brand).Padding(22).Column(banner =>
                    {
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

                        banner.Item().PaddingTop(14).LineHorizontal(1, teal);

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

                    col.Item().Background(accent).Padding(3);
                });

                page.Content().PaddingTop(0.6 * 28.35).Column(col =>
                {
                    col.Spacing(10);

                    void SectionHeading(string title, string subtitle)
                    {
                        col.Item().PaddingTop(4).Column(h =>
                        {
                            h.Item().Row(row =>
                            {
                                row.ConstantItem(4).Background(accent);
                                row.RelativeItem().PaddingLeft(10).Column(text =>
                                {
                                    text.Item().Text(title).Bold().FontSize(13).FontColor(brand);
                                    text.Item().Text(subtitle).FontSize(9).FontColor(muted);
                                });
                            });
                            h.Item().PaddingTop(4).LineHorizontal(1, divider);
                        });
                    }

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
                                        cardRow.RelativeItem().Margin(4);
                                        return;
                                    }

                                    var (code, name, tagline, badge, badgeColor, price, unit) = products[idx];

                                    cardRow.RelativeItem().Margin(4)
                                        .RoundedBox(radius: 8, fillHexColor: light,
                                                    borderHexColor: divider, lineWidth: 1)
                                        .Padding(12).Column(card =>
                                        {
                                            card.Item().Row(top =>
                                            {
                                                top.RelativeItem()
                                                   .Text(code).Bold().FontSize(8).FontColor(muted);
                                                top.AutoItem()
                                                   .RoundedBox(radius: 4,
                                                               fillHexColor: badgeColor,
                                                               borderHexColor: badgeColor)
                                                   .Padding(3)
                                                   .Text(badge).Bold().FontSize(7).FontColor(Color.White);
                                            });

                                            card.Item().PaddingTop(4)
                                                .Text(name).Bold().FontSize(12).FontColor(brand);

                                            card.Item().PaddingTop(4)
                                                .Text(tagline).FontSize(9).FontColor(muted)
                                                .LineHeight(1.4).Justify();

                                            card.Item().PaddingTop(8).LineHorizontal(0.5, divider);

                                            card.Item().PaddingTop(6).Row(pricing =>
                                            {
                                                pricing.RelativeItem()
                                                       .Text("Price").FontSize(8).FontColor(muted);
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

                    SectionHeading("SDK Licences",
                        "Perpetual licences — install on unlimited developer machines.");
                    ProductGrid(sdks);

                    col.PageBreak();
                    SectionHeading("Cloud APIs  (SaaS)",
                        "Subscription-based rendering in the cloud — no server to maintain.");
                    ProductGrid(saas);

                    col.PageBreak();
                    SectionHeading("Add-ons & Integrations",
                        "Extend your licence with optional capabilities and connectors.");
                    ProductGrid(addons);

                    col.PageBreak();
                    SectionHeading("SDK Feature Comparison",
                        "Side-by-side view of Core, Pro, and Enterprise capabilities.");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });

                        table.HeaderRow(row =>
                        {
                            row.Cell().Background(brand).Padding(7).Text("Feature").Bold().FontColor(Color.White);
                            row.Cell().Background(brand).Padding(7).AlignCenter().Text("Core").Bold().FontColor(Color.White);
                            row.Cell().Background(brand).Padding(7).AlignCenter().Text("Pro").Bold().FontColor(accent);
                            row.Cell().Background(brand).Padding(7).AlignCenter().Text("Enterprise").Bold().FontColor(teal);
                        });

                        bool shade = false;
                        foreach (var (feature, core, pro, ent) in featureMatrix)
                        {
                            string bg = shade ? light : Color.White;
                            table.Row(row =>
                            {
                                row.Cell().Background(bg).Padding(6).Text(feature);
                                row.Cell().Background(bg).Padding(6).AlignCenter()
                                   .Text(core).FontColor(core == "✓" ? teal : muted).Bold();
                                row.Cell().Background(bg).Padding(6).AlignCenter()
                                   .Text(pro).FontColor(pro == "✓" ? teal : muted).Bold();
                                row.Cell().Background(bg).Padding(6).AlignCenter()
                                   .Text(ent).FontColor(ent == "✓" ? teal : muted).Bold();
                            });
                            shade = !shade;
                        }
                    });

                    col.Item().PaddingTop(8)
                       .RoundedBox(radius: 8, fillHexColor: light,
                                   borderHexColor: divider, lineWidth: 1)
                       .Padding(14).Column(terms =>
                    {
                        terms.Item().Text("Terms & Contact").Bold().FontSize(11).FontColor(brand);

                        terms.Item().PaddingTop(6).Row(contact =>
                        {
                            contact.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Sales").Bold().FontColor(brand).FontSize(9);
                                c.Item().Text("sales@terrapdf.example").FontColor(teal).Underline();
                                c.Item().Text("+1 (415) 000-0000").FontColor(muted);
                            });

                            contact.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Documentation").Bold().FontColor(brand).FontSize(9);
                                c.Item().Text("docs.terrapdf.example").FontColor(teal).Underline();
                                c.Item().Text("github.com/terrapdf").FontColor(muted);
                            });

                            contact.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Legal").Bold().FontColor(brand).FontSize(9);
                                c.Item().Text("Prices exclude VAT / GST.").FontColor(muted).FontSize(9);
                                c.Item().Text("Subject to change without notice.").FontColor(muted).FontSize(9);
                                c.Item().Text("Volume discounts available on request.").FontColor(muted).FontSize(9);
                            });
                        });
                    });
                });

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
                                logo.Item().Text("TerraPDF").Bold().FontSize(9).FontColor(brand);
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
}
