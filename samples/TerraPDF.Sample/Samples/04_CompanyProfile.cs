using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  4. COMPANY PROFILE
//  Shows: full-width header banner image, small logo in footer, about section,
//  key-metrics row, team table, services list, multi-page with logo footer.
// =============================================================================
internal static class CompanyProfile
{
    internal static void Generate(string path, string headerImg, string smallImg)
    {
        const string brand  = "#0D3349";
        const string accent = "#F4A020";
        const string light  = "#F7F9FC";
        const string muted  = "#607080";

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

        Console.WriteLine($"  [4] Company profile       -> {path}");
    }
}
